using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>A parsed art file name: &lt;CAT&gt;_&lt;ID&gt;_&lt;Name&gt;_&lt;state&gt;[_f&lt;frames&gt;][_&lt;variant&gt;].png</summary>
    public readonly struct ArtName
    {
        public readonly string Category, Id, Name, State, Variant;
        public readonly int Frames;

        ArtName(string cat, string id, string name, string state, int frames, string variant)
        {
            Category = cat; Id = id; Name = name; State = state; Frames = frames; Variant = variant;
        }

        public string Key => $"{Category}_{Id}";

        public static bool TryParse(string fileName, out ArtName result)
        {
            result = default;
            var parts = new List<string>(System.IO.Path.GetFileNameWithoutExtension(fileName).Split('_'));
            if (parts.Count < 4) return false;
            string cat = parts[0], id = parts[1], name = parts[2];
            parts.RemoveRange(0, 3);
            string variant = null;
            if (parts.Count > 1 && parts[^1].Length == 1 && char.IsLower(parts[^1][0])) { variant = parts[^1]; parts.RemoveAt(parts.Count - 1); }
            int frames = 1;
            if (parts.Count > 1 && parts[^1].Length > 1 && parts[^1][0] == 'f' && int.TryParse(parts[^1].Substring(1), out int f)) { frames = Math.Max(1, f); parts.RemoveAt(parts.Count - 1); }
            if (parts.Count == 0) return false;
            result = new ArtName(cat, id, name, string.Join("_", parts), frames, variant);
            return true;
        }
    }

    /// <summary>
    /// Import rules for AI art (docs/ART_MASTER_BRIEF.md): cell size in game units per asset id, pivot per
    /// category. Art-pipeline configuration only — no gameplay numbers.
    /// </summary>
    public static class ArtRules
    {
        public const string IncomingDir = "Assets/Art/Incoming";
        public const int PixelsPerUnit = 128;

        static readonly Dictionary<string, float> CellUnits = new()
        {
            ["CHR_PLY"] = 2, ["CHR_OPR"] = 2,
            ["ENM_A1"] = 2, ["ENM_A2"] = 2, ["ENM_A3"] = 3, ["ENM_A4"] = 3, ["ENM_A5"] = 2, ["ENM_A6"] = 1, ["ENM_A7"] = 3, ["ENM_A7c"] = 1,
            ["ENM_R1"] = 2, ["ENM_R2"] = 2, ["ENM_R3"] = 2, ["ENM_R4"] = 3, ["ENM_R5"] = 2, ["ENM_R6"] = 1, ["ENM_R7"] = 3, ["ENM_R7c"] = 1,
            ["ENM_H1"] = 2, ["ENM_H2"] = 2, ["ENM_H3"] = 2, ["ENM_H4"] = 2, ["ENM_H5"] = 2, ["ENM_H6"] = 2, ["ENM_H7"] = 2, ["ENM_H7c"] = 2,
            ["BOS_B1"] = 6, ["BOS_B2"] = 6, ["BOS_B3"] = 4,
            ["PKP_COIN"] = 0.5f, ["PKP_GRN"] = 1, ["PKP_MIN"] = 1,
            ["FX_GEN"] = 1, ["FX_DEC"] = 1,
        };

        /// <summary>Weapon and projectile lengths (canvas is 2 × length wide).</summary>
        static readonly Dictionary<string, float> Lengths = new()
        {
            ["WPN_PST"] = 0.6f, ["WPN_SMG"] = 0.9f, ["WPN_DBS"] = 1.2f, ["WPN_BAT"] = 1.0f, ["WPN_IRF"] = 1.3f,
            ["WPN_MSG"] = 1.2f, ["WPN_HAM"] = 1.3f, ["WPN_ERF"] = 1.4f, ["WPN_ESG"] = 1.3f, ["WPN_EBL"] = 1.3f,
            ["PRJ_PLY"] = 0.4f, ["PRJ_ENM"] = 0.4f,
        };

        /// <summary>Prop cells (square), by prop name, plus the ground texture size.</summary>
        static readonly Dictionary<string, float> PropCells = new()
        {
            ["Tree"] = 5, ["Rock"] = 2.5f, ["Hedge"] = 3.5f, ["Stumps"] = 1.5f, ["LogPile"] = 2.5f, ["Excavator"] = 4,
            ["CableSpool"] = 1.5f, ["MachineLit"] = 3, ["CrateStack"] = 2.5f, ["PipeBarrier"] = 3.5f, ["Generator"] = 2.5f,
            ["Barricade"] = 3.5f, ["MachineBroken"] = 3, ["Sandbags"] = 3.5f, ["DeadTree"] = 4, ["LightPost"] = 3.5f,
            ["Kiosk"] = 3, ["BorderH"] = 4, ["BorderV"] = 1, ["Entrance"] = 3,
        };

        public static float GroundUnits = 8f;

        /// <summary>Width of one frame in game units (null = keep default scale, e.g. UI).</summary>
        public static float? FrameWidthUnits(ArtName n)
        {
            switch (n.Category)
            {
                case "WPN":
                case "PRJ":
                    return Lengths.TryGetValue(n.Key, out float len) ? len * 2f : 1f;
                case "ENV":
                    if (n.State == "ground") return GroundUnits;
                    if (n.State == "decor") return 1f;
                    return PropCells.TryGetValue(n.Name, out float cell) ? cell : 2f;
                case "ICO":
                case "UI":
                case "KEY":
                    return null;
                default:
                    return CellUnits.TryGetValue(n.Key, out float c) ? c : 2f;
            }
        }

        public static Vector2 Pivot(ArtName n) => n.Category switch
        {
            "CHR" or "ENM" or "BOS" or "PKP" => new Vector2(0.5f, 0.1f),
            "ENV" when n.State == "prop" => new Vector2(0.5f, 0.1f),
            "WPN" => new Vector2(0.25f, 0.5f),
            _ => new Vector2(0.5f, 0.5f),
        };

        /// <summary>
        /// Layout of an animation sheet of square cells: one row (a strip), or a grid read left to right, top to
        /// bottom when the image tool cannot make a very wide image (6 frames as 3 × 2 in a 3:2 image). The grid
        /// is the smallest one whose shape matches the image; unused cells at the end stay empty.
        /// </summary>
        public static (int cols, int rows) Grid(int frames, int width, int height)
        {
            if (frames <= 1 || height <= 0) return (1, 1);
            float aspect = width / (float)height;
            (int cols, int rows) best = (frames, 1);
            float bestError = Mathf.Abs(Mathf.Log(aspect / frames));
            for (int rows = 2; rows <= frames; rows++)
            {
                int cols = (frames + rows - 1) / rows;
                if ((cols * rows) - frames >= cols) continue; // a whole empty row
                float error = Mathf.Abs(Mathf.Log(aspect * rows / cols));
                if (error < bestError - 0.01f) { best = (cols, rows); bestError = error; }
            }
            return best;
        }

        /// <summary>Effects on black are blended additively: black becomes transparent.</summary>
        public static bool BlackIsTransparent(ArtName n) => n.Category == "FX" && n.Id == "GEN";
    }

    /// <summary>
    /// Configures every file dropped in Assets/Art/Incoming from its name: sprite import, pixels per unit so the
    /// cell matches its game size, pivot (feet for characters, grip for weapons), frame slicing for strips and grids,
    /// and removal of a flat magenta (#FF00FF) or black (effects) background.
    /// </summary>
    public sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRules.IncomingDir)) return;
            if (!ArtName.TryParse(assetPath, out var n)) { Debug.LogWarning($"[Hollow art] '{assetPath}' does not follow the naming convention; left as is."); return; }
            var imp = (TextureImporter)assetImporter;
            imp.GetSourceTextureWidthAndHeight(out int w, out int h);
            imp.textureType = n.Category is "UI" or "KEY" or "ICO" ? TextureImporterType.Sprite : TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.isReadable = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.maxTextureSize = 4096;

            int frames = n.State == "ground" ? 64 : n.Frames;
            var (cols, rows) = n.State == "ground" ? (1, 1) : ArtRules.Grid(frames, w, h);
            float frameWidthPx = w / (float)cols;
            float? units = ArtRules.FrameWidthUnits(n);
            imp.spritePixelsPerUnit = units.HasValue ? frameWidthPx / units.Value : 100f;

            var pivot = ArtRules.Pivot(n);
            if (frames <= 1)
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                imp.SetTextureSettings(settings);
                return;
            }

            imp.spriteImportMode = SpriteImportMode.Multiple;
            var rects = new List<SpriteRect>();
            if (n.State == "ground")
            {
                // A seamless 8 × 8-unit texture becomes 64 one-unit tiles.
                float tw = w / 8f, th = h / 8f;
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    rects.Add(Rect($"{n.Name}_{x}_{y}", new Rect(x * tw, y * th, tw, th), new Vector2(0.5f, 0.5f)));
            }
            else
            {
                float fw = w / (float)cols, fh = h / (float)rows;
                for (int i = 0; i < frames; i++)
                {
                    int col = i % cols, row = i / cols; // row 0 is the top row; texture y starts at the bottom
                    rects.Add(Rect($"{n.State}_{i}", new Rect(col * fw, h - (row + 1) * fh, fw, fh), pivot));
                }
            }
            var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(imp);
            provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(rects.ToArray());
            provider.Apply();
        }

        static SpriteRect Rect(string name, Rect r, Vector2 pivot) => new()
        {
            name = name, rect = r, alignment = SpriteAlignment.Custom, pivot = pivot, spriteID = GUID.Generate(),
        };

        /// <summary>
        /// Keys the source file itself (once): flat magenta becomes transparent, and black effect sheets get
        /// alpha from brightness. AI images are usually opaque RGB, so this must happen on the PNG, not later.
        /// Returns true when the file was rewritten (Unity then imports it again).
        /// </summary>
        static bool KeySourceFile(string path, ArtName n)
        {
            if (n.State == "ground" || n.Category == "KEY") return false;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(path))) return false;
                var px = tex.GetPixels32();
                bool changed = false;
                if (ArtRules.BlackIsTransparent(n))
                {
                    if (px[0].a < 255 && px[^1].a < 255) return false; // already has alpha
                    for (int i = 0; i < px.Length; i++)
                    {
                        byte a = Math.Max(px[i].r, Math.Max(px[i].g, px[i].b));
                        if (a < px[i].a) { px[i].a = a; changed = true; }
                    }
                }
                else if (IsMagenta(px[0]) || IsMagenta(px[^1]))
                {
                    for (int i = 0; i < px.Length; i++)
                    {
                        var c = px[i];
                        // Distance from pure magenta; near pixels vanish, edge pixels lose the pink spill.
                        int d = Math.Abs(255 - c.r) + c.g + Math.Abs(255 - c.b);
                        if (d < 90) { px[i].a = 0; changed = true; }
                        else if (d < 200)
                        {
                            px[i].a = (byte)Math.Min(c.a, (d - 90) * 255 / 110);
                            px[i].r = (byte)Math.Min(c.r, c.g + 40);
                            px[i].b = (byte)Math.Min(c.b, c.g + 40);
                            changed = true;
                        }
                    }
                }
                if (!changed) return false;
                tex.SetPixels32(px);
                System.IO.File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!path.StartsWith(ArtRules.IncomingDir) || !path.EndsWith(".png") || !ArtName.TryParse(path, out var n)) continue;
                if (KeySourceFile(path, n)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
        static bool IsMagenta(Color32 c) => c.r > 220 && c.g < 40 && c.b > 220;
    }
}
