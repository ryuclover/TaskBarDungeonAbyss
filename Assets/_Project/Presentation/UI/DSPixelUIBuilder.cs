using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TBDA.Presentation.UI
{
    /// <summary>
    /// Authentic Nintendo DS / GBA RPG UI Factory (inspired by Pokémon Platinum, HGSS & Mystery Dungeon).
    /// Procedurally generates crisp 9-sliced beveled pixel-art window frames, tactile DS touch buttons,
    /// glossy GBA/NDS status bars, and drop-shadowed pixel typography in memory.
    /// </summary>
    public static class DSPixelUIBuilder
    {
        private static Font _cachedFont;
        private static Sprite _whitePixelSprite;
        private static Sprite _glossyBarSprite;
        private static readonly Dictionary<Color32, Sprite> _frameSpriteCache = new Dictionary<Color32, Sprite>();
        private static readonly Dictionary<Color32, Sprite> _buttonSpriteCache = new Dictionary<Color32, Sprite>();

        public static readonly Color DsBgDark = new Color(0.09f, 0.10f, 0.16f, 0.98f);
        public static readonly Color DsPanelMid = new Color(0.14f, 0.16f, 0.25f, 0.98f);
        public static readonly Color DsBorderGold = new Color(0.92f, 0.76f, 0.36f, 1f);
        public static readonly Color DsBorderSlate = new Color(0.42f, 0.48f, 0.66f, 1f);
        public static readonly Color DsAccentCyan = new Color(0.32f, 0.86f, 0.96f, 1f);
        public static readonly Color DsAccentGreen = new Color(0.36f, 0.82f, 0.46f, 1f);
        public static readonly Color DsAccentWarm = new Color(0.92f, 0.46f, 0.34f, 1f);
        public static readonly Color DsTextCream = new Color(0.98f, 0.96f, 0.90f, 1f);
        public static readonly Color DsTextMuted = new Color(0.74f, 0.78f, 0.88f, 1f);

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

        /// <summary>
        /// Generates a 16x16 9-sliced Nintendo DS RPG window frame (beveled corners, outer dark outline,
        /// colored metallic inner rim, and corner rivets).
        /// </summary>
        public static Sprite GetDsWindowSprite(Color rimColor)
        {
            Color32 key = rimColor;
            if (_frameSpriteCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color outerDark = new Color(0.04f, 0.04f, 0.08f, 1f);
            Color rimHighlight = Color.Lerp(rimColor, Color.white, 0.32f);
            Color rimShadow = Color.Lerp(rimColor, Color.black, 0.35f);
            Color innerGroove = new Color(0.06f, 0.07f, 0.12f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x, size - 1 - x);
                    int dy = Mathf.Min(y, size - 1 - y);

                    // Cut 1px outer corners for authentic GBA/NDS rounded pixel window silhouette
                    if (dx == 0 && dy == 0)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    // Outer dark 1px border (including diagonal corner step)
                    if (dx == 0 || dy == 0 || (dx == 1 && dy == 1))
                    {
                        tex.SetPixel(x, y, outerDark);
                        continue;
                    }

                    // 2px Metallic/Colored DS Bevel Rim
                    if (dx <= 2 || dy <= 2)
                    {
                        // Corner rivet accent
                        if (dx == 2 && dy == 2)
                        {
                            tex.SetPixel(x, y, rimHighlight);
                        }
                        else if (y >= size - 3 || x <= 2)
                        {
                            tex.SetPixel(x, y, rimHighlight);
                        }
                        else
                        {
                            tex.SetPixel(x, y, rimShadow);
                        }
                        continue;
                    }

                    // 1px Inner dark groove
                    if (dx == 3 || dy == 3)
                    {
                        tex.SetPixel(x, y, innerGroove);
                        continue;
                    }

                    // Interior fill (white so Image.color tints it cleanly)
                    tex.SetPixel(x, y, Color.white);
                }
            }

            tex.Apply();
            var spr = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                16f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(5, 5, 5, 5));

            _frameSpriteCache[key] = spr;
            return spr;
        }

        /// <summary>
        /// Generates a tactile 3D beveled Nintendo DS touch-screen button sprite.
        /// </summary>
        public static Sprite GetDsButtonSprite(Color rimColor)
        {
            Color32 key = rimColor;
            if (_buttonSpriteCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color outerDark = new Color(0.04f, 0.04f, 0.08f, 1f);
            Color topHighlight = new Color(1f, 1f, 1f, 1f);
            Color bottomShade = new Color(0.68f, 0.68f, 0.76f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x, size - 1 - x);
                    int dy = Mathf.Min(y, size - 1 - y);

                    if (dx == 0 && dy == 0)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (dx == 0 || dy == 0 || (dx == 1 && dy == 1))
                    {
                        tex.SetPixel(x, y, outerDark);
                    }
                    else if (dx == 1 || dy == 1)
                    {
                        tex.SetPixel(x, y, rimColor);
                    }
                    else if (y >= size - 4)
                    {
                        tex.SetPixel(x, y, topHighlight);
                    }
                    else if (y <= 3)
                    {
                        tex.SetPixel(x, y, bottomShade);
                    }
                    else
                    {
                        tex.SetPixel(x, y, new Color(0.90f, 0.90f, 0.95f, 1f));
                    }
                }
            }

            tex.Apply();
            var spr = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                16f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(4, 4, 4, 4));

            _buttonSpriteCache[key] = spr;
            return spr;
        }

        /// <summary>
        /// Generates a GBA/NDS Pokémon-style glossy status bar sprite with a top specular reflection band.
        /// </summary>
        public static Sprite GetGlossyBarSprite()
        {
            if (_glossyBarSprite != null) return _glossyBarSprite;
            const int w = 8;
            const int h = 12;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < h; y++)
            {
                Color rowColor;
                if (y >= h - 3)
                    rowColor = new Color(1f, 1f, 1f, 1f); // Top glossy highlight
                else if (y <= 2)
                    rowColor = new Color(0.72f, 0.72f, 0.78f, 1f); // Bottom shadow
                else
                    rowColor = new Color(0.90f, 0.90f, 0.94f, 1f); // Mid body

                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, rowColor);
            }

            tex.Apply();
            _glossyBarSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
            return _glossyBarSprite;
        }

        public static RectTransform CreatePanel(
            Transform parent,
            string name,
            Color bgColor,
            Color borderColor,
            Vector2 anchoredPos,
            Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var img = go.GetComponent<Image>();
            img.sprite = GetDsWindowSprite(borderColor);
            img.type = Image.Type.Sliced;
            img.color = bgColor;

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
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
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

            // Classic Nintendo DS 1px pixel drop shadow for crisp readability
            var shadow = go.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.03f, 0.03f, 0.07f, 0.92f);
            shadow.effectDistance = new Vector2(1f, -1f);

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
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = GetDsButtonSprite(DsBorderGold);
            img.type = Image.Type.Sliced;
            img.color = bgColor;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.80f, 0.80f, 0.85f, 1f);
            btn.colors = colors;

            var txt = CreateText(rt, "Label", label, fontSize, DsTextCream, TextAnchor.MiddleCenter);
            txt.fontStyle = FontStyle.Bold;
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6f, 3f);
            trt.offsetMax = new Vector2(-6f, -3f);

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
            var bg = CreatePanel(parent, name + "_Bg", new Color(0.05f, 0.06f, 0.10f, 1f), DsBorderSlate, anchoredPos, size);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bg, false);
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(1f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = new Vector2(3f, 3f);
            frt.offsetMax = new Vector2(-3f, -3f);

            var fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = GetGlossyBarSprite();
            fillImg.color = fillColor;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 1f;
            fillImg.raycastTarget = false;

            valueLabel = CreateText(bg, "Value", "100%", Mathf.Max(9, (int)size.y - 4), DsTextCream, TextAnchor.MiddleCenter);
            valueLabel.fontStyle = FontStyle.Bold;
            var lrt = valueLabel.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            return fillImg;
        }
    }
}
