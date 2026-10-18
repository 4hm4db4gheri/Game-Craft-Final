using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollow.UI
{
    /// <summary>Small helpers to build uGUI hierarchies in code from the theme (one look everywhere).</summary>
    public static class UIFactory
    {
        /// <summary>Raised for every button press (the audio bridge plays the UI click).</summary>
        public static event Action ButtonClicked;

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

        /// <summary>
        /// Every UI text is a right-to-left Persian text (RTLTMPro: letter joining, Persian digits, rich-text tags).
        /// Left-aligned layouts are mirrored to the right edge, where Persian lines start.
        /// </summary>
        public static TextMeshProUGUI Text(Transform parent, string name, string text, UITheme theme, float size,
            TextAlignmentOptions align, Color? color = null)
        {
            var rt = Stretch(parent, name);
            var t = rt.gameObject.AddComponent<RTLTMPro.RTLTextMeshPro>();
            t.Farsi = true;
            t.FixTags = true;
            t.ForceFix = true;
            if (theme.Font != null) t.font = theme.Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = Mirror(align);
            t.color = color ?? theme.Text;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public static TextAlignmentOptions Mirror(TextAlignmentOptions a) => a switch
        {
            TextAlignmentOptions.TopLeft => TextAlignmentOptions.TopRight,
            TextAlignmentOptions.Left => TextAlignmentOptions.Right,
            TextAlignmentOptions.MidlineLeft => TextAlignmentOptions.MidlineRight,
            TextAlignmentOptions.BottomLeft => TextAlignmentOptions.BottomRight,
            TextAlignmentOptions.BaselineLeft => TextAlignmentOptions.BaselineRight,
            TextAlignmentOptions.CaplineLeft => TextAlignmentOptions.CaplineRight,
            _ => a,
        };

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
            b.onClick.AddListener(() =>
            {
                Click();
                onClick();
            });
            text = Text(rt, "Label", label, theme, fontSize > 0 ? fontSize : theme.BaseFontSize, TextAlignmentOptions.Center);
            text.margin = new Vector4(10, 6, 10, 6);
            return b;
        }

        /// <summary>Plays the UI click for a button built by hand. Feedback only: a failing listener never swallows the action.</summary>
        public static void Click()
        {
            try { ButtonClicked?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
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

        static Sprite white, triangle;

        /// <summary>Enter Play Mode without Domain Reload keeps statics between sessions: start every session clean.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ButtonClicked = null;
            white = triangle = null;
        }

        /// <summary>An upward-pointing arrow head drawn as an image (the Persian font has no ▲ glyph).</summary>
        public static Image Triangle(RectTransform parent, Color color)
        {
            var rt = Stretch(parent, "Arrow");
            rt.offsetMin = new Vector2(8, 10);
            rt.offsetMax = new Vector2(-8, -10);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = TriangleSprite();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Sprite TriangleSprite()
        {
            if (triangle != null) return triangle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float half = (size - y) * 0.5f; // apex at the top
                    float d = Mathf.Min(x - (size * 0.5f - half), (size * 0.5f + half) - x, y);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            triangle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return triangle;
        }

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
