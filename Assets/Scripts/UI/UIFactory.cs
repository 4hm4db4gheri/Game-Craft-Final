using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollow.UI
{
    /// <summary>Small helpers to build uGUI hierarchies in code from the theme (one look everywhere).</summary>
    public static class UIFactory
    {
        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Panel(RectTransform rt, UITheme theme, Color? color = null, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = theme.Panel;
            img.type = theme.Panel != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color ?? theme.PanelColor;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, UITheme theme, float size,
            TextAlignmentOptions align, Color? color = null)
        {
            var rt = Stretch(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (theme.Font != null) t.font = theme.Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? theme.Text;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, UITheme theme, Vector2 anchor, Vector2 pos, Vector2 size,
            Action onClick, out TextMeshProUGUI text, float fontSize = 0)
        {
            var rt = Rect(parent, name, anchor, new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = theme.Button;
            img.type = theme.Button != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = Color.white;
            var b = rt.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = theme.ButtonColor;
            colors.highlightedColor = theme.ButtonHover;
            colors.selectedColor = theme.ButtonHover;
            colors.pressedColor = theme.ButtonHover * 1.2f;
            colors.disabledColor = theme.ButtonDisabled;
            colors.colorMultiplier = 1;
            b.colors = colors;
            b.onClick.AddListener(() => onClick());
            text = Text(rt, "Label", label, theme, fontSize > 0 ? fontSize : theme.BaseFontSize, TextAlignmentOptions.Center);
            text.margin = new Vector4(10, 6, 10, 6);
            return b;
        }

        public static BarView Bar(Transform parent, string name, UITheme theme, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color fill)
        {
            var rt = Rect(parent, name, anchor, pivot, pos, size);
            var back = rt.gameObject.AddComponent<Image>();
            back.color = new Color(0, 0, 0, 0.55f);
            back.raycastTarget = false;
            var fillRt = Stretch(rt, "Fill");
            fillRt.offsetMin = new Vector2(2, 2);
            fillRt.offsetMax = new Vector2(-2, -2);
            var img = fillRt.gameObject.AddComponent<Image>();
            img.sprite = theme.Bar;
            img.color = fill;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.raycastTarget = false;
            if (img.sprite == null) img.sprite = WhiteSprite();
            var bar = rt.gameObject.AddComponent<BarView>();
            bar.Init(img);
            return bar;
        }

        static Sprite white;

        /// <summary>Filled images need a sprite; a generated 4×4 white one is enough.</summary>
        public static Sprite WhiteSprite()
        {
            if (white != null) return white;
            var tex = new Texture2D(4, 4);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            return white;
        }
    }
}
