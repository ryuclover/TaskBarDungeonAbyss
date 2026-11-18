using System;
using UnityEngine;
using UnityEngine.UI;

namespace TBDA.Presentation.UI
{
    /// <summary>
    /// Helper factory for crisp Nintendo DS-styled dark slate pixel panels, buttons, and bars.
    /// Compatible with both Unity 6 (LegacyRuntime.ttf) and Unity 2022.3 LTS (Arial.ttf).
    /// </summary>
    public static class DSPixelUIBuilder
    {
        private static Font _cachedFont;
        private static Sprite _whitePixelSprite;

        public static readonly Color DsBgDark = new Color(0.10f, 0.10f, 0.16f, 0.97f);
        public static readonly Color DsPanelMid = new Color(0.16f, 0.16f, 0.25f, 0.98f);
        public static readonly Color DsBorderGold = new Color(0.84f, 0.70f, 0.36f, 1f);
        public static readonly Color DsBorderSlate = new Color(0.38f, 0.42f, 0.56f, 1f);
        public static readonly Color DsAccentCyan = new Color(0.28f, 0.82f, 0.92f, 1f);
        public static readonly Color DsAccentGreen = new Color(0.36f, 0.78f, 0.44f, 1f);
        public static readonly Color DsAccentWarm = new Color(0.88f, 0.44f, 0.34f, 1f);
        public static readonly Color DsTextCream = new Color(0.96f, 0.94f, 0.88f, 1f);
        public static readonly Color DsTextMuted = new Color(0.70f, 0.72f, 0.82f, 1f);

        public static Font GetFont()
        {
            if (_cachedFont != null) return _cachedFont;
            _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_cachedFont == null)
                _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _cachedFont;
        }

        public static Sprite GetWhiteSprite()
        {
            if (_whitePixelSprite != null) return _whitePixelSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            _whitePixelSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 16f);
            return _whitePixelSprite;
        }

        public static RectTransform CreatePanel(
            Transform parent,
            string name,
            Color bgColor,
            Color borderColor,
            Vector2 anchoredPos,
            Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var img = go.GetComponent<Image>();
            img.sprite = GetWhiteSprite();
            img.color = bgColor;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(2f, -2f);
            return rt;
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            Color color,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var txt = go.GetComponent<Text>();
            txt.font = GetFont();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPos,
            Vector2 size,
            Color bgColor,
            int fontSize,
            Action onClick)
        {
            var rt = CreatePanel(parent, name, bgColor, DsBorderSlate, anchoredPos, size);
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            btn.colors = colors;

            var txt = CreateText(rt, "Label", label, fontSize, DsTextCream, TextAnchor.MiddleCenter);
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(4f, 2f);
            trt.offsetMax = new Vector2(-4f, -2f);

            if (onClick != null)
                btn.onClick.AddListener(() => onClick.Invoke());

            return btn;
        }

        public static Image CreateStatBar(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            Color fillColor,
            out Text valueLabel)
        {
            var bg = CreatePanel(parent, name + "_Bg", new Color(0.06f, 0.06f, 0.10f, 1f), DsBorderSlate, anchoredPos, size);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bg, false);
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(1f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);

            var fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = GetWhiteSprite();
            fillImg.color = fillColor;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 1f;
            fillImg.raycastTarget = false;

            valueLabel = CreateText(bg, "Value", "100%", Mathf.Max(9, (int)size.y - 4), DsTextCream, TextAnchor.MiddleCenter);
            var lrt = valueLabel.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            return fillImg;
        }
    }
}
