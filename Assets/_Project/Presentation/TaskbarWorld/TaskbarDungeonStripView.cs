using System;
using System.Collections.Generic;
using TBDA.Data;
using TBDA.Domain.DungeonMap;
using TBDA.Domain.Events;
using TBDA.Domain.Pets;
using TBDA.Domain.Session;
using TBDA.Platform;
using TBDA.Presentation.Audio;
using TBDA.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TBDA.Presentation.TaskbarWorld
{
    /// <summary>
    /// Renders the interactive Dungeon Taskbar Strip (Nintendo DS Touch Screen) at the bottom
    /// and the collapsible Nintendo DS Top-Screen Popup (Tamagotchi Care, Punch Club Training, Shifting Mini-Map) above it.
    /// </summary>
    public sealed class TaskbarDungeonStripView : MonoBehaviour
    {
        private enum TopPopupTab
        {
            None = 0,
            Care = 1,
            Training = 2,
            MiniMap = 3
        }

        private GameSessionService _session;
        private GameDatabaseSO _db;
        private DSChiptuneSynthesizer _audio;
        private Win32TaskbarOverlayController _windowController;
        private Action _onRequestResetRun;

        private GameObject _rootCanvasGo;
        private RectTransform _topPopupRoot;
        private RectTransform _careTabPanel;
        private RectTransform _trainTabPanel;
        private RectTransform _mapTabPanel;
        private TopPopupTab _currentTab = TopPopupTab.None;

        // Taskbar Dungeon World Visuals
        private RectTransform _petActorRt;
        private Image _petActorImg;
        private Image _campfireImg;
        private Text _petActivityTag;
        private RectTransform _floatingTextContainer;

        // Taskbar Mini HUD
        private Text _hudPetTitle;
        private Image _hudHungerBar;
        private Text _hudHungerTxt;
        private Image _hudAffectionBar;
        private Text _hudAffectionTxt;
        private Image _hudEnergyBar;
        private Text _hudEnergyTxt;
        private Text _hudStatsSummary;
        private Text _editorOverlayHint;

        // Care Tab UI
        private Text _careSummaryText;
        private readonly List<Text> _foodButtonLabels = new List<Text>();
        private readonly List<string> _foodIds = new List<string>();

        // Training Tab UI
        private Image _strSubBar;
        private Text _strSubTxt;
        private Image _agiSubBar;
        private Text _agiSubTxt;
        private Image _vitSubBar;
        private Text _vitSubTxt;
        private Text _trainActiveDesc;

        // Mini-Map Tab UI
        private RectTransform _mapGridContainer;
        private Text _mapHeaderText;
        private Text _mapLogText;
        private Image _expeditionBarFill;
        private Text _expeditionBarTxt;

        // Station X positions along the 920px-wide dungeon strip
        private const float CampfirePosX = -330f;
        private const float IdleLoungePosX = -175f;
        private const float StrStationPosX = -20f;
        private const float AgiStationPosX = 110f;
        private const float VitStationPosX = 240f;
        private const float PortalPosX = 375f;

        private float _currentPetPosX = -175f;
        private float _animClock;

        private sealed class FloatingToast
        {
            public RectTransform Rt;
            public Text Label;
            public float Remaining;
        }

        private readonly List<FloatingToast> _activeToasts = new List<FloatingToast>();

        public void Initialize(
            GameSessionService session,
            GameDatabaseSO db,
            DSChiptuneSynthesizer audio,
            Win32TaskbarOverlayController windowController,
            Action onRequestResetRun)
        {
            _session = session;
            _db = db;
            _audio = audio;
            _windowController = windowController;
            _onRequestResetRun = onRequestResetRun;

            BuildTaskbarCanvas();
            SubscribeDomainEvents();
            RefreshAllUI();
        }

        public void SetVisible(bool visible)
        {
            if (_rootCanvasGo != null)
                _rootCanvasGo.SetActive(visible);
        }

        private void SubscribeDomainEvents()
        {
            _session.EventBus.Subscribe<PetStateUpdatedEvent>(_ => RefreshAllUI());
            _session.EventBus.Subscribe<StatLevelUpEvent>(_ =>
            {
                _audio?.PlayLevelUp();
                RefreshAllUI();
            });
            _session.EventBus.Subscribe<MapMutatedEvent>(evt =>
            {
                SpawnFloatingText($"~ {evt.MutatedNodesCount} salas mudaram no mapa!", DSPixelUIBuilder.DsAccentCyan);
                if (_currentTab == TopPopupTab.MiniMap) RebuildMiniMapNodes();
            });
            _session.EventBus.Subscribe<ExpeditionCompletedEvent>(_ =>
            {
                _audio?.PlayUiTap();
                RefreshAllUI();
            });
            _session.EventBus.Subscribe<FloatingFeedbackEvent>(evt =>
            {
                Color c = DSPixelUIBuilder.DsTextCream;
                if (evt.Tone == FeedbackTone.Positive) c = DSPixelUIBuilder.DsAccentGreen;
                else if (evt.Tone == FeedbackTone.Warning) c = DSPixelUIBuilder.DsAccentWarm;
                else if (evt.Tone == FeedbackTone.StatLevelUp) c = DSPixelUIBuilder.DsBorderGold;
                else if (evt.Tone == FeedbackTone.Love)
                {
                    c = new Color(1f, 0.52f, 0.72f, 1f);
                    _audio?.PlayPetLove();
                }
                SpawnFloatingText(evt.Message, c);
            });
        }

        private void Update()
        {
            if (_rootCanvasGo == null || !_rootCanvasGo.activeSelf || _session == null) return;

            _animClock += Time.deltaTime;
            AnimateDungeonWorld(Time.deltaTime);
            UpdateFloatingToasts(Time.deltaTime);
            RefreshLiveBars();
        }

        private void BuildTaskbarCanvas()
        {
            _rootCanvasGo = new GameObject("TaskbarDungeonCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _rootCanvasGo.transform.SetParent(transform, false);

            var canvas = _rootCanvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = _rootCanvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 420f);
            scaler.matchWidthOrHeight = 1f; // Match height so 112px bottom strip stays crisp across ultrawide/16:9!

            // Editor Transparency Indicator (Top-Left, non-raycast so click-through testing works!)
            _editorOverlayHint = DSPixelUIBuilder.CreateText(
                _rootCanvasGo.transform, "EditorTransparencyBadge", "", 11,
                new Color(0.65f, 0.85f, 0.95f, 0.8f), TextAnchor.UpperLeft);
            var hintRt = _editorOverlayHint.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 1f);
            hintRt.anchorMax = new Vector2(0f, 1f);
            hintRt.pivot = new Vector2(0f, 1f);
            hintRt.anchoredPosition = new Vector2(10f, -6f);
            hintRt.sizeDelta = new Vector2(520f, 24f);

            BuildBottomTaskbarStrip(_rootCanvasGo.transform);
            BuildTopScreenPopupDrawer(_rootCanvasGo.transform);
        }

        private void BuildBottomTaskbarStrip(Transform root)
        {
            // Anchor full-width at bottom (height = 114px)
            var stripRoot = new GameObject("DungeonTaskbarStrip", typeof(RectTransform), typeof(Image), typeof(Outline));
            stripRoot.transform.SetParent(root, false);
            var srt = stripRoot.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = new Vector2(0f, 114f);

            var stripBg = stripRoot.GetComponent<Image>();
            if (_db != null && _db.DungeonStripBg != null)
            {
                stripBg.sprite = _db.DungeonStripBg;
                stripBg.type = Image.Type.Tiled;
            }
            else
            {
                stripBg.sprite = DSPixelUIBuilder.GetWhiteSprite();
                stripBg.color = new Color(0.16f, 0.15f, 0.24f, 1f);
            }
            var stripOutline = stripRoot.GetComponent<Outline>();
            stripOutline.effectColor = DSPixelUIBuilder.DsBorderGold;
            stripOutline.effectDistance = new Vector2(0f, 2f);

            // Centered Play Stage inside the strip (880px wide) so props sit cleanly on any monitor width
            var stage = new GameObject("DungeonStage", typeof(RectTransform));
            stage.transform.SetParent(srt, false);
            var stageRt = stage.GetComponent<RectTransform>();
            stageRt.anchorMin = new Vector2(0.5f, 0f);
            stageRt.anchorMax = new Vector2(0.5f, 0f);
            stageRt.pivot = new Vector2(0.5f, 0f);
            stageRt.anchoredPosition = new Vector2(0f, 34f);
            stageRt.sizeDelta = new Vector2(880f, 78f);

            // 1. Campfire Station (Rest)
            _campfireImg = CreateInteractiveWorldProp(
                stageRt, "Campfire", _db != null && _db.CampfireFrames != null && _db.CampfireFrames.Length > 0 ? _db.CampfireFrames[0] : null,
                new Vector2(CampfirePosX, 26f), "Fogueira (Dormir)",
                () =>
                {
                    _audio?.PlayUiTap();
                    _session.SetPetActivity(PetActivityState.RestingAtCampfire);
                });

            // 2. Punch Club Stations (FOR / AGI / VIT)
            Sprite strSpr = GetDrillSprite(CombatStatType.Strength);
            CreateInteractiveWorldProp(
                stageRt, "Station_STR", strSpr,
                new Vector2(StrStationPosX, 28f), "Treino FOR",
                () => HandleStationClicked(PetActivityState.TrainingStrength));

            Sprite agiSpr = GetDrillSprite(CombatStatType.Agility);
            CreateInteractiveWorldProp(
                stageRt, "Station_AGI", agiSpr,
                new Vector2(AgiStationPosX, 28f), "Treino AGI",
                () => HandleStationClicked(PetActivityState.TrainingAgility));

            Sprite vitSpr = GetDrillSprite(CombatStatType.Vitality);
            CreateInteractiveWorldProp(
                stageRt, "Station_VIT", vitSpr,
                new Vector2(VitStationPosX, 28f), "Treino VIT",
                () => HandleStationClicked(PetActivityState.TrainingVitality));

            // 3. Dungeon Mini-Map Gate
            CreateInteractiveWorldProp(
                stageRt, "DungeonPortal", _db != null ? _db.PortalGateSprite : null,
                new Vector2(PortalPosX, 30f), "Mapa Masmorra",
                () =>
                {
                    _audio?.PlayUiTap();
                    OpenTab(TopPopupTab.MiniMap);
                });

            // 4. Clickable Pet Actor on the Taskbar!
            var petGo = new GameObject("PetActor", typeof(RectTransform), typeof(Image), typeof(Button));
            petGo.transform.SetParent(stageRt, false);
            _petActorRt = petGo.GetComponent<RectTransform>();
            _petActorRt.pivot = new Vector2(0.5f, 0f);
            _petActorRt.anchoredPosition = new Vector2(IdleLoungePosX, 4f);
            _petActorRt.sizeDelta = new Vector2(64f, 64f);
            _petActorImg = petGo.GetComponent<Image>();
            _petActorImg.sprite = DSPixelUIBuilder.GetWhiteSprite();

            petGo.GetComponent<Button>().onClick.AddListener(() =>
            {
                _session.HandlePetStylusClicked();
            });

            _petActivityTag = DSPixelUIBuilder.CreateText(
                _petActorRt, "ActivityBubble", "", 10,
                DSPixelUIBuilder.DsBorderGold, TextAnchor.MiddleCenter);
            _petActivityTag.rectTransform.anchoredPosition = new Vector2(0f, 38f);
            _petActivityTag.rectTransform.sizeDelta = new Vector2(140f, 18f);

            // Floating Popups Layer
            var floatGo = new GameObject("FloatingPopups", typeof(RectTransform));
            floatGo.transform.SetParent(stageRt, false);
            _floatingTextContainer = floatGo.GetComponent<RectTransform>();
            _floatingTextContainer.anchoredPosition = Vector2.zero;
            _floatingTextContainer.sizeDelta = new Vector2(880f, 120f);

            // 5. Compact Bottom Control Bar (Height 34px at very bottom of taskbar strip)
            var hudBar = DSPixelUIBuilder.CreatePanel(
                srt, "CompactHudBar",
                new Color(0.07f, 0.07f, 0.12f, 0.98f), DSPixelUIBuilder.DsBorderSlate,
                Vector2.zero, new Vector2(0f, 34f));
            hudBar.anchorMin = new Vector2(0f, 0f);
            hudBar.anchorMax = new Vector2(1f, 0f);
            hudBar.pivot = new Vector2(0.5f, 0f);
            hudBar.anchoredPosition = Vector2.zero;
            hudBar.sizeDelta = new Vector2(0f, 34f);

            var hudInner = new GameObject("HudInner", typeof(RectTransform));
            hudInner.transform.SetParent(hudBar, false);
            var hirt = hudInner.GetComponent<RectTransform>();
            hirt.anchoredPosition = Vector2.zero;
            hirt.sizeDelta = new Vector2(940f, 34f);

            _hudPetTitle = DSPixelUIBuilder.CreateText(
                hirt, "PetTitle", "Pipoca", 12,
                DSPixelUIBuilder.DsBorderGold, TextAnchor.MiddleLeft);
            _hudPetTitle.rectTransform.anchoredPosition = new Vector2(-405f, 0f);
            _hudPetTitle.rectTransform.sizeDelta = new Vector2(118f, 28f);

            _hudHungerBar = DSPixelUIBuilder.CreateStatBar(
                hirt, "BarFome", new Vector2(-296f, 0f), new Vector2(82f, 18f),
                new Color(0.86f, 0.52f, 0.22f, 1f), out _hudHungerTxt);

            _hudAffectionBar = DSPixelUIBuilder.CreateStatBar(
                hirt, "BarAfeto", new Vector2(-206f, 0f), new Vector2(82f, 18f),
                new Color(0.88f, 0.36f, 0.56f, 1f), out _hudAffectionTxt);

            _hudEnergyBar = DSPixelUIBuilder.CreateStatBar(
                hirt, "BarEnergia", new Vector2(-116f, 0f), new Vector2(82f, 18f),
                new Color(0.26f, 0.68f, 0.88f, 1f), out _hudEnergyTxt);

            _hudStatsSummary = DSPixelUIBuilder.CreateText(
                hirt, "StatsTxt", "FOR 2 | AGI 2 | VIT 2 | ♦ 5", 11,
                DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleCenter);
            _hudStatsSummary.rectTransform.anchoredPosition = new Vector2(22f, 0f);
            _hudStatsSummary.rectTransform.sizeDelta = new Vector2(184f, 28f);

            // DS Drawer Toggle Buttons
            DSPixelUIBuilder.CreateButton(
                hirt, "BtnCare", "Comida",
                new Vector2(154f, 0f), new Vector2(68f, 24f),
                new Color(0.20f, 0.44f, 0.26f, 1f), 11,
                () => ToggleTab(TopPopupTab.Care));

            DSPixelUIBuilder.CreateButton(
                hirt, "BtnTrain", "Treino",
                new Vector2(226f, 0f), new Vector2(68f, 24f),
                new Color(0.46f, 0.26f, 0.22f, 1f), 11,
                () => ToggleTab(TopPopupTab.Training));

            DSPixelUIBuilder.CreateButton(
                hirt, "BtnMap", "Mapa",
                new Vector2(298f, 0f), new Vector2(68f, 24f),
                new Color(0.22f, 0.34f, 0.56f, 1f), 11,
                () => ToggleTab(TopPopupTab.MiniMap));

            DSPixelUIBuilder.CreateButton(
                hirt, "BtnWindowMode", "DS/Barra",
                new Vector2(374f, 0f), new Vector2(74f, 24f),
                new Color(0.28f, 0.28f, 0.42f, 1f), 10,
                ToggleWindowMode);

            DSPixelUIBuilder.CreateButton(
                hirt, "BtnReset", "Novo",
                new Vector2(442f, 0f), new Vector2(52f, 24f),
                new Color(0.35f, 0.18f, 0.22f, 1f), 10,
                () => _onRequestResetRun?.Invoke());
        }

        private Image CreateInteractiveWorldProp(
            RectTransform parent,
            string name,
            Sprite sprite,
            Vector2 pos,
            string caption,
            Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(56f, 56f);

            var img = go.GetComponent<Image>();
            img.sprite = sprite != null ? sprite : DSPixelUIBuilder.GetWhiteSprite();

            var lbl = DSPixelUIBuilder.CreateText(
                rt, "Caption", caption, 9,
                DSPixelUIBuilder.DsTextMuted, TextAnchor.MiddleCenter);
            lbl.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            lbl.rectTransform.sizeDelta = new Vector2(96f, 14f);

            go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            return img;
        }

        private void BuildTopScreenPopupDrawer(Transform root)
        {
            // Positioned right above the 114px Taskbar Strip (Nintendo DS Top-Screen 520x286)
            _topPopupRoot = DSPixelUIBuilder.CreatePanel(
                root, "DS_TopScreenDrawer",
                DSPixelUIBuilder.DsBgDark, DSPixelUIBuilder.DsBorderGold,
                new Vector2(0f, 260f), new Vector2(560f, 286f));
            _topPopupRoot.anchorMin = new Vector2(0.5f, 0f);
            _topPopupRoot.anchorMax = new Vector2(0.5f, 0f);
            _topPopupRoot.pivot = new Vector2(0.5f, 0.5f);

            // Top Tab Header inside Drawer
            DSPixelUIBuilder.CreateButton(
                _topPopupRoot, "TabBtn_Care", "1. Tamagotchi",
                new Vector2(-180f, 122f), new Vector2(132f, 28f),
                new Color(0.18f, 0.40f, 0.26f, 1f), 12,
                () => OpenTab(TopPopupTab.Care));

            DSPixelUIBuilder.CreateButton(
                _topPopupRoot, "TabBtn_Train", "2. Punch Club",
                new Vector2(-38f, 122f), new Vector2(132f, 28f),
                new Color(0.44f, 0.24f, 0.22f, 1f), 12,
                () => OpenTab(TopPopupTab.Training));

            DSPixelUIBuilder.CreateButton(
                _topPopupRoot, "TabBtn_Map", "3. Mini-Mapa",
                new Vector2(104f, 122f), new Vector2(132f, 28f),
                new Color(0.20f, 0.34f, 0.54f, 1f), 12,
                () => OpenTab(TopPopupTab.MiniMap));

            DSPixelUIBuilder.CreateButton(
                _topPopupRoot, "CloseDrawerBtn", "Fechar [X]",
                new Vector2(226f, 122f), new Vector2(88f, 28f),
                new Color(0.32f, 0.18f, 0.22f, 1f), 11,
                () => OpenTab(TopPopupTab.None));

            BuildCareTab(_topPopupRoot);
            BuildTrainingTab(_topPopupRoot);
            BuildMiniMapTab(_topPopupRoot);

            _topPopupRoot.gameObject.SetActive(false);
        }

        private void BuildCareTab(RectTransform parent)
        {
            _careTabPanel = DSPixelUIBuilder.CreatePanel(
                parent, "CareTab",
                DSPixelUIBuilder.DsPanelMid, DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, -16f), new Vector2(536f, 234f));

            _careSummaryText = DSPixelUIBuilder.CreateText(
                _careTabPanel, "CareSummary", "", 13,
                DSPixelUIBuilder.DsTextCream, TextAnchor.UpperLeft);
            _careSummaryText.rectTransform.anchoredPosition = new Vector2(-132f, 18f);
            _careSummaryText.rectTransform.sizeDelta = new Vector2(240f, 170f);

            var items = _session.GetAllCareItems();
            _foodButtonLabels.Clear();
            _foodIds.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string itemId = item.ItemId;
                _foodIds.Add(itemId);

                float y = 68f - i * 54f;
                var btn = DSPixelUIBuilder.CreateButton(
                    _careTabPanel, "Feed_" + itemId, item.DisplayName,
                    new Vector2(126f, y), new Vector2(252f, 44f),
                    new Color(0.20f, 0.34f, 0.26f, 1f), 12,
                    () =>
                    {
                        _audio?.PlayUiTap();
                        _session.FeedPetFromInventory(itemId);
                    });
                _foodButtonLabels.Add(btn.GetComponentInChildren<Text>());
            }

            DSPixelUIBuilder.CreateButton(
                _careTabPanel, "BtnPetLove", "Fazer Carinho ♥",
                new Vector2(-190f, -86f), new Vector2(128f, 34f),
                new Color(0.52f, 0.22f, 0.38f, 1f), 12,
                () => _session.HandlePetStylusClicked());

            DSPixelUIBuilder.CreateButton(
                _careTabPanel, "BtnSleep", "Dormir na Fogueira",
                new Vector2(-52f, -86f), new Vector2(134f, 34f),
                new Color(0.24f, 0.30f, 0.48f, 1f), 11,
                () =>
                {
                    _audio?.PlayUiTap();
                    _session.SetPetActivity(PetActivityState.RestingAtCampfire);
                });
        }

        private void BuildTrainingTab(RectTransform parent)
        {
            _trainTabPanel = DSPixelUIBuilder.CreatePanel(
                parent, "TrainingTab",
                DSPixelUIBuilder.DsPanelMid, DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, -16f), new Vector2(536f, 234f));

            _strSubBar = DSPixelUIBuilder.CreateStatBar(
                _trainTabPanel, "SubSTR", new Vector2(-95f, 74f), new Vector2(300f, 26f),
                new Color(0.84f, 0.34f, 0.30f, 1f), out _strSubTxt);

            DSPixelUIBuilder.CreateButton(
                _trainTabPanel, "AssignSTR", "Treinar FOR",
                new Vector2(158f, 74f), new Vector2(168f, 28f),
                new Color(0.50f, 0.22f, 0.20f, 1f), 12,
                () =>
                {
                    _audio?.PlayUiTap();
                    _session.SetPetActivity(PetActivityState.TrainingStrength);
                });

            _agiSubBar = DSPixelUIBuilder.CreateStatBar(
                _trainTabPanel, "SubAGI", new Vector2(-95f, 34f), new Vector2(300f, 26f),
                new Color(0.28f, 0.76f, 0.46f, 1f), out _agiSubTxt);

            DSPixelUIBuilder.CreateButton(
                _trainTabPanel, "AssignAGI", "Treinar AGI",
                new Vector2(158f, 34f), new Vector2(168f, 28f),
                new Color(0.18f, 0.44f, 0.28f, 1f), 12,
                () =>
                {
                    _audio?.PlayUiTap();
                    _session.SetPetActivity(PetActivityState.TrainingAgility);
                });

            _vitSubBar = DSPixelUIBuilder.CreateStatBar(
                _trainTabPanel, "SubVIT", new Vector2(-95f, -6f), new Vector2(300f, 26f),
                new Color(0.28f, 0.62f, 0.88f, 1f), out _vitSubTxt);

            DSPixelUIBuilder.CreateButton(
                _trainTabPanel, "AssignVIT", "Treinar VIT",
                new Vector2(158f, -6f), new Vector2(168f, 28f),
                new Color(0.20f, 0.34f, 0.54f, 1f), 12,
                () =>
                {
                    _audio?.PlayUiTap();
                    _session.SetPetActivity(PetActivityState.TrainingVitality);
                });

            _trainActiveDesc = DSPixelUIBuilder.CreateText(
                _trainTabPanel, "TrainDesc", "", 12,
                DSPixelUIBuilder.DsTextMuted, TextAnchor.MiddleLeft);
            _trainActiveDesc.rectTransform.anchoredPosition = new Vector2(-95f, -48f);
            _trainActiveDesc.rectTransform.sizeDelta = new Vector2(304f, 36f);

            // Cookie-Clicker Slow Focus Boost Button
            DSPixelUIBuilder.CreateButton(
                _trainTabPanel, "ManualClickBoost", "⚡ INCENTIVAR! (Clique)",
                new Vector2(158f, -68f), new Vector2(174f, 52f),
                DSPixelUIBuilder.DsAccentWarm, 13,
                () =>
                {
                    _audio?.PlayUiTap();
                    _session.HandleTrainingManualClick();
                });

            DSPixelUIBuilder.CreateButton(
                _trainTabPanel, "StopTrainBtn", "Pausar / Passear",
                new Vector2(-176f, -88f), new Vector2(140f, 28f),
                new Color(0.26f, 0.26f, 0.36f, 1f), 11,
                () =>
                {
                    _audio?.PlayUiTap();
                    _session.SetPetActivity(PetActivityState.Idle);
                });
        }

        private void BuildMiniMapTab(RectTransform parent)
        {
            _mapTabPanel = DSPixelUIBuilder.CreatePanel(
                parent, "MiniMapTab",
                DSPixelUIBuilder.DsPanelMid, DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, -16f), new Vector2(536f, 234f));

            _mapHeaderText = DSPixelUIBuilder.CreateText(
                _mapTabPanel, "MapHeader", "ATO 1: MASMORRA RASA — Andar 1", 13,
                DSPixelUIBuilder.DsBorderGold, TextAnchor.MiddleLeft);
            _mapHeaderText.rectTransform.anchoredPosition = new Vector2(-90f, 96f);
            _mapHeaderText.rectTransform.sizeDelta = new Vector2(330f, 24f);

            _expeditionBarFill = DSPixelUIBuilder.CreateStatBar(
                _mapTabPanel, "ExpProgress", new Vector2(168f, 96f), new Vector2(168f, 20f),
                DSPixelUIBuilder.DsAccentCyan, out _expeditionBarTxt);

            var gridBg = DSPixelUIBuilder.CreatePanel(
                _mapTabPanel, "DS_GridMap",
                new Color(0.06f, 0.08f, 0.13f, 1f), DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, 8f), new Vector2(512f, 142f));
            _mapGridContainer = gridBg;

            _mapLogText = DSPixelUIBuilder.CreateText(
                _mapTabPanel, "MapLog", "", 11,
                DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleCenter);
            _mapLogText.rectTransform.anchoredPosition = new Vector2(0f, -86f);
            _mapLogText.rectTransform.sizeDelta = new Vector2(508f, 36f);
        }

        private void RebuildMiniMapNodes()
        {
            if (_mapGridContainer == null || _session == null) return;

            for (int i = _mapGridContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_mapGridContainer.GetChild(i).gameObject);
            }

            var map = _session.State.DungeonMap;
            _mapHeaderText.text = $"ATO 1: DUNGEON (Andar {map.DepthFloor}) | Poder Pet: {_session.State.ActivePet.Stats.TotalPowerScore}";
            _mapLogText.text = map.LastExpeditionLog;

            // Helper to map (Column 0..4, Row 0..2) -> local UI coords inside 512x142 DS screen
            Vector2 GetNodeUiPos(DungeonNodeState n)
            {
                float x = -196f + n.Column * 98f;
                float y = (1 - n.Row) * 42f;
                return new Vector2(x, y);
            }

            // 1. Draw connection lines first
            for (int i = 0; i < map.Nodes.Count; i++)
            {
                var from = map.Nodes[i];
                Vector2 p1 = GetNodeUiPos(from);
                for (int j = 0; j < from.NextNodeIds.Count; j++)
                {
                    var to = DungeonMiniMapSystem.GetNodeById(map, from.NextNodeIds[j]);
                    if (to == null) continue;
                    Vector2 p2 = GetNodeUiPos(to);
                    DrawUiLine(_mapGridContainer, p1, p2,
                        from.NodeId == map.CurrentNodeId
                            ? new Color(0.35f, 0.85f, 0.95f, 0.85f)
                            : new Color(0.32f, 0.36f, 0.48f, 0.55f));
                }
            }

            // 2. Draw interactive node tiles
            for (int i = 0; i < map.Nodes.Count; i++)
            {
                var node = map.Nodes[i];
                int capturedId = node.NodeId;
                bool isCurrent = node.NodeId == map.CurrentNodeId;
                bool isExploringHere = node.NodeId == map.ActiveExpeditionNodeId;
                bool selectable = _session.MiniMap.IsNodeSelectable(map, node.NodeId);

                Color nodeColor = new Color(0.18f, 0.20f, 0.30f, 1f);
                if (isExploringHere) nodeColor = new Color(0.78f, 0.52f, 0.18f, 1f);
                else if (isCurrent) nodeColor = new Color(0.20f, 0.52f, 0.34f, 1f);
                else if (selectable) nodeColor = new Color(0.22f, 0.42f, 0.66f, 1f);
                else if (node.Cleared) nodeColor = new Color(0.14f, 0.24f, 0.20f, 0.85f);

                string prefix = node.IsUnstable && !node.Cleared ? "~" : "";
                string label = prefix + DungeonMiniMapSystem.GetNodeLabel(node.NodeType);
                if (isCurrent) label = "★ AQUI";
                else if (isExploringHere) label = "⚔ LUTANDO";

                var btn = DSPixelUIBuilder.CreateButton(
                    _mapGridContainer, $"Node_{node.NodeId}", label,
                    GetNodeUiPos(node), new Vector2(84f, 32f),
                    nodeColor, 10,
                    () =>
                    {
                        if (_session.StartMapNodeExpedition(capturedId))
                        {
                            _audio?.PlayUiTap();
                            RefreshAllUI();
                        }
                    });
                btn.interactable = selectable;
            }
        }

        private static void DrawUiLine(RectTransform parent, Vector2 a, Vector2 b, Color color)
        {
            var go = new GameObject("MapEdge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = DSPixelUIBuilder.GetWhiteSprite();
            img.color = color;
            img.raycastTarget = false;

            Vector2 diff = b - a;
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(diff.magnitude, 2.5f);
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void HandleStationClicked(PetActivityState stationActivity)
        {
            _audio?.PlayUiTap();
            if (_session.State.ActivePet.Activity == stationActivity)
            {
                _session.HandleTrainingManualClick();
            }
            else
            {
                _session.SetPetActivity(stationActivity);
            }
        }

        private void ToggleTab(TopPopupTab tab)
        {
            _audio?.PlayUiTap();
            OpenTab(_currentTab == tab ? TopPopupTab.None : tab);
        }

        private void OpenTab(TopPopupTab tab)
        {
            _currentTab = tab;
            if (_topPopupRoot == null) return;

            bool open = tab != TopPopupTab.None;
            _topPopupRoot.gameObject.SetActive(open);
            if (!open) return;

            _careTabPanel.gameObject.SetActive(tab == TopPopupTab.Care);
            _trainTabPanel.gameObject.SetActive(tab == TopPopupTab.Training);
            _mapTabPanel.gameObject.SetActive(tab == TopPopupTab.MiniMap);
            RefreshAllUI();
        }

        private void ToggleWindowMode()
        {
            _audio?.PlayUiTap();
            if (_windowController == null) return;

            var next = _windowController.CurrentMode == WindowDisplayMode.TaskbarOverlay
                ? WindowDisplayMode.FramedHandheldDS
                : WindowDisplayMode.TaskbarOverlay;
            _windowController.ApplyDisplayMode(next);
            SpawnFloatingText(
                next == WindowDisplayMode.TaskbarOverlay
                    ? "Modo Barra de Tarefas Transparente Ativo!"
                    : "Modo Portátil Nintendo DS Ativo!",
                DSPixelUIBuilder.DsAccentCyan);
        }

        private void AnimateDungeonWorld(float dt)
        {
            var pet = _session.State.ActivePet;

            // Campfire flicker
            if (_db != null && _db.CampfireFrames != null && _db.CampfireFrames.Length > 0 && _campfireImg != null)
            {
                int f = Mathf.FloorToInt(_animClock * 5f) % _db.CampfireFrames.Length;
                _campfireImg.sprite = _db.CampfireFrames[f];
            }

            // Target X along taskbar based on Pet activity
            float targetX = IdleLoungePosX + Mathf.Sin(_animClock * 0.9f) * 38f;
            switch (pet.Activity)
            {
                case PetActivityState.RestingAtCampfire: targetX = CampfirePosX + 48f; break;
                case PetActivityState.TrainingStrength: targetX = StrStationPosX - 36f; break;
                case PetActivityState.TrainingAgility: targetX = AgiStationPosX - 36f; break;
                case PetActivityState.TrainingVitality: targetX = VitStationPosX - 36f; break;
                case PetActivityState.ExploringDungeonNode: targetX = PortalPosX; break;
            }

            float prevX = _currentPetPosX;
            _currentPetPosX = Mathf.MoveTowards(_currentPetPosX, targetX, 130f * dt);
            if (_petActorRt != null)
            {
                _petActorRt.anchoredPosition = new Vector2(_currentPetPosX, 4f);
                if (Mathf.Abs(_currentPetPosX - prevX) > 0.05f)
                {
                    float dir = _currentPetPosX >= prevX ? 1f : -1f;
                    _petActorRt.localScale = new Vector3(dir, 1f, 1f);
                    _petActivityTag.rectTransform.localScale = new Vector3(dir, 1f, 1f);
                }
            }

            // Sprite frame selection
            var spec = _db != null ? _db.GetSpecies(pet.Species) : null;
            if (spec != null && _petActorImg != null)
            {
                _petActorImg.color = pet.Activity == PetActivityState.ExploringDungeonNode
                    ? new Color(1f, 1f, 1f, 0.45f)
                    : Color.white;

                if (pet.Activity == PetActivityState.RestingAtCampfire && spec.SleepSprite != null)
                {
                    _petActorImg.sprite = spec.SleepSprite;
                }
                else if ((pet.Activity == PetActivityState.TrainingStrength ||
                          pet.Activity == PetActivityState.TrainingAgility ||
                          pet.Activity == PetActivityState.TrainingVitality) && spec.TrainSprite != null)
                {
                    // Energetic training bounce!
                    bool frameToggle = (Mathf.FloorToInt(_animClock * 6f) % 2) == 0;
                    _petActorImg.sprite = frameToggle || spec.IdleSprites == null || spec.IdleSprites.Length == 0
                        ? spec.TrainSprite
                        : spec.IdleSprites[0];
                }
                else if (spec.IdleSprites != null && spec.IdleSprites.Length > 0)
                {
                    int idx = Mathf.FloorToInt(_animClock * 3.2f) % spec.IdleSprites.Length;
                    _petActorImg.sprite = spec.IdleSprites[idx];
                }
            }
        }

        public void RefreshAllUI()
        {
            if (_session == null) return;
            RefreshLiveBars();

            var pet = _session.State.ActivePet;
            _petActivityTag.text = GetActivityLabel(pet.Activity);

            if (_currentTab == TopPopupTab.Care)
            {
                float mult = _session.Care.ComputeTrainingMoodMultiplier(pet.Vitals);
                _careSummaryText.text =
                    $"Companheiro: {pet.PetName}\n" +
                    $"Saúde: {pet.Vitals.Health:0}%\n" +
                    $"Fome: {pet.Vitals.Hunger:0}%\n" +
                    $"Afeto: {pet.Vitals.Affection:0}%\n" +
                    $"Energia: {pet.Vitals.Energy:0}%\n\n" +
                    $"Multiplicador Treino: x{mult:0.00}\n" +
                    $"Fragmentos de Alma: ♦ {_session.State.SoulShards}";

                var items = _session.GetAllCareItems();
                for (int i = 0; i < _foodIds.Count && i < _foodButtonLabels.Count; i++)
                {
                    int qty = _session.GetInventoryCount(_foodIds[i]);
                    var cfg = items[i];
                    _foodButtonLabels[i].text = qty > 0
                        ? $"{cfg.DisplayName} (x{qty}) [+{cfg.HungerRestore:0} Fome]"
                        : $"{cfg.DisplayName} (Comprar ♦{cfg.ShardPrice})";
                }
            }
            else if (_currentTab == TopPopupTab.Training)
            {
                _trainActiveDesc.text =
                    $"Estação atual: {GetActivityLabel(pet.Activity)}\n" +
                    $"Dica: Clique no pet, na estação ou em INCENTIVAR para impulso extra!";
            }
            else if (_currentTab == TopPopupTab.MiniMap)
            {
                RebuildMiniMapNodes();
            }
        }

        private void RefreshLiveBars()
        {
            var pet = _session.State.ActivePet;
            var v = pet.Vitals;
            var s = pet.Stats;

            _hudPetTitle.text = $"★ {pet.PetName}";
            _hudHungerBar.fillAmount = v.Hunger / 100f;
            _hudHungerTxt.text = $"Fome {v.Hunger:0}%";

            _hudAffectionBar.fillAmount = v.Affection / 100f;
            _hudAffectionTxt.text = $"Afeto {v.Affection:0}%";

            _hudEnergyBar.fillAmount = v.Energy / 100f;
            _hudEnergyTxt.text = $"Ener {v.Energy:0}%";

            _hudStatsSummary.text = $"FOR {s.Strength} | AGI {s.Agility} | VIT {s.Vitality} | ♦ {_session.State.SoulShards}";

            if (_currentTab == TopPopupTab.Training)
            {
                _strSubBar.fillAmount = s.StrengthProgress / 100f;
                _strSubTxt.text = $"FORÇA Nv.{s.Strength} ({s.StrengthProgress:0}%)";

                _agiSubBar.fillAmount = s.AgilityProgress / 100f;
                _agiSubTxt.text = $"AGILIDADE Nv.{s.Agility} ({s.AgilityProgress:0}%)";

                _vitSubBar.fillAmount = s.VitalityProgress / 100f;
                _vitSubTxt.text = $"VIGOR Nv.{s.Vitality} ({s.VitalityProgress:0}%)";
            }
            else if (_currentTab == TopPopupTab.MiniMap)
            {
                var map = _session.State.DungeonMap;
                if (map.ActiveExpeditionNodeId >= 0 && map.ExpeditionDuration > 0.01f)
                {
                    float ratio = Mathf.Clamp01(map.ExpeditionElapsed / map.ExpeditionDuration);
                    _expeditionBarFill.fillAmount = ratio;
                    _expeditionBarTxt.text = $"Explorando {ratio * 100f:0}%";
                }
                else
                {
                    _expeditionBarFill.fillAmount = 0f;
                    _expeditionBarTxt.text = "Pronto";
                }
            }

#if UNITY_EDITOR
            if (_windowController != null && _editorOverlayHint != null)
            {
                _editorOverlayHint.text = _windowController.CurrentMode == WindowDisplayMode.TaskbarOverlay
                    ? (_windowController.IsCurrentlyClickThrough
                        ? "[Simulação Editor] Área Transparente: Clique atravessa pro Windows no Build (.exe)"
                        : "[Simulação Editor] Área Interativa da Barra/DS: Capturando mouse")
                    : "[Modo Console Portátil Nintendo DS]";
            }
#endif
        }

        private void SpawnFloatingText(string msg, Color color)
        {
            if (_floatingTextContainer == null) return;
            var txt = DSPixelUIBuilder.CreateText(
                _floatingTextContainer, "Toast", msg, 12, color, TextAnchor.MiddleCenter);
            var rt = txt.rectTransform;
            rt.anchoredPosition = new Vector2(_currentPetPosX, 54f);
            rt.sizeDelta = new Vector2(320f, 24f);

            _activeToasts.Add(new FloatingToast { Rt = rt, Label = txt, Remaining = 1.6f });
        }

        private void UpdateFloatingToasts(float dt)
        {
            for (int i = _activeToasts.Count - 1; i >= 0; i--)
            {
                var t = _activeToasts[i];
                t.Remaining -= dt;
                if (t.Remaining <= 0f || t.Rt == null)
                {
                    if (t.Rt != null) Destroy(t.Rt.gameObject);
                    _activeToasts.RemoveAt(i);
                    continue;
                }
                t.Rt.anchoredPosition += new Vector2(0f, 22f * dt);
                Color c = t.Label.color;
                c.a = Mathf.Clamp01(t.Remaining / 0.6f);
                t.Label.color = c;
            }
        }

        private Sprite GetDrillSprite(CombatStatType stat)
        {
            if (_db == null || _db.TrainingDrills == null) return null;
            for (int i = 0; i < _db.TrainingDrills.Count; i++)
            {
                var d = _db.TrainingDrills[i];
                if (d != null && d.Config.TargetStat == stat) return d.StationSprite;
            }
            return null;
        }

        private static string GetActivityLabel(PetActivityState state)
        {
            switch (state)
            {
                case PetActivityState.RestingAtCampfire: return "Descansando zZz";
                case PetActivityState.TrainingStrength: return "Treinando FOR";
                case PetActivityState.TrainingAgility: return "Treinando AGI";
                case PetActivityState.TrainingVitality: return "Treinando VIT";
                case PetActivityState.ExploringDungeonNode: return "No Mapa...";
                default: return "Passeando";
            }
        }
    }
}
