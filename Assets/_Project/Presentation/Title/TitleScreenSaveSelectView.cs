using System;
using System.Collections.Generic;
using TBDA.Data;
using TBDA.Domain.Pets;
using TBDA.Domain.Session;
using TBDA.Presentation.Audio;
using TBDA.Presentation.Save;
using TBDA.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TBDA.Presentation.Title
{
    /// <summary>
    /// Single-screen retro RPG Title Screen & Multi-Save File Selection View.
    /// Manages Slots 1, 2, and 3 with full Create, Load, and Delete capabilities
    /// styled in authentic Nintendo DS pixel art interface standards.
    /// </summary>
    public sealed class TitleScreenSaveSelectView : MonoBehaviour
    {
        private UnityJsonSaveRepository _saveRepo;
        private GameDatabaseSO _db;
        private DSChiptuneSynthesizer _audio;
        private Action<int, bool> _onSlotChosen; // (slotIndex, isNewGame)

        private GameObject _rootCanvasGo;
        private RectTransform _mainPanelRt;
        private readonly RectTransform[] _slotCardRoots = new RectTransform[4]; // 1-indexed (1..3)

        // Deletion Confirmation Modal
        private GameObject _confirmModalGo;
        private Text _confirmModalText;
        private int _pendingDeleteSlot = -1;

        public void Initialize(
            UnityJsonSaveRepository saveRepo,
            GameDatabaseSO db,
            DSChiptuneSynthesizer audio,
            Action<int, bool> onSlotChosen)
        {
            _saveRepo = saveRepo;
            _db = db;
            _audio = audio;
            _onSlotChosen = onSlotChosen;

            BuildSingleScreenCanvas();
            RefreshAllSlots();
        }

        public void SetVisible(bool visible)
        {
            if (_rootCanvasGo != null)
            {
                _rootCanvasGo.SetActive(visible);
                if (visible)
                {
                    CloseDeleteConfirmation();
                    RefreshAllSlots();
                }
            }
        }

        private void BuildSingleScreenCanvas()
        {
            _rootCanvasGo = new GameObject("Title_SaveSelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _rootCanvasGo.transform.SetParent(transform, false);

            var canvas = _rootCanvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;

            var scaler = _rootCanvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540f, 740f);
            scaler.matchWidthOrHeight = 0.5f;

            // Single Retro Screen Shell Frame (500x700)
            _mainPanelRt = DSPixelUIBuilder.CreatePanel(
                _rootCanvasGo.transform, "MainScreenShell",
                DSPixelUIBuilder.DsBgDark, DSPixelUIBuilder.DsBorderGold,
                Vector2.zero, new Vector2(512f, 712f));

            // Top Header: Game Logo & Title Banner
            var headerBox = DSPixelUIBuilder.CreatePanel(
                _mainPanelRt, "HeaderBox",
                new Color(0.08f, 0.10f, 0.18f, 1f), DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, 290f), new Vector2(484f, 96f));

            var titleText = DSPixelUIBuilder.CreateText(
                headerBox, "GameTitle", "TASK BAR: DUNGEON ABYSS", 18,
                DSPixelUIBuilder.DsBorderGold, TextAnchor.MiddleCenter);
            titleText.fontStyle = FontStyle.Bold;
            titleText.rectTransform.anchoredPosition = new Vector2(0f, 16f);
            titleText.rectTransform.sizeDelta = new Vector2(464f, 32f);

            var subtitleText = DSPixelUIBuilder.CreateText(
                headerBox, "Subtitle", "◈ SELEÇÃO DE ARQUIVO • SLOTS 1 A 3 ◈", 12,
                DSPixelUIBuilder.DsAccentCyan, TextAnchor.MiddleCenter);
            subtitleText.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            subtitleText.rectTransform.sizeDelta = new Vector2(464f, 24f);

            // Container for 3 Save Slots
            var slotsContainer = new GameObject("SlotsContainer", typeof(RectTransform));
            slotsContainer.transform.SetParent(_mainPanelRt, false);
            var scRt = slotsContainer.GetComponent<RectTransform>();
            scRt.anchoredPosition = new Vector2(0f, 20f);
            scRt.sizeDelta = new Vector2(484f, 420f);

            float[] yOffsets = { 0f, 138f, 0f, -138f };
            for (int s = 1; s <= UnityJsonSaveRepository.MaxSlots; s++)
            {
                var card = DSPixelUIBuilder.CreatePanel(
                    scRt, $"SlotCard_{s}",
                    DSPixelUIBuilder.DsPanelMid, DSPixelUIBuilder.DsBorderSlate,
                    new Vector2(0f, yOffsets[s]), new Vector2(476f, 126f));
                _slotCardRoots[s] = card;
            }

            // Bottom Footer
            var footerBox = DSPixelUIBuilder.CreatePanel(
                _mainPanelRt, "FooterBox",
                new Color(0.06f, 0.07f, 0.12f, 1f), DSPixelUIBuilder.DsBorderSlate,
                new Vector2(0f, -316f), new Vector2(484f, 48f));

            var footerText = DSPixelUIBuilder.CreateText(
                footerBox, "FooterTxt", "Toque em um arquivo para jogar ou crie um novo companheiro!", 11,
                DSPixelUIBuilder.DsTextMuted, TextAnchor.MiddleLeft);
            footerText.rectTransform.anchoredPosition = new Vector2(-60f, 0f);
            footerText.rectTransform.sizeDelta = new Vector2(330f, 32f);

            DSPixelUIBuilder.CreateButton(
                footerBox, "QuitBtn", "Sair",
                new Vector2(188f, 0f), new Vector2(74f, 30f),
                new Color(0.25f, 0.16f, 0.20f, 1f), 11,
                () => Application.Quit());

            BuildConfirmationModal();
        }

        public void RefreshAllSlots()
        {
            if (_saveRepo == null) return;

            for (int s = 1; s <= UnityJsonSaveRepository.MaxSlots; s++)
            {
                RefreshSlotCard(s);
            }
        }

        private void RefreshSlotCard(int slotIndex)
        {
            var cardRt = _slotCardRoots[slotIndex];
            if (cardRt == null) return;

            // Clear previous dynamic children
            for (int i = cardRt.childCount - 1; i >= 0; i--)
            {
                Destroy(cardRt.GetChild(i).gameObject);
            }

            var summary = _saveRepo.GetSlotSummary(slotIndex);

            // Card Header Tag
            var tagBg = DSPixelUIBuilder.CreatePanel(
                cardRt, "TagBg",
                new Color(0.08f, 0.10f, 0.18f, 1f),
                summary.Exists ? DSPixelUIBuilder.DsBorderGold : DSPixelUIBuilder.DsBorderSlate,
                new Vector2(-158f, 44f), new Vector2(138f, 24f));

            var tagTxt = DSPixelUIBuilder.CreateText(
                tagBg, "TagTxt", $"ARQUIVO {slotIndex}", 11,
                summary.Exists ? DSPixelUIBuilder.DsBorderGold : DSPixelUIBuilder.DsTextMuted,
                TextAnchor.MiddleCenter);
            tagTxt.fontStyle = FontStyle.Bold;
            StretchFull(tagTxt.rectTransform, 2f);

            if (!summary.Exists)
            {
                // EMPTY SLOT
                var emptyTxt = DSPixelUIBuilder.CreateText(
                    cardRt, "EmptyTxt", "(Slot Vazio — Pronto para iniciar uma nova aventura!)", 12,
                    DSPixelUIBuilder.DsTextMuted, TextAnchor.MiddleLeft);
                emptyTxt.rectTransform.anchoredPosition = new Vector2(-46f, 4f);
                emptyTxt.rectTransform.sizeDelta = new Vector2(340f, 36f);

                int s = slotIndex;
                DSPixelUIBuilder.CreateButton(
                    cardRt, "NewGameBtn", "＋ NOVO JOGO",
                    new Vector2(154f, 0f), new Vector2(138f, 44f),
                    new Color(0.16f, 0.48f, 0.28f, 1f), 13,
                    () => HandleSlotAction(s, isNewGame: true));
            }
            else
            {
                // OCCUPIED SLOT
                // Pet Portrait box on left
                var portraitBox = DSPixelUIBuilder.CreatePanel(
                    cardRt, "PortraitBox",
                    new Color(0.08f, 0.09f, 0.14f, 1f), DSPixelUIBuilder.DsBorderGold,
                    new Vector2(-176f, -10f), new Vector2(74f, 74f));

                var portGo = new GameObject("Sprite", typeof(RectTransform), typeof(Image));
                portGo.transform.SetParent(portraitBox, false);
                portGo.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 64f);
                var portImg = portGo.GetComponent<Image>();
                portImg.raycastTarget = false;

                var spec = _db != null ? _db.GetSpecies(summary.Species) : null;
                if (spec != null && spec.IdleSprites != null && spec.IdleSprites.Length > 0)
                    portImg.sprite = spec.IdleSprites[0];

                // Pet Info Column
                string speciesName = spec != null ? spec.DisplayNamePtBr : summary.Species.ToString();
                var nameTxt = DSPixelUIBuilder.CreateText(
                    cardRt, "PetName", $"<b>{summary.PetName}</b>  <color=#FAD64E>({speciesName})</color>", 14,
                    DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleLeft);
                nameTxt.rectTransform.anchoredPosition = new Vector2(14f, 22f);
                nameTxt.rectTransform.sizeDelta = new Vector2(280f, 24f);

                var statsTxt = DSPixelUIBuilder.CreateText(
                    cardRt, "StatsTxt", $"FOR <b>{summary.Strength}</b>  |  AGI <b>{summary.Agility}</b>  |  VIT <b>{summary.Vitality}</b>", 12,
                    DSPixelUIBuilder.DsAccentCyan, TextAnchor.MiddleLeft);
                statsTxt.rectTransform.anchoredPosition = new Vector2(14f, -4f);
                statsTxt.rectTransform.sizeDelta = new Vector2(280f, 22f);

                var progTxt = DSPixelUIBuilder.CreateText(
                    cardRt, "ProgTxt", $"Masmorra Andar {summary.DepthFloor}  •  ♦ {summary.SoulShards} Shards  •  Salvo: {summary.FormattedLastSaved}", 10,
                    DSPixelUIBuilder.DsTextMuted, TextAnchor.MiddleLeft);
                progTxt.rectTransform.anchoredPosition = new Vector2(14f, -28f);
                progTxt.rectTransform.sizeDelta = new Vector2(280f, 20f);

                // Action Buttons on Right
                int s = slotIndex;
                DSPixelUIBuilder.CreateButton(
                    cardRt, "PlayBtn", "▶ CONTINUAR",
                    new Vector2(166f, 18f), new Vector2(118f, 38f),
                    new Color(0.18f, 0.38f, 0.62f, 1f), 12,
                    () => HandleSlotAction(s, isNewGame: false));

                DSPixelUIBuilder.CreateButton(
                    cardRt, "DeleteBtn", "✖ Excluir",
                    new Vector2(166f, -28f), new Vector2(118f, 28f),
                    new Color(0.48f, 0.18f, 0.22f, 1f), 10,
                    () => OpenDeleteConfirmation(s, summary.PetName));
            }
        }

        private void BuildConfirmationModal()
        {
            _confirmModalGo = new GameObject("DeleteConfirmModal", typeof(RectTransform), typeof(Image));
            _confirmModalGo.transform.SetParent(_rootCanvasGo.transform, false);
            StretchFull(_confirmModalGo.GetComponent<RectTransform>(), 0f);

            var modalBg = _confirmModalGo.GetComponent<Image>();
            modalBg.sprite = DSPixelUIBuilder.GetWhiteSprite();
            modalBg.color = new Color(0.04f, 0.05f, 0.08f, 0.85f);

            // Dialog Window
            var dialog = DSPixelUIBuilder.CreatePanel(
                _confirmModalGo.transform, "DialogBox",
                DSPixelUIBuilder.DsPanelMid, DSPixelUIBuilder.DsAccentWarm,
                Vector2.zero, new Vector2(410f, 210f));

            var title = DSPixelUIBuilder.CreateText(
                dialog, "DialogTitle", "⚠ APAGAR ARQUIVO DE SAVE", 14,
                DSPixelUIBuilder.DsAccentWarm, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.anchoredPosition = new Vector2(0f, 72f);
            title.rectTransform.sizeDelta = new Vector2(380f, 28f);

            _confirmModalText = DSPixelUIBuilder.CreateText(
                dialog, "DialogMessage", "Deseja realmente apagar este arquivo?\nTodo o progresso será perdido permanentemente.", 13,
                DSPixelUIBuilder.DsTextCream, TextAnchor.MiddleCenter);
            _confirmModalText.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            _confirmModalText.rectTransform.sizeDelta = new Vector2(370f, 64f);

            DSPixelUIBuilder.CreateButton(
                dialog, "CancelBtn", "Cancelar",
                new Vector2(-88f, -60f), new Vector2(140f, 40f),
                new Color(0.24f, 0.26f, 0.36f, 1f), 13,
                CloseDeleteConfirmation);

            DSPixelUIBuilder.CreateButton(
                dialog, "ConfirmBtn", "SIM, APAGAR",
                new Vector2(88f, -60f), new Vector2(140f, 40f),
                new Color(0.64f, 0.18f, 0.22f, 1f), 13,
                ExecuteConfirmedDelete);

            _confirmModalGo.SetActive(false);
        }

        private void OpenDeleteConfirmation(int slotIndex, string petName)
        {
            _audio?.PlayUiTap();
            _pendingDeleteSlot = slotIndex;
            if (_confirmModalText != null)
            {
                _confirmModalText.text = $"Deseja realmente apagar o <b>Arquivo {slotIndex} ({petName})</b>?\nTodo o progresso será perdido permanentemente.";
            }
            if (_confirmModalGo != null)
                _confirmModalGo.SetActive(true);
        }

        private void CloseDeleteConfirmation()
        {
            _audio?.PlayUiTap();
            _pendingDeleteSlot = -1;
            if (_confirmModalGo != null)
                _confirmModalGo.SetActive(false);
        }

        private void ExecuteConfirmedDelete()
        {
            if (_pendingDeleteSlot >= 1 && _pendingDeleteSlot <= UnityJsonSaveRepository.MaxSlots)
            {
                _saveRepo.DeleteSlot(_pendingDeleteSlot);
                _audio?.PlayUiTap();
            }
            CloseDeleteConfirmation();
            RefreshAllSlots();
        }

        private void HandleSlotAction(int slotIndex, bool isNewGame)
        {
            _audio?.PlayUiTap();
            _saveRepo.ActiveSlotIndex = slotIndex;
            _onSlotChosen?.Invoke(slotIndex, isNewGame);
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
