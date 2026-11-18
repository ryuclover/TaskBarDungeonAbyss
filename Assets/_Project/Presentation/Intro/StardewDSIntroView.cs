using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Nintendo DS Dual-Screen (2x 256x192) Cutscene & Starter Selection View
    /// inspired by Pokémon Emerald (GBA), Pokémon HeartGold/SoulSilver & Mystery Dungeon (NDS),
    /// and Stardew Valley's emotional opening prologue:
    /// - Step 0: Choose Dog / Cat / Spirit-Golem via visual Pokémon-style Starter Cards + Name Entry
    /// - Step 1: Growing up together through the golden autumn seasons (with falling autumn leaves)
    /// - Step 2: Quiet farewell on the starry hilltop (with rising cyan spirit fireflies)
    /// - Step 3: Rainy 2:14 AM bedroom PC -> mysterious email from <pet>@dungeon.abyss -> Awaken in Taskbar!
    /// </summary>
    public sealed class StardewDSIntroView : MonoBehaviour
    {
        private struct WeatherParticle
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Velocity;
            public float Phase;
        }

        private GameSessionService _session;
        private GameDatabaseSO _db;
        private DSChiptuneSynthesizer _audio;
        private Action _onIntroFinished;

        private GameObject _rootCanvasGo;
        private Image _topScreenArt;
        private RectTransform _weatherContainer;
        private readonly List<WeatherParticle> _particles = new List<WeatherParticle>();

        private Image _topPetPreview;
        private Image _topEmoteBubbleBg;
        private Text _topEmoteText;
        private Text _topCaptionText;
        private Image _topChapterFade;

        private RectTransform _stepAdoptPanel;
        private RectTransform _stepStoryPanel;
        private RectTransform _stepEmailPanel;
        private Image _flashOverlay;

        // Step 0 Starter Cards
        private readonly Image[] _starterCardFrames = new Image[3];
        private readonly Image[] _starterCardPortraits = new Image[3];
        private InputField _nameInput;
        private Text _speciesDescText;
        private Text _speciesStatsBadgeText;

        // Step 1 & 2 Pokémon Mystery Dungeon-style Portrait Dialogue Box
        private Image _dialoguePortraitImg;
        private Text _speakerNameTagText;
        private Text _storyBodyText;
        private Text _blinkingAdvanceArrow;

        // Step 3 Email Inbox
        private Image _emailPetAvatarImg;
        private Text _emailHeaderText;
        private Text _emailBodyText;
        private Text _exeButtonLabel;

        private PetSpeciesId _selectedSpecies = PetSpeciesId.Dog;
        private string _chosenName = "Pipoca";
        private int _storyStepIndex = 0;
        private float _animTimer;
        private Coroutine _typewriterRoutine;
        private Coroutine _stepTransitionRoutine;

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
            PopulateStarterCardPortraits();
            SelectSpecies(PetSpeciesId.Dog, playSound: false);
            ShowStepImmediate(0);
        }

        public void SetVisible(bool visible)
        {
            if (_rootCanvasGo != null)
                _rootCanvasGo.SetActive(visible);
        }

        private void Update()
        {
            if (_rootCanvasGo == null || !_rootCanvasGo.activeSelf || _db == null) return;

            _animTimer += Time.deltaTime;
            var spec = _db.GetSpecies(_selectedSpecies);

            // Animate Gen 5 B&W pet sprite on Top Screen & Dialogue Portrait at 3.2 FPS
            if (spec != null)
            {
                Sprite currentSprite = null;
                if (_storyStepIndex == 2 && spec.SleepSprite != null)
                {
                    currentSprite = spec.SleepSprite;
                }
                else if (spec.IdleSprites != null && spec.IdleSprites.Length > 0)
                {
                    int idx = Mathf.FloorToInt(_animTimer * 3.2f) % spec.IdleSprites.Length;
                    currentSprite = spec.IdleSprites[idx];
                }

                if (currentSprite != null)
                {
                    if (_topPetPreview != null && _topPetPreview.gameObject.activeSelf)
                        _topPetPreview.sprite = currentSprite;
                    if (_dialoguePortraitImg != null && _dialoguePortraitImg.gameObject.activeInHierarchy)
                        _dialoguePortraitImg.sprite = currentSprite;
                    if (_emailPetAvatarImg != null && _emailPetAvatarImg.gameObject.activeInHierarchy)
                        _emailPetAvatarImg.sprite = currentSprite;
                }
            }

            // Gentle floating emote bob above pet on Top Screen
            if (_topEmoteBubbleBg != null && _topEmoteBubbleBg.gameObject.activeSelf && _topPetPreview != null)
            {
                Vector2 petPos = _topPetPreview.rectTransform.anchoredPosition;
                float bob = Mathf.Sin(_animTimer * 4.2f) * 3f;
                _topEmoteBubbleBg.rectTransform.anchoredPosition = new Vector2(petPos.x + 36f, petPos.y + 52f + bob);
            }

            // Blinking Pokémon dialogue advance arrow ▼
            if (_blinkingAdvanceArrow != null && _blinkingAdvanceArrow.gameObject.activeInHierarchy)
            {
                float alpha = (Mathf.Sin(_animTimer * 6.5f) > -0.2f) ? 1f : 0.15f;
                var c = _blinkingAdvanceArrow.color;
                c.a = alpha;
                _blinkingAdvanceArrow.color = c;
            }

            UpdateWeatherParticles(Time.deltaTime);
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
            scaler.referenceResolution = new Vector2(540f, 810f);
            scaler.matchWidthOrHeight = 0.5f;

            // Nintendo DS Lite / DSi Console Outer Shell
            var shell = DSPixelUIBuilder.CreatePanel(
                _rootCanvasGo.transform, "DS_Shell",
                new Color(0.10f, 0.11f, 0.17f, 1f),
                DSPixelUIBuilder.DsBorderSlate,
                Vector2.zero, new Vector2(528f, 796f));

            // Speaker grilles on top shell
            var speakers = DSPixelUIBuilder.CreateText(
                shell, "Speakers", "• • • • • •               NINTENDO DS DUAL-SCREEN               • • • • • •", 10,
                new Color(0.45f, 0.50f, 0.65f, 0.75f), TextAnchor.MiddleCenter);
            speakers.rectTransform.anchoredPosition = new Vector2(0f, 384f);
            speakers.rectTransform.sizeDelta = new Vector2(490f, 16f);

            // --- TOP SCREEN (256x192 scaled 2x -> 500x374) ---
            var topBezel = DSPixelUIBuilder.CreatePanel(
                shell, "TopScreen_Bezel",
                new Color(0.04f, 0.04f, 0.08f, 1f), DSPixelUIBuilder.DsBorderGold,
                new Vector2(0f, 190f), new Vector2(500f, 376f));

            var artGo = new GameObject("TopArt", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            artGo.transform.SetParent(topBezel, false);
            var artRt = artGo.GetComponent<RectTransform>();
            artRt.anchorMin = Vector2.zero;
            artRt.anchorMax = Vector2.one;
            artRt.offsetMin = new Vector2(6f, 6f);
            artRt.offsetMax = new Vector2(-6f, -6f);
            _topScreenArt = artGo.GetComponent<Image>();
            _topScreenArt.sprite = DSPixelUIBuilder.GetWhiteSprite();

            // Live Cutscene Atmospheric Particle Layer inside Top Screen
            var weatherGo = new GameObject("WeatherLayer", typeof(RectTransform));
            weatherGo.transform.SetParent(artRt, false);
            _weatherContainer = weatherGo.GetComponent<RectTransform>();
            StretchFull(_weatherContainer, 0f);
            CreateWeatherParticlePool();

            // Pet sprite standing in the memory clearing (128x128 = exact 2x of 64x64 Gen 5 sprite!)
            var petGo = new GameObject("PetSceneSprite", typeof(RectTransform), typeof(Image));
            petGo.transform.SetParent(artRt, false);
            var petRt = petGo.GetComponent<RectTransform>();
            petRt.anchoredPosition = new Vector2(12f, -66f);
            petRt.sizeDelta = new Vector2(128f, 128f);
            _topPetPreview = petGo.GetComponent<Image>();
            _topPetPreview.raycastTarget = false;

            // Cute Pixel Emote Bubble above pet (Stardew Valley / Pokémon HGSS follower style!)
            var emotePanel = DSPixelUIBuilder.CreatePanel(
                artRt, "EmoteBubble",
                new Color(0.98f, 0.96f, 0.92f, 0.96f), DSPixelUIBuilder.DsBorderGold,
                new Vector2(48f, -14f), new Vector2(34f, 28f));
            _topEmoteBubbleBg = emotePanel.GetComponent<Image>();
            _topEmoteText = DSPixelUIBuilder.CreateText(
                emotePanel, "EmoteTxt", "♥", 15,
                new Color(0.92f, 0.28f, 0.42f, 1f), TextAnchor.MiddleCenter);
            StretchFull(_topEmoteText.rectTransform, 2f);

            // Top RPG Chapter Location Banner (Pokémon Emerald / Mystery Dungeon style)
            var topBanner = DSPixelUIBuilder.CreatePanel(
                artRt, "TitleBanner",
                new Color(0.06f, 0.08f, 0.15f, 0.90f), DSPixelUIBuilder.DsBorderGold,
                new Vector2(0f, 152f), new Vector2(468f, 38f));
            _topCaptionText = DSPixelUIBuilder.CreateText(
                topBanner, "BannerText",
                "❖ CAPÍTULO I — MANHÃ DE PRIMAVERA", 14,
                DSPixelUIBuilder.DsBorderGold, TextAnchor.MiddleCenter);
            _topCaptionText.fontStyle = FontStyle.Bold;
            StretchFull(_topCaptionText.rectTransform, 6f);

            // Chapter Cross-Fade Overlay inside Top Screen
            var fadeGo = new GameObject("TopChapterFade", typeof(RectTransform), typeof(Image));
            fadeGo.transform.SetParent(artRt, false);
            StretchFull(fadeGo.GetComponent<RectTransform>(), 0f);
            _topChapterFade = fadeGo.GetComponent<Image>();
            _topChapterFade.sprite = DSPixelUIBuilder.GetWhiteSprite();
            _topChapterFade.color = new Color(0.04f, 0.04f, 0.08f, 0f);
            _topChapterFade.raycastTarget = false;

            // DS Center Hinge + Dual Status LEDs
            var hinge = DSPixelUIBuilder.CreatePanel(
                shell, "DS_Hinge",
                new Color(0.06f, 0.07f, 0.11f, 1f), DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, -2f), new Vector2(512f, 22f));
            var hingeTitle = DSPixelUIBuilder.CreateText(
                hinge, "HingeTitle", "  ◆ TASK BAR: DUNGEON ABYSS", 11,
                DSPixelUIBuilder.DsTextMuted, TextAnchor.MiddleLeft);
            StretchFull(hingeTitle.rectTransform, 8f);
            var led = DSPixelUIBuilder.CreateText(
                hinge, "LED", "● WIRELESS   ● POWER  ", 11,
                DSPixelUIBuilder.DsAccentGreen, TextAnchor.MiddleRight);
            StretchFull(led.rectTransform, 8f);

            // --- BOTTOM TOUCH SCREEN (500x376) ---
            var bottomBezel = DSPixelUIBuilder.CreatePanel(
                shell, "BottomTouchScreen",
                DSPixelUIBuilder.DsBgDark, DSPixelUIBuilder.DsAccentCyan,
                new Vector2(0f, -196f), new Vector2(500f, 376f));

            BuildStep0AdoptPanel(bottomBezel);
            BuildStepStoryPanel(bottomBezel);
            BuildStepEmailPanel(bottomBezel);

            // Fast Skip Button on top-right bezel
            DSPixelUIBuilder.CreateButton(
                shell, "SkipIntroBtn", "Pular Prólogo »",
                new Vector2(194f, 356f), new Vector2(114f, 26f),
                new Color(0.18f, 0.20f, 0.32f, 0.92f), 11,
                () =>
                {
                    _chosenName = _nameInput != null && !string.IsNullOrWhiteSpace(_nameInput.text)
                        ? _nameInput.text.Trim()
                        : _chosenName;
                    FinishIntroTransition();
                });

            // White/Cyan Awakening Flash Overlay
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
                DSPixelUIBuilder.DsPanelMid, DSPixelUIBuilder.DsBorderGold,
                Vector2.zero, new Vector2(484f, 360f));

            var headerBanner = DSPixelUIBuilder.CreatePanel(
                _stepAdoptPanel, "PromptHeader",
                new Color(0.08f, 0.10f, 0.18f, 1f), DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, 148f), new Vector2(456f, 36f));

            var title = DSPixelUIBuilder.CreateText(
                headerBanner, "Prompt",
                "★ ESCOLHA SEU COMPANHEIRO DE INFÂNCIA ★", 14,
                DSPixelUIBuilder.DsBorderGold, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            StretchFull(title.rectTransform, 4f);

            // 3 Visual Pokémon-DS Starter Cards with Gen 5 Sprite Portraits!
            BuildStarterCard(0, PetSpeciesId.Dog, "Cãozinho", "Guardião Leal", new Vector2(-150f, 64f), new Color(0.36f, 0.22f, 0.14f, 1f));
            BuildStarterCard(1, PetSpeciesId.Cat, "Gatinho", "Sombra Veloz", new Vector2(0f, 64f), new Color(0.24f, 0.18f, 0.38f, 1f));
            BuildStarterCard(2, PetSpeciesId.SpiritGolem, "Golem Rúnico", "Colosso Antigo", new Vector2(150f, 64f), new Color(0.14f, 0.32f, 0.34f, 1f));

            // Species RPG Lore & Stat Badge Box
            var loreBox = DSPixelUIBuilder.CreatePanel(
                _stepAdoptPanel, "LoreBox",
                new Color(0.08f, 0.09f, 0.15f, 1f), DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, -18f), new Vector2(456f, 58f));

            _speciesDescText = DSPixelUIBuilder.CreateText(
                loreBox, "SpeciesDesc", "", 12,
                DSPixelUIBuilder.DsTextCream, TextAnchor.UpperCenter);
            _speciesDescText.rectTransform.anchoredPosition = new Vector2(0f, 9f);
            _speciesDescText.rectTransform.sizeDelta = new Vector2(436f, 32f);

            _speciesStatsBadgeText = DSPixelUIBuilder.CreateText(
                loreBox, "StatsBadge", "", 11,
                DSPixelUIBuilder.DsAccentCyan, TextAnchor.LowerCenter);
            _speciesStatsBadgeText.fontStyle = FontStyle.Bold;
            _speciesStatsBadgeText.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            _speciesStatsBadgeText.rectTransform.sizeDelta = new Vector2(436f, 20f);

            // Name Entry Row (Pokémon NDS Naming Screen style)
            var nameLbl = DSPixelUIBuilder.CreateText(
                _stepAdoptPanel, "NameLbl", "NOME DO PET:", 13,
                DSPixelUIBuilder.DsBorderGold, TextAnchor.MiddleRight);
            nameLbl.fontStyle = FontStyle.Bold;
            nameLbl.rectTransform.anchoredPosition = new Vector2(-118f, -74f);
            nameLbl.rectTransform.sizeDelta = new Vector2(140f, 32f);

            var inputBg = DSPixelUIBuilder.CreatePanel(
                _stepAdoptPanel, "NameInputBg",
                new Color(0.05f, 0.06f, 0.10f, 1f), DSPixelUIBuilder.DsAccentCyan,
                new Vector2(54f, -74f), new Vector2(220f, 38f));

            var inputText = DSPixelUIBuilder.CreateText(
                inputBg, "Text", "Pipoca", 16,
                DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleCenter);
            inputText.fontStyle = FontStyle.Bold;
            StretchFull(inputText.rectTransform, 6f);

            var placeholder = DSPixelUIBuilder.CreateText(
                inputBg, "Placeholder", "Digite o nome...", 14,
                new Color(0.5f, 0.5f, 0.6f, 0.7f), TextAnchor.MiddleCenter);
            StretchFull(placeholder.rectTransform, 6f);

            _nameInput = inputBg.gameObject.AddComponent<InputField>();
            _nameInput.textComponent = inputText;
            _nameInput.placeholder = placeholder;
            _nameInput.characterLimit = 14;
            _nameInput.text = "Pipoca";

            DSPixelUIBuilder.CreateButton(
                _stepAdoptPanel, "StartStoryBtn", "► DESPERTAR MEMÓRIAS DE INFÂNCIA",
                new Vector2(0f, -134f), new Vector2(360f, 48f),
                new Color(0.16f, 0.48f, 0.28f, 1f), 15,
                () =>
                {
                    _audio?.PlayUiTap();
                    _chosenName = string.IsNullOrWhiteSpace(_nameInput.text) ? _chosenName : _nameInput.text.Trim();
                    TransitionToStep(1);
                });
        }

        private void BuildStarterCard(int index, PetSpeciesId species, string title, string subtitle, Vector2 pos, Color cardBg)
        {
            var cardRt = DSPixelUIBuilder.CreatePanel(
                _stepAdoptPanel, $"StarterCard_{species}",
                cardBg, DSPixelUIBuilder.DsBorderSlate,
                pos, new Vector2(144f, 112f));

            _starterCardFrames[index] = cardRt.GetComponent<Image>();

            var btn = cardRt.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => SelectSpecies(species, true));

            // Portrait Mugshot Inner Box
            var portraitBox = DSPixelUIBuilder.CreatePanel(
                cardRt, "PortraitBox",
                new Color(0.06f, 0.07f, 0.12f, 1f), DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, 16f), new Vector2(64f, 64f));

            var pGo = new GameObject("PortraitSprite", typeof(RectTransform), typeof(Image));
            pGo.transform.SetParent(portraitBox, false);
            var pRt = pGo.GetComponent<RectTransform>();
            pRt.sizeDelta = new Vector2(56f, 56f);
            var pImg = pGo.GetComponent<Image>();
            pImg.raycastTarget = false;
            _starterCardPortraits[index] = pImg;

            var nameTxt = DSPixelUIBuilder.CreateText(
                cardRt, "Title", title, 13,
                DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleCenter);
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            nameTxt.rectTransform.sizeDelta = new Vector2(134f, 20f);

            var subTxt = DSPixelUIBuilder.CreateText(
                cardRt, "Subtitle", subtitle, 10,
                DSPixelUIBuilder.DsBorderGold, TextAnchor.MiddleCenter);
            subTxt.rectTransform.anchoredPosition = new Vector2(0f, -43f);
            subTxt.rectTransform.sizeDelta = new Vector2(134f, 16f);
        }

        private void PopulateStarterCardPortraits()
        {
            if (_db == null) return;
            PetSpeciesId[] ids = { PetSpeciesId.Dog, PetSpeciesId.Cat, PetSpeciesId.SpiritGolem };
            for (int i = 0; i < ids.Length; i++)
            {
                var spec = _db.GetSpecies(ids[i]);
                if (spec != null && _starterCardPortraits[i] != null)
                {
                    Sprite spr = (spec.IdleSprites != null && spec.IdleSprites.Length > 0)
                        ? spec.IdleSprites[0]
                        : null;
                    if (spr != null)
                        _starterCardPortraits[i].sprite = spr;
                }
            }
        }

        private void BuildStepStoryPanel(RectTransform parent)
        {
            _stepStoryPanel = DSPixelUIBuilder.CreatePanel(
                parent, "Step_Story",
                DSPixelUIBuilder.DsPanelMid, DSPixelUIBuilder.DsBorderGold,
                Vector2.zero, new Vector2(484f, 360f));

            // Pokémon Mystery Dungeon: Explorers of Sky style Dialogue Box + Speaker Tag + 76x76 Portrait Mugshot!
            var speakerBadge = DSPixelUIBuilder.CreatePanel(
                _stepStoryPanel, "SpeakerBadge",
                new Color(0.10f, 0.14f, 0.26f, 1f), DSPixelUIBuilder.DsBorderGold,
                new Vector2(-114f, 136f), new Vector2(228f, 32f));

            _speakerNameTagText = DSPixelUIBuilder.CreateText(
                speakerBadge, "SpeakerTxt", "DIÁRIO DE MEMÓRIAS", 13,
                DSPixelUIBuilder.DsBorderGold, TextAnchor.MiddleCenter);
            _speakerNameTagText.fontStyle = FontStyle.Bold;
            StretchFull(_speakerNameTagText.rectTransform, 4f);

            // Main Beveled RPG Dialogue Frame
            var dialogueFrame = DSPixelUIBuilder.CreatePanel(
                _stepStoryPanel, "PokeDialogueFrame",
                new Color(0.06f, 0.08f, 0.14f, 1f), DSPixelUIBuilder.DsAccentCyan,
                new Vector2(0f, 18f), new Vector2(456f, 206f));

            // Left Portrait Mugshot Frame (PMD 56x56 scaled to 88x88)
            var mugshotFrame = DSPixelUIBuilder.CreatePanel(
                dialogueFrame, "PortraitMugshotFrame",
                new Color(0.12f, 0.14f, 0.24f, 1f), DSPixelUIBuilder.DsBorderGold,
                new Vector2(-164f, 0f), new Vector2(96f, 96f));

            var mugGo = new GameObject("MugshotSprite", typeof(RectTransform), typeof(Image));
            mugGo.transform.SetParent(mugshotFrame, false);
            mugGo.GetComponent<RectTransform>().sizeDelta = new Vector2(82f, 82f);
            _dialoguePortraitImg = mugGo.GetComponent<Image>();
            _dialoguePortraitImg.raycastTarget = false;

            // Dialogue Body Text on the right of the portrait
            _storyBodyText = DSPixelUIBuilder.CreateText(
                dialogueFrame, "StoryBody", "", 15,
                DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleLeft);
            _storyBodyText.rectTransform.anchoredPosition = new Vector2(48f, 4f);
            _storyBodyText.rectTransform.sizeDelta = new Vector2(298f, 172f);

            // Blinking Golden RPG Advance Arrow ▼
            _blinkingAdvanceArrow = DSPixelUIBuilder.CreateText(
                dialogueFrame, "BlinkArrow", "▼", 16,
                DSPixelUIBuilder.DsBorderGold, TextAnchor.LowerRight);
            _blinkingAdvanceArrow.rectTransform.anchoredPosition = new Vector2(198f, -74f);
            _blinkingAdvanceArrow.rectTransform.sizeDelta = new Vector2(32f, 32f);

            DSPixelUIBuilder.CreateButton(
                _stepStoryPanel, "NextSlideBtn", "AVANÇAR MEMÓRIA (CLIQUE / TOQUE) ►",
                new Vector2(0f, -126f), new Vector2(346f, 48f),
                new Color(0.20f, 0.34f, 0.56f, 1f), 14,
                () =>
                {
                    _audio?.PlayUiTap();
                    TransitionToStep(_storyStepIndex + 1);
                });
        }

        private void BuildStepEmailPanel(RectTransform parent)
        {
            _stepEmailPanel = DSPixelUIBuilder.CreatePanel(
                parent, "Step_Email",
                new Color(0.08f, 0.11f, 0.18f, 1f), DSPixelUIBuilder.DsAccentCyan,
                Vector2.zero, new Vector2(484f, 360f));

            // Retro Desktop OS Window Titlebar
            var osTitleBar = DSPixelUIBuilder.CreatePanel(
                _stepEmailPanel, "OSTitleBar",
                new Color(0.14f, 0.22f, 0.38f, 1f), DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, 150f), new Vector2(458f, 32f));

            var osTitleTxt = DSPixelUIBuilder.CreateText(
                osTitleBar, "OSTitleTxt", "  ✉ ABYSS-MAIL OS v1.4 — 1 MENSAGEM NÃO LIDA", 12,
                DSPixelUIBuilder.DsAccentCyan, TextAnchor.MiddleLeft);
            osTitleTxt.fontStyle = FontStyle.Bold;
            StretchFull(osTitleTxt.rectTransform, 6f);

            // Sender Header Row with Pet Avatar
            var headerBox = DSPixelUIBuilder.CreatePanel(
                _stepEmailPanel, "HeaderBox",
                new Color(0.06f, 0.08f, 0.14f, 1f), DSPixelUIBuilder.DsBorderGold,
                new Vector2(0f, 94f), new Vector2(458f, 68f));

            var avatarFrame = DSPixelUIBuilder.CreatePanel(
                headerBox, "AvatarFrame",
                new Color(0.12f, 0.14f, 0.24f, 1f), DSPixelUIBuilder.DsAccentCyan,
                new Vector2(-188f, 0f), new Vector2(54f, 54f));

            var avGo = new GameObject("AvatarSprite", typeof(RectTransform), typeof(Image));
            avGo.transform.SetParent(avatarFrame, false);
            avGo.GetComponent<RectTransform>().sizeDelta = new Vector2(46f, 46f);
            _emailPetAvatarImg = avGo.GetComponent<Image>();

            _emailHeaderText = DSPixelUIBuilder.CreateText(
                headerBox, "EmailHeader", "", 13,
                DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleLeft);
            _emailHeaderText.rectTransform.anchoredPosition = new Vector2(32f, 0f);
            _emailHeaderText.rectTransform.sizeDelta = new Vector2(366f, 56f);

            var mailBox = DSPixelUIBuilder.CreatePanel(
                _stepEmailPanel, "MailBodyBox",
                new Color(0.04f, 0.06f, 0.11f, 1f), DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, -2f), new Vector2(458f, 112f));

            _emailBodyText = DSPixelUIBuilder.CreateText(
                mailBox, "MailBody", "", 14,
                DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleLeft);
            StretchFull(_emailBodyText.rectTransform, 14f);

            var launchBtn = DSPixelUIBuilder.CreateButton(
                _stepEmailPanel, "LaunchAttachmentBtn", "EXECUTAR ANEXO",
                new Vector2(0f, -116f), new Vector2(396f, 54f),
                DSPixelUIBuilder.DsAccentWarm, 15,
                () => StartCoroutine(CoPlayAwakeningSequence()));
            _exeButtonLabel = launchBtn.GetComponentInChildren<Text>();
        }

        private void SelectSpecies(PetSpeciesId id, bool playSound)
        {
            _selectedSpecies = id;
            if (playSound) _audio?.PlayUiTap();

            string defaultName;
            string desc;
            string statsBadge;
            switch (id)
            {
                case PetSpeciesId.Dog:
                    defaultName = "Pipoca";
                    desc = "Filhote guardião de pelagem dourada e lenço vermelho. Leal, valente e sempre pronto para proteger você.";
                    statsBadge = "ATRIBUTOS INICIAIS:  FOR ★★★ (3)   |   AGI ★★☆ (2)   |   VIT ★★★ (3)";
                    break;
                case PetSpeciesId.Cat:
                    defaultName = "Mimi";
                    desc = "Felino místico da meia-noite com olhos esmeralda e guizo lunar. Ágil, curioso e mestre em esquivas.";
                    statsBadge = "ATRIBUTOS INICIAIS:  FOR ★★☆ (2)   |   AGI ★★★★ (4)   |   VIT ★★☆ (2)";
                    break;
                default:
                    defaultName = "Rocco";
                    desc = "Pequeno guardião ancestral de jade e musgo com núcleo espiral luminoso. Inabalável e protetor.";
                    statsBadge = "ATRIBUTOS INICIAIS:  FOR ★★★ (3)   |   AGI ★☆☆ (1)   |   VIT ★★★★ (4)";
                    break;
            }

            if (_speciesDescText != null) _speciesDescText.text = desc;
            if (_speciesStatsBadgeText != null) _speciesStatsBadgeText.text = statsBadge;

            // Highlight selected Starter Card frame
            PetSpeciesId[] ids = { PetSpeciesId.Dog, PetSpeciesId.Cat, PetSpeciesId.SpiritGolem };
            for (int i = 0; i < ids.Length; i++)
            {
                if (_starterCardFrames[i] == null) continue;
                bool sel = (ids[i] == id);
                _starterCardFrames[i].sprite = DSPixelUIBuilder.GetDsWindowSprite(
                    sel ? DSPixelUIBuilder.DsBorderGold : DSPixelUIBuilder.DsBorderSlate);
                _starterCardFrames[i].transform.localScale = sel ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one;
            }

            if (_nameInput != null && (_nameInput.text == "Pipoca" || _nameInput.text == "Mimi" || _nameInput.text == "Rocco"))
            {
                _nameInput.text = defaultName;
            }
            _chosenName = _nameInput != null ? _nameInput.text : defaultName;
        }

        private void TransitionToStep(int nextStep)
        {
            if (_stepTransitionRoutine != null)
                StopCoroutine(_stepTransitionRoutine);
            _stepTransitionRoutine = StartCoroutine(CoTransitionStep(nextStep));
        }

        private IEnumerator CoTransitionStep(int nextStep)
        {
            if (_topChapterFade != null)
            {
                float t = 0f;
                while (t < 0.16f)
                {
                    t += Time.deltaTime;
                    _topChapterFade.color = new Color(0.04f, 0.04f, 0.08f, Mathf.Clamp01(t / 0.16f));
                    yield return null;
                }
            }

            ShowStepImmediate(nextStep);

            if (_topChapterFade != null)
            {
                float t = 0f;
                while (t < 0.22f)
                {
                    t += Time.deltaTime;
                    _topChapterFade.color = new Color(0.04f, 0.04f, 0.08f, 1f - Mathf.Clamp01(t / 0.22f));
                    yield return null;
                }
                _topChapterFade.color = new Color(0.04f, 0.04f, 0.08f, 0f);
            }
        }

        private void ShowStepImmediate(int step)
        {
            _storyStepIndex = step;
            _stepAdoptPanel.gameObject.SetActive(step == 0);
            _stepStoryPanel.gameObject.SetActive(step == 1 || step == 2);
            _stepEmailPanel.gameObject.SetActive(step == 3);

            ConfigureWeatherForStep(step);

            if (step == 0)
            {
                if (_db != null && _db.IntroAdoptSprite != null) _topScreenArt.sprite = _db.IntroAdoptSprite;
                _topPetPreview.gameObject.SetActive(true);
                _topPetPreview.rectTransform.anchoredPosition = new Vector2(18f, -66f);
                _topEmoteBubbleBg.gameObject.SetActive(true);
                _topEmoteText.text = "♥";
                _topEmoteText.color = new Color(0.95f, 0.28f, 0.44f, 1f);
                _topCaptionText.text = "❖ CAPÍTULO I — MANHÃ DE PRIMAVERA NA VILA";
            }
            else if (step == 1)
            {
                if (_db != null && _db.IntroGrowingSprite != null) _topScreenArt.sprite = _db.IntroGrowingSprite;
                _topPetPreview.gameObject.SetActive(true);
                _topPetPreview.rectTransform.anchoredPosition = new Vector2(4f, -72f);
                _topEmoteBubbleBg.gameObject.SetActive(true);
                _topEmoteText.text = "♪";
                _topEmoteText.color = new Color(0.95f, 0.62f, 0.18f, 1f);
                _topCaptionText.text = $"❖ CAPÍTULO II — OUTONO DOURADO COM {_chosenName.ToUpperInvariant()}";
                _speakerNameTagText.text = $"MEMÓRIAS DE {_chosenName.ToUpperInvariant()}";
                PlayTypewriter(
                    $"As estações passaram e você cresceu ao lado de {_chosenName}.\n\n" +
                    "Nas tardes douradas de outono perto da ponte velha, não importava o cansaço do dia: seu companheiro estava sempre ali, esperando um carinho.");
            }
            else if (step == 2)
            {
                if (_db != null && _db.IntroFarewellSprite != null) _topScreenArt.sprite = _db.IntroFarewellSprite;
                _topPetPreview.gameObject.SetActive(true);
                _topPetPreview.rectTransform.anchoredPosition = new Vector2(14f, -74f);
                _topEmoteBubbleBg.gameObject.SetActive(true);
                _topEmoteText.text = "Zzz";
                _topEmoteText.color = DSPixelUIBuilder.DsAccentCyan;
                _topCaptionText.text = "❖ CAPÍTULO III — A COLINA DAS ESTRELAS";
                _speakerNameTagText.text = "DESPEDIDA SILENCIOSA";
                PlayTypewriter(
                    $"Os anos voaram. {_chosenName} ficou velhinho e, numa noite serena sob as flores lunares, fechou os olhos para descansar em paz.\n\n" +
                    "A barra inferior do seu computador ficou silenciosa e vazia...");
            }
            else if (step == 3)
            {
                _audio?.PlayEmailChime();
                if (_db != null && _db.IntroPcEmailSprite != null) _topScreenArt.sprite = _db.IntroPcEmailSprite;
                _topPetPreview.gameObject.SetActive(false);
                _topEmoteBubbleBg.gameObject.SetActive(false);
                _topCaptionText.text = "❖ CAPÍTULO IV — 02:14 AM (1 NOVO E-MAIL)";

                string slug = _chosenName.ToLowerInvariant().Replace(" ", "_");
                _emailHeaderText.text =
                    $"De: <color=#52DBFF>{slug}@dungeon.abyss</color>\n" +
                    $"Para: Voce (Desktop Principal)\n" +
                    $"Assunto: <color=#FAD64E>está escuro aqui embaixo... você tá aí?</color>";

                _emailBodyText.text =
                    $"\"Ei... acordei numa masmorra antiga bem embaixo da sua Barra de Tarefas!\n" +
                    $"Ouvi o clique do seu mouse lá em cima. Tem fogueira, bonecos de treino e portais que mudam sozinhos...\n" +
                    $"Abre o anexo pra cuidar de mim enquanto você usa o PC!\"";

                _exeButtonLabel.text = $"[ ▶ EXECUTAR {_chosenName}_Taskbar.exe ]";
            }
        }

        private void CreateWeatherParticlePool()
        {
            _particles.Clear();
            for (int i = 0; i < 16; i++)
            {
                var go = new GameObject($"Particle_{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_weatherContainer, false);
                var rt = go.GetComponent<RectTransform>();
                var img = go.GetComponent<Image>();
                img.sprite = DSPixelUIBuilder.GetWhiteSprite();
                img.raycastTarget = false;
                _particles.Add(new WeatherParticle
                {
                    Rt = rt,
                    Img = img,
                    Velocity = Vector2.zero,
                    Phase = i * 0.7f
                });
            }
        }

        private void ConfigureWeatherForStep(int step)
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                float rx = UnityEngine.Random.Range(-220f, 220f);
                float ry = UnityEngine.Random.Range(-150f, 150f);
                p.Rt.anchoredPosition = new Vector2(rx, ry);

                if (step == 0)
                {
                    // Spring cherry petals & sunlit pollen motes
                    p.Rt.sizeDelta = (i % 2 == 0) ? new Vector2(6f, 4f) : new Vector2(4f, 4f);
                    p.Img.color = (i % 2 == 0)
                        ? new Color(1f, 0.78f, 0.86f, 0.85f)
                        : new Color(1f, 0.96f, 0.68f, 0.80f);
                    p.Velocity = new Vector2(18f, -22f);
                }
                else if (step == 1)
                {
                    // Golden & crimson falling autumn leaves
                    p.Rt.sizeDelta = new Vector2(7f, 5f);
                    p.Img.color = (i % 3 == 0)
                        ? new Color(0.95f, 0.42f, 0.20f, 0.90f)
                        : new Color(0.98f, 0.74f, 0.24f, 0.90f);
                    p.Velocity = new Vector2(-28f, -32f);
                }
                else if (step == 2)
                {
                    // Rising cyan & gold spirit fireflies on the starry hilltop
                    p.Rt.sizeDelta = new Vector2(5f, 5f);
                    p.Img.color = (i % 3 == 0)
                        ? new Color(1f, 0.92f, 0.52f, 0.90f)
                        : new Color(0.42f, 0.95f, 1f, 0.90f);
                    p.Velocity = new Vector2(6f, 24f);
                }
                else
                {
                    // Subtle dust/rain motes in bedroom
                    p.Rt.sizeDelta = new Vector2(3f, 3f);
                    p.Img.color = new Color(0.55f, 0.80f, 1f, 0.30f);
                    p.Velocity = new Vector2(-8f, -14f);
                }

                _particles[i] = p;
            }
        }

        private void UpdateWeatherParticles(float dt)
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                if (p.Rt == null) continue;

                Vector2 pos = p.Rt.anchoredPosition;
                float sway = Mathf.Sin(_animTimer * 2.4f + p.Phase) * 14f;
                pos.x += (p.Velocity.x + sway) * dt;
                pos.y += p.Velocity.y * dt;

                if (pos.y < -175f) pos.y = 165f;
                if (pos.y > 175f) pos.y = -165f;
                if (pos.x < -235f) pos.x = 235f;
                if (pos.x > 235f) pos.x = -235f;

                p.Rt.anchoredPosition = pos;
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
                yield return new WaitForSeconds(0.016f);
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
