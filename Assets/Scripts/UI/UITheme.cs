using TMPro;
using UnityEngine;

namespace Hollow.UI
{
    /// <summary>
    /// Look of every screen in one asset so the artist can restyle the UI (9-slice sprites, colours, font)
    /// without touching code. Empty sprites fall back to flat colours.
    /// </summary>
    [CreateAssetMenu(menuName = "Hollow/UI Theme")]
    public sealed class UITheme : ScriptableObject
    {
        public TMP_FontAsset Font;
        public Sprite Panel;
        public Sprite Button;
        public Sprite Bar;
        public Color PanelColor = new(0.05f, 0.06f, 0.08f, 0.88f);
        public Color ButtonColor = new(0.14f, 0.18f, 0.22f, 1f);
        public Color ButtonHover = new(0.2f, 0.3f, 0.36f, 1f);
        public Color ButtonDisabled = new(0.12f, 0.12f, 0.13f, 0.8f);
        public Color Text = new(0.9f, 0.95f, 1f);
        public Color TextDim = new(0.6f, 0.66f, 0.7f);
        public Color Accent = new(0.5f, 0.92f, 1f);
        public Color Health = new(0.85f, 0.3f, 0.3f);
        public Color CompanionHealth = new(0.4f, 0.85f, 0.5f);
        public Color Xp = new(0.55f, 0.75f, 1f);
        public Color Coin = new(1f, 0.78f, 0.24f);
        public Color Warning = new(1f, 0.29f, 0.11f);
        public float BaseFontSize = 28f;
    }
}
