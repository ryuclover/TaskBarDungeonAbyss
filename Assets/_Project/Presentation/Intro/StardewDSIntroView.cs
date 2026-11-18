using System;
using System.Collections;
using TBDA.Data;
using TBDA.Domain.Pets;
using TBDA.Domain.Session;
using TBDA.Presentation.Audio;
using TBDA.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TBDA.Presentation.Intro
{
    /// <summary>
    /// Nintendo DS Dual-Screen (2x 256x192) Stardew Valley-style narrative intro:
    /// 0. Choose Dog / Cat / Spirit-Golem & Name on the Bottom Touch Screen
    /// 1. Growing up together through the seasons
    /// 2. Growing old & quiet farewell
    /// 3. Rainy night PC -> mysterious email from <pet>@dungeon.abyss -> Launch Taskbar Dungeon!
    /// </summary>
    public sealed class StardewDSIntroView : MonoBehaviour
    {
        private GameSessionService _session;
        private GameDatabaseSO _db;
        private DSChiptuneSynthesizer _audio;
        private Action _onIntroFinished;

        private GameObject _rootCanvasGo;
        private Image _topScreenArt;
        private Image _topPetPreview;
        private Text _topCaptionText;

        private RectTransform _stepAdoptPanel;
        private RectTransform _stepStoryPanel;
        private RectTransform _stepEmailPanel;
        private Image _flashOverlay;

        private InputField _nameInput;
        private Text _speciesDescText;
        private Text _storyBodyText;
        private Text _emailHeaderText;
        private Text _emailBodyText;
        private Text _exeButtonLabel;

        private PetSpeciesId _selectedSpecies = PetSpeciesId.Dog;
        private string _chosenName = "Pipoca";
        private int _storyStepIndex = 0;
        private float _animTimer;
        private Coroutine _typewriterRoutine;

        public void Initialize(
            GameSessionService session,
            GameDatabaseSO db,
            DSChiptuneSynthesizer audio,
            Action onIntroFinished)
        {
            _session = session;
            _db = db;
            _audio = audio;
            _onIntroFinished = onIntroFinished;

            BuildDualScreenCanvas();
            SelectSpecies(PetSpeciesId.Dog, playSound: false);
            ShowStep(0);
        }

        public void SetVisible(bool visible)
        {
            if (_rootCanvasGo != null)
                _rootCanvasGo.SetActive(visible);
        }

        private void Update()
        {
            if (_rootCanvasGo == null || !_rootCanvasGo.activeSelf || _db == null) return;

            // Animate pet preview at 3 FPS (classic DS sprite charm)
            _animTimer += Time.deltaTime;
            var spec = _db.GetSpecies(_selectedSpecies);
            if (spec != null && _topPetPreview != null && _topPetPreview.gameObject.activeSelf)
            {
                if (_storyStepIndex == 2 && spec.SleepSprite != null)
                {
                    _topPetPreview.sprite = spec.SleepSprite;
                }
                else if (spec.IdleSprites != null && spec.IdleSprites.Length > 0)
                {
                    int idx = Mathf.FloorToInt(_animTimer * 3f) % spec.IdleSprites.Length;
                    _topPetPreview.sprite = spec.IdleSprites[idx];
                }
            }
        }

        private void BuildDualScreenCanvas()
        {
            _rootCanvasGo = new GameObject("DS_IntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _rootCanvasGo.transform.SetParent(transform, false);

            var canvas = _rootCanvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = _rootCanvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Authentic 2x Nintendo DS stacked resolution (256x192 top + hinge + 256x192 bottom)
            scaler.referenceResolution = new Vector2(540f, 810f);
            scaler.matchWidthOrHeight = 0.5f;

            // DS Console Outer Shell
            var shell = DSPixelUIBuilder.CreatePanel(
                _rootCanvasGo.transform, "DS_Shell",
                new Color(0.11f, 0.11f, 0.17f, 1f),
                DSPixelUIBuilder.DsBorderSlate,
                Vector2.zero, new Vector2(524f, 792f));

            // --- TOP SCREEN (256x192 scaled 2x -> 496x368) ---
            var topBezel = DSPixelUIBuilder.CreatePanel(
                shell, "TopScreen_Bezel",
                Color.black, DSPixelUIBuilder.DsBorderGold,
                new Vector2(0f, 192f), new Vector2(496f, 372f));

            var artGo = new GameObject("TopArt", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(topBezel, false);
            var artRt = artGo.GetComponent<RectTransform>();
            artRt.anchorMin = Vector2.zero;
            artRt.anchorMax = Vector2.one;
            artRt.offsetMin = new Vector2(4f, 4f);
            artRt.offsetMax = new Vector2(-4f, -4f);
            _topScreenArt = artGo.GetComponent<Image>();
            _topScreenArt.sprite = DSPixelUIBuilder.GetWhiteSprite();

            // Pet sprite standing in the memory scene!
            var petGo = new GameObject("PetSceneSprite", typeof(RectTransform), typeof(Image));
            petGo.transform.SetParent(topBezel, false);
            var petRt = petGo.GetComponent<RectTransform>();
            petRt.anchoredPosition = new Vector2(0f, -68f);
            petRt.sizeDelta = new Vector2(112f, 112f);
            _topPetPreview = petGo.GetComponent<Image>();

            var topBanner = DSPixelUIBuilder.CreatePanel(
                topBezel, "TitleBanner",
                new Color(0.06f, 0.06f, 0.12f, 0.84f), DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, 150f), new Vector2(468f, 42f));
            _topCaptionText = DSPixelUIBuilder.CreateText(
                topBanner, "BannerText",
                "TASK BAR: DUNGEON ABYSS — Prólogo", 16,
                DSPixelUIBuilder.DsBorderGold, TextAnchor.MiddleCenter);
            StretchFull(_topCaptionText.rectTransform, 6f);

            // DS Center Hinge + Power LEDs
            var hinge = DSPixelUIBuilder.CreatePanel(
                shell, "DS_Hinge",
                new Color(0.07f, 0.07f, 0.11f, 1f), DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, 0f), new Vector2(512f, 20f));
            var led = DSPixelUIBuilder.CreateText(
                hinge, "LED", "● POWER   ● PIXEL DS MODE", 11,
                DSPixelUIBuilder.DsAccentGreen, TextAnchor.MiddleRight);
            StretchFull(led.rectTransform, 8f);

            // --- BOTTOM TOUCH SCREEN (496x372) ---
            var bottomBezel = DSPixelUIBuilder.CreatePanel(
                shell, "BottomTouchScreen",
                DSPixelUIBuilder.DsBgDark, DSPixelUIBuilder.DsAccentCyan,
                new Vector2(0f, -192f), new Vector2(496f, 372f));

            BuildStep0AdoptPanel(bottomBezel);
            BuildStepStoryPanel(bottomBezel);
            BuildStepEmailPanel(bottomBezel);

            // Skip button for fast developer iteration
            DSPixelUIBuilder.CreateButton(
                shell, "SkipIntroBtn", "Pular Intro »",
                new Vector2(198f, 374f), new Vector2(106f, 26f),
                new Color(0.20f, 0.20f, 0.30f, 0.9f), 11,
                () =>
                {
                    _chosenName = _nameInput != null && !string.IsNullOrWhiteSpace(_nameInput.text)
                        ? _nameInput.text
                        : _chosenName;
                    FinishIntroTransition();
                });

            // White warp flash overlay
            var flashGo = new GameObject("WarpFlash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(_rootCanvasGo.transform, false);
            StretchFull(flashGo.GetComponent<RectTransform>(), 0f);
            _flashOverlay = flashGo.GetComponent<Image>();
            _flashOverlay.sprite = DSPixelUIBuilder.GetWhiteSprite();
            _flashOverlay.color = new Color(1f, 1f, 1f, 0f);
            _flashOverlay.raycastTarget = false;
        }

        private void BuildStep0AdoptPanel(RectTransform parent)
        {
            _stepAdoptPanel = DSPixelUIBuilder.CreatePanel(
                parent, "Step0_Adopt",
                DSPixelUIBuilder.DsPanelMid, DSPixelUIBuilder.DsBorderSlate,
                Vector2.zero, new Vector2(476f, 352f));

            var title = DSPixelUIBuilder.CreateText(
                _stepAdoptPanel, "Prompt",
                "Com qual companheiro você cresceu?", 17,
                DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleCenter);
            title.rectTransform.anchoredPosition = new Vector2(0f, 142f);
            title.rectTransform.sizeDelta = new Vector2(440f, 32f);

            // 3 Species Choice Buttons
            DSPixelUIBuilder.CreateButton(
                _stepAdoptPanel, "BtnDog", "Cãozinho",
                new Vector2(-146f, 88f), new Vector2(134f, 46f),
                new Color(0.45f, 0.28f, 0.16f, 1f), 15,
                () => SelectSpecies(PetSpeciesId.Dog, true));

            DSPixelUIBuilder.CreateButton(
                _stepAdoptPanel, "BtnCat", "Gatinho",
                new Vector2(0f, 88f), new Vector2(134f, 46f),
                new Color(0.28f, 0.24f, 0.44f, 1f), 15,
                () => SelectSpecies(PetSpeciesId.Cat, true));

            DSPixelUIBuilder.CreateButton(
                _stepAdoptPanel, "BtnGolem", "Espírito/Golem",
                new Vector2(146f, 88f), new Vector2(138f, 46f),
                new Color(0.18f, 0.36f, 0.38f, 1f), 14,
                () => SelectSpecies(PetSpeciesId.SpiritGolem, true));

            _speciesDescText = DSPixelUIBuilder.CreateText(
                _stepAdoptPanel, "SpeciesDesc", "", 13,
                DSPixelUIBuilder.DsTextMuted, TextAnchor.MiddleCenter);
            _speciesDescText.rectTransform.anchoredPosition = new Vector2(0f, 28f);
            _speciesDescText.rectTransform.sizeDelta = new Vector2(430f, 48f);

            // Name Input Box
            var nameLbl = DSPixelUIBuilder.CreateText(
                _stepAdoptPanel, "NameLbl", "Qual era o nome dele(a)?", 14,
                DSPixelUIBuilder.DsBorderGold, TextAnchor.MiddleCenter);
            nameLbl.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            nameLbl.rectTransform.sizeDelta = new Vector2(360f, 24f);

            var inputBg = DSPixelUIBuilder.CreatePanel(
                _stepAdoptPanel, "NameInputBg",
                new Color(0.06f, 0.06f, 0.10f, 1f), DSPixelUIBuilder.DsAccentCyan,
                new Vector2(0f, -62f), new Vector2(280f, 42f));

            var inputText = DSPixelUIBuilder.CreateText(
                inputBg, "Text", "Pipoca", 18,
                DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleCenter);
            StretchFull(inputText.rectTransform, 6f);

            var placeholder = DSPixelUIBuilder.CreateText(
                inputBg, "Placeholder", "Digite o nome...", 16,
                new Color(0.5f, 0.5f, 0.6f, 0.7f), TextAnchor.MiddleCenter);
            StretchFull(placeholder.rectTransform, 6f);

            _nameInput = inputBg.gameObject.AddComponent<InputField>();
            _nameInput.textComponent = inputText;
            _nameInput.placeholder = placeholder;
            _nameInput.characterLimit = 14;
            _nameInput.text = "Pipoca";

            DSPixelUIBuilder.CreateButton(
                _stepAdoptPanel, "StartStoryBtn", "Lembrar da nossa história ►",
                new Vector2(0f, -128f), new Vector2(310f, 50f),
                new Color(0.18f, 0.46f, 0.28f, 1f), 16,
                () =>
                {
                    _audio?.PlayUiTap();
                    _chosenName = string.IsNullOrWhiteSpace(_nameInput.text) ? _chosenName : _nameInput.text.Trim();
                    ShowStep(1);
                });
        }

        private void BuildStepStoryPanel(RectTransform parent)
        {
            _stepStoryPanel = DSPixelUIBuilder.CreatePanel(
                parent, "Step_Story",
                DSPixelUIBuilder.DsPanelMid, DSPixelUIBuilder.DsBorderGold,
                Vector2.zero, new Vector2(476f, 352f));

            _storyBodyText = DSPixelUIBuilder.CreateText(
                _stepStoryPanel, "StoryBody", "", 17,
                DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleCenter);
            _storyBodyText.rectTransform.anchoredPosition = new Vector2(0f, 26f);
            _storyBodyText.rectTransform.sizeDelta = new Vector2(420f, 200f);

            DSPixelUIBuilder.CreateButton(
                _stepStoryPanel, "NextSlideBtn", "Continuar (Toque / Clique) ►",
                new Vector2(0f, -122f), new Vector2(290f, 48f),
                new Color(0.24f, 0.32f, 0.52f, 1f), 15,
                () =>
                {
                    _audio?.PlayUiTap();
                    ShowStep(_storyStepIndex + 1);
                });
        }

        private void BuildStepEmailPanel(RectTransform parent)
        {
            _stepEmailPanel = DSPixelUIBuilder.CreatePanel(
                parent, "Step_Email",
                new Color(0.09f, 0.13f, 0.20f, 1f), DSPixelUIBuilder.DsAccentCyan,
                Vector2.zero, new Vector2(476f, 352f));

            _emailHeaderText = DSPixelUIBuilder.CreateText(
                _stepEmailPanel, "EmailHeader", "", 14,
                DSPixelUIBuilder.DsAccentCyan, TextAnchor.UpperLeft);
            _emailHeaderText.rectTransform.anchoredPosition = new Vector2(0f, 116f);
            _emailHeaderText.rectTransform.sizeDelta = new Vector2(432f, 68f);

            var mailBox = DSPixelUIBuilder.CreatePanel(
                _stepEmailPanel, "MailBodyBox",
                new Color(0.05f, 0.07f, 0.12f, 1f), DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, 8f), new Vector2(436f, 132f));

            _emailBodyText = DSPixelUIBuilder.CreateText(
                mailBox, "MailBody", "", 14,
                DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleLeft);
            StretchFull(_emailBodyText.rectTransform, 12f);

            var launchBtn = DSPixelUIBuilder.CreateButton(
                _stepEmailPanel, "LaunchAttachmentBtn", "EXECUTAR ANEXO",
                new Vector2(0f, -114f), new Vector2(360f, 56f),
                DSPixelUIBuilder.DsAccentWarm, 16,
                () => StartCoroutine(CoPlayAwakeningSequence()));
            _exeButtonLabel = launchBtn.GetComponentInChildren<Text>();
        }

        private void SelectSpecies(PetSpeciesId id, bool playSound)
        {
            _selectedSpecies = id;
            if (playSound) _audio?.PlayUiTap();

            string defaultName;
            string desc;
            switch (id)
            {
                case PetSpeciesId.Dog:
                    defaultName = "Pipoca";
                    desc = "Leal e corajoso (+FOR / +VIT iniciais). Sempre protegeu você desde filhote.";
                    break;
                case PetSpeciesId.Cat:
                    defaultName = "Mimi";
                    desc = "Curioso e veloz (+AGI inicial). Adorava cochilar perto do teclado.";
                    break;
                default:
                    defaultName = "Rocco";
                    desc = "Pequeno espírito de pedra musgosa estilo Golem Hearts (+VIT / +FOR iniciais).";
                    break;
            }

            _speciesDescText.text = desc;
            if (_nameInput != null && (_nameInput.text == "Pipoca" || _nameInput.text == "Mimi" || _nameInput.text == "Rocco"))
            {
                _nameInput.text = defaultName;
            }
            _chosenName = _nameInput != null ? _nameInput.text : defaultName;
        }

        private void ShowStep(int step)
        {
            _storyStepIndex = step;
            _stepAdoptPanel.gameObject.SetActive(step == 0);
            _stepStoryPanel.gameObject.SetActive(step == 1 || step == 2);
            _stepEmailPanel.gameObject.SetActive(step == 3);

            if (step == 0)
            {
                if (_db != null && _db.IntroAdoptSprite != null) _topScreenArt.sprite = _db.IntroAdoptSprite;
                _topPetPreview.gameObject.SetActive(true);
                _topPetPreview.rectTransform.anchoredPosition = new Vector2(0f, -68f);
                _topCaptionText.text = "TASK BAR: DUNGEON ABYSS — Memórias";
            }
            else if (step == 1)
            {
                if (_db != null && _db.IntroGrowingSprite != null) _topScreenArt.sprite = _db.IntroGrowingSprite;
                _topPetPreview.gameObject.SetActive(true);
                _topCaptionText.text = $"As tardes de outono com {_chosenName}...";
                PlayTypewriter(
                    $"Você cresceu junto com {_chosenName}.\n\n" +
                    "Chuva ou sol, vitórias ou dias difíceis — bastava olhar para o lado e seu companheiro estava ali, abanando e esperando carinho.");
            }
            else if (step == 2)
            {
                if (_db != null && _db.IntroFarewellSprite != null) _topScreenArt.sprite = _db.IntroFarewellSprite;
                _topPetPreview.gameObject.SetActive(true);
                _topCaptionText.text = "O tempo passou devagar...";
                PlayTypewriter(
                    $"Os anos passaram. {_chosenName} ficou velhinho, cansado, e num fim de noite tranquilo descansou para sempre.\n\n" +
                    "A tela do seu computador nunca pareceu tão vazia...");
            }
            else if (step == 3)
            {
                _audio?.PlayEmailChime();
                if (_db != null && _db.IntroPcEmailSprite != null) _topScreenArt.sprite = _db.IntroPcEmailSprite;
                _topPetPreview.gameObject.SetActive(false);
                _topCaptionText.text = "1 NOVA MENSAGEM RECEBIDA (02:14 AM)";

                string slug = _chosenName.ToLowerInvariant().Replace(" ", "_");
                _emailHeaderText.text =
                    $"✉ CAIXA DE ENTRADA\n" +
                    $"De: {slug}@dungeon.abyss\n" +
                    $"Assunto: escuro aqui embaixo... você tá aí?";

                _emailBodyText.text =
                    $"\"Acordei numa masmorra fria dentro da sua Barra de Tarefas.\n" +
                    $"Tem monstros e portas que mudam de lugar... mas ouvi o barulho do seu mouse!\n" +
                    $"Não me deixa sozinho de novo. Clica aqui pra cuidar de mim!\"";

                _exeButtonLabel.text = $"[ INICIAR {_chosenName}_Taskbar.exe ]";
            }
        }

        private void PlayTypewriter(string fullText)
        {
            if (_typewriterRoutine != null) StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = StartCoroutine(CoTypewriter(fullText));
        }

        private IEnumerator CoTypewriter(string fullText)
        {
            _storyBodyText.text = "";
            for (int i = 0; i < fullText.Length; i++)
            {
                _storyBodyText.text = fullText.Substring(0, i + 1);
                if (i % 4 == 0 && fullText[i] != ' ')
                    _audio?.PlayTypewriterBlip();
                yield return new WaitForSeconds(0.018f);
            }
        }

        private IEnumerator CoPlayAwakeningSequence()
        {
            _audio?.PlayDungeonBootWarp();
            float t = 0f;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                _flashOverlay.color = new Color(0.85f, 0.96f, 1f, Mathf.Clamp01(t / 0.45f));
                yield return null;
            }
            FinishIntroTransition();
        }

        private void FinishIntroTransition()
        {
            _session.CompleteIntroAndAwakenPet(_selectedSpecies, _chosenName);
            SetVisible(false);
            _onIntroFinished?.Invoke();
        }

        private static void StretchFull(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
