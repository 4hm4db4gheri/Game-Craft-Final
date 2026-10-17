using System.Linq;
using Hollow.Presentation.Menu;
using UnityEditor;
using UnityEngine;
using Layer = Hollow.Presentation.Menu.MenuBackdrop.Layer;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Places the parallax layers of the main menu (back to front) for a 7.5-unit orthographic camera. The menu
    /// buttons sit on the right (Persian reads from the right), so the AI helper floats on the left.
    /// </summary>
    public static class MenuBackdropBuilder
    {
        public static MenuBackdrop Build(Camera camera)
        {
            var info = MenuArt.Generate();
            var backdrop = new GameObject("Backdrop").AddComponent<MenuBackdrop>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(PrefabBuilder.UnlitMaterialPath);
            Vector2 ai = new(-7.6f, 1.9f);

            Layer Tiled(string name, float bottom, float height, float depth, float scroll, Color tint, int order, string sprite = null) =>
                new() { Name = name, Sprite = MenuArt.Load(sprite ?? name), Position = new Vector2(0, bottom), Height = height, Depth = depth, Scroll = scroll, Tint = tint, Order = order };
            Layer Single(string name, string sprite, Vector2 at, float height, float depth, Color tint, int order) =>
                new() { Name = name, Sprite = MenuArt.Load(sprite), Tiled = false, Position = at, Height = height, Depth = depth, Tint = tint, Order = order };

            var layers = new[]
            {
                new Layer { Name = "Sky", Sprite = MenuArt.Load("Sky"), FillScreen = true, Order = -100 },
                Pulse(Tiled("Stars", -1f, 8.6f, 0.02f, 0.03f, new Color(1f, 0.95f, 0.9f, 0.9f), -97), 0.2f, 0.6f),
                Single("MoonGlow", "Glow", new Vector2(-3.5f, 4.6f), 7f, 0.04f, new Color(1f, 0.78f, 0.6f, 0.16f), -96),
                Single("Moon", "Moon", new Vector2(-3.5f, 4.6f), 1.6f, 0.04f, new Color(1f, 0.94f, 0.82f), -95),
                Single("SunsetGlow", "Glow", new Vector2(4f, -3.8f), 17f, 0.05f, new Color(1f, 0.52f, 0.3f, 0.22f), -94),
                Tiled("Mountains", -5f, 5.2f, 0.07f, 0.06f, new Color(0.36f, 0.22f, 0.36f), -90),
                Tiled("CloudsThin", 2f, 4f, 0.09f, 0.18f, new Color(0.9f, 0.5f, 0.5f, 0.14f), -89),
                Tiled("Clouds", -2.2f, 5f, 0.1f, 0.12f, new Color(0.62f, 0.3f, 0.36f, 0.42f), -88),
                Tiled("Factory", -6.6f, 10f, 0.14f, 0.22f, new Color(0.2f, 0.13f, 0.22f), -85),
                Tiled("Fog", -4.6f, 3f, 0.2f, 0.35f, new Color(0.95f, 0.5f, 0.42f, 0.26f), -82),
                Tiled("HillsFar", -8.7f, PixelHeight("HillsFar") * (5f / 400f), 0.28f, 0.4f, new Color(0.09f, 0.08f, 0.13f), -80),
                Tiled("Pylons", -7.6f, 8.5f, 0.4f, 0.55f, new Color(0.05f, 0.045f, 0.07f), -75),
                Tiled("HillsNear", -9.2f, PixelHeight("HillsNear") * (4.2f / 400f), 0.6f, 0.8f, new Color(0.025f, 0.025f, 0.04f), -70),
                Pulse(Bob(Single("AiGlow", "Glow", ai, 6f, 0.45f, new Color(0.4f, 0.95f, 1f, 0.24f), -62)), 0.35f, 2f),
                Bob(Single("AiShell", "AiShell", ai, 2.4f, 0.45f, new Color(0.62f, 0.66f, 0.74f), -61)),
                Pulse(Bob(Single("AiCore", "AiCore", ai + new Vector2(0, -0.04f), 1.32f, 0.45f, new Color(0.5f, 0.97f, 1f), -60)), 0.2f, 2f),
                Tiled("LowFog", -8.6f, 3.2f, 0.8f, 0.6f, new Color(0.45f, 0.28f, 0.34f, 0.2f), -50, "Fog"),
                new Layer { Name = "Vignette", Sprite = MenuArt.Load("Vignette"), FillScreen = true, Order = -40 },
            };
            foreach (var l in layers.Where(l => l.Sprite == null)) Debug.LogError($"[Hollow art] menu layer '{l.Name}' has no sprite.");

            backdrop.Configure(camera, material, layers, MenuArt.Load("Smoke"), MenuArt.Load("Dot"), MenuArt.Load("Glow"),
                info.Smoke.ToArray(), info.Lights.ToArray(), info.Windows.ToArray());
            return backdrop;
        }

        /// <summary>Hill strips grow upwards with their trees; the hill itself keeps its 400-pixel scale.</summary>
        static float PixelHeight(string sprite) => MenuArt.Load(sprite) is { } s ? s.rect.height : 400f;

        static Layer Bob(Layer l) { l.Bob = 0.22f; l.BobSpeed = 1.3f; return l; }
        static Layer Pulse(Layer l, float amount, float speed) { l.Pulse = amount; l.PulseSpeed = speed; return l; }
    }
}
