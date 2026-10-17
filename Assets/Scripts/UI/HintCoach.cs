using Hollow.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollow.UI
{
    /// <summary>
    /// Shows one contextual hint at a time like the opening tutorial: the screen dims a little except the HUD part
    /// the hint is about, which gets a pulsing frame, and a short card sits above it. The world keeps running, the
    /// overlay never takes a click, and a hint closes when the player does what it says or after a few seconds.
    /// </summary>
    public sealed class HintCoach : MonoBehaviour
    {
        const float MinSeconds = 0.8f, MaxSeconds = 4f, GapSeconds = 1.2f, FadeSeconds = 0.2f, Pad = 14f, DimAlpha = 0.5f;

        UIRoot root;
        HudView hud;
        RectTransform overlay, card;
        CanvasGroup group;
        readonly Image[] dim = new Image[4];
        readonly Image[] frame = new Image[4];
        TextMeshProUGUI cardText;

        HintId? current;
        HintStart start;
        float shownFor, gapLeft, alpha;

        public bool Showing => current.HasValue;

        public void Init(UIRoot uiRoot, HudView hudView)
        {
            root = uiRoot;
            hud = hudView;
            var t = root.Theme;
            overlay = UIFactory.Stretch(root.Hud, "Hint");
            overlay.SetAsLastSibling(); // over the HUD, under menus and the shop
            group = overlay.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            for (int i = 0; i < 4; i++)
            {
                dim[i] = Piece("Dim", new Color(0, 0, 0, DimAlpha));
                frame[i] = Piece("Frame", t.Accent);
            }
            card = UIFactory.Rect(overlay, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(660, 118));
            UIFactory.Panel(card, t);
            var edge = UIFactory.Rect(card, "Edge", new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(6, 118));
            var edgeImage = edge.gameObject.AddComponent<Image>();
            edgeImage.color = t.Accent;
            edgeImage.raycastTarget = false;
            cardText = UIFactory.Text(card, "Text", "", t, 26, TextAlignmentOptions.MidlineRight);
            cardText.margin = new Vector4(22, 10, 26, 10);
            group.alpha = 0;
            overlay.gameObject.SetActive(false);
        }

        Image Piece(string name, Color color)
        {
            var rt = UIFactory.Rect(overlay, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Called every frame. <paramref name="canShow"/> is false over menus, scenes and the open shop.</summary>
        public void Sync(World w, bool canShow)
        {
            float dt = Time.unscaledDeltaTime;
            if (!canShow)
            {
                // A hint interrupted by the shop or a menu is finished: it was seen, and acting on it often opens the shop.
                if (current.HasValue && shownFor >= MinSeconds) Close();
                alpha = 0;
                Apply();
                return;
            }

            if (current == null)
            {
                gapLeft -= dt;
                if (gapLeft > 0) { Apply(); return; }
                var next = HintRules.Next(w);
                if (next == null) { Apply(); return; }
                current = next;
                start = HintRules.Capture(w);
                shownFor = 0;
                HintRules.MarkSeen(w, next.Value);
                cardText.text = HintRules.Text(next.Value);
            }

            shownFor += dt;
            bool finished = shownFor >= MaxSeconds || (shownFor >= MinSeconds && HintRules.Done(w, current.Value, start));
            if (finished) Close();
            alpha = Mathf.MoveTowards(alpha, current.HasValue ? 1f : 0f, dt / FadeSeconds);
            Apply();
        }

        void Close()
        {
            current = null;
            gapLeft = GapSeconds;
        }

        void Apply()
        {
            if (current == null && alpha <= 0f)
            {
                if (overlay.gameObject.activeSelf) overlay.gameObject.SetActive(false);
                return;
            }
            if (current == null) alpha = Mathf.MoveTowards(alpha, 0f, Time.unscaledDeltaTime / FadeSeconds);
            overlay.gameObject.SetActive(true);
            group.alpha = alpha;
            if (current.HasValue) Layout(hud.TargetRect(HintRules.Target(current.Value)));
        }

        /// <summary>Four dark pieces around the target leave it lit; a frame pulses around it; the card sits above it.</summary>
        void Layout(RectTransform target)
        {
            var r = overlay.rect;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = overlay.InverseTransformPoint(corners[0]), max = overlay.InverseTransformPoint(corners[2]);
            min -= Vector2.one * Pad;
            max += Vector2.one * Pad;
            Place(dim[0], new Vector2(r.xMin, max.y), new Vector2(r.xMax, r.yMax));
            Place(dim[1], new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, min.y));
            Place(dim[2], new Vector2(r.xMin, min.y), new Vector2(min.x, max.y));
            Place(dim[3], new Vector2(max.x, min.y), new Vector2(r.xMax, max.y));

            const float w = 4f;
            Place(frame[0], new Vector2(min.x - w, max.y), new Vector2(max.x + w, max.y + w));
            Place(frame[1], new Vector2(min.x - w, min.y - w), new Vector2(max.x + w, min.y));
            Place(frame[2], new Vector2(min.x - w, min.y), new Vector2(min.x, max.y));
            Place(frame[3], new Vector2(max.x, min.y), new Vector2(max.x + w, max.y));
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
            var c = root.Theme.Accent;
            foreach (var f in frame) f.color = new Color(c.r, c.g, c.b, pulse);

            float half = card.sizeDelta.x * 0.5f + 20f;
            float x = Mathf.Clamp((min.x + max.x) * 0.5f, r.xMin + half, r.xMax - half);
            card.anchoredPosition = new Vector2(x, max.y + 24f);
        }

        static void Place(Image img, Vector2 from, Vector2 to)
        {
            var rt = img.rectTransform;
            rt.anchoredPosition = (from + to) * 0.5f;
            rt.sizeDelta = new Vector2(Mathf.Max(0, to.x - from.x), Mathf.Max(0, to.y - from.y));
        }
    }
}
