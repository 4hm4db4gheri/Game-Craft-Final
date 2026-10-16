using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Generates the simple white placeholder shapes used until AI art arrives (final rule 3: generated
    /// simple textures only). Every sprite is 128 px per unit and tinted per entity in its visual prefab.
    /// </summary>
    public static class PlaceholderArt
    {
        public const string Dir = "Assets/Art/Placeholders/Sprites";
        public const int PixelsPerUnit = 128;

        public static string PathOf(string name) => $"{Dir}/{name}.png";

        [MenuItem("Hollow/Setup/Generate Placeholder Sprites")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Dir);
            Write("PH_Circle", 128, 128, (u, v) => Disc(u, v, 0.5f, 0.5f, 0.48f));
            Write("PH_Square", 128, 128, (u, v) => 1f);
            Write("PH_Ring", 128, 128, (u, v) => Mathf.Min(Disc(u, v, .5f, .5f, .48f), 1f - Disc(u, v, .5f, .5f, .40f)));
            Write("PH_Capsule", 128, 256, (u, v) => Capsule(u, v));
            Write("PH_Triangle", 128, 128, (u, v) => Triangle(u, v));
            Write("PH_Diamond", 128, 128, (u, v) => Mathf.Clamp01((0.48f - (Mathf.Abs(u - .5f) + Mathf.Abs(v - .5f))) * 64f));
            Write("PH_Shadow", 128, 64, (u, v) => SoftEllipse(u, v) * 0.55f, black: true);
            Write("PH_SoftGlow", 128, 128, (u, v) => Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2f), 2f));
            Write("PH_Reticle", 128, 128, (u, v) => Reticle(u, v));
            Write("PH_Bar", 64, 16, (u, v) => 1f);
            // Arena (T16): ground tiles are grey-scale so a tint gives each phase its own material.
            WriteGray("PH_Speckle", 128, 128, Speckle);
            WriteGray("PH_Plate", 128, 128, Plate);
            Write("PH_Tuft", 128, 128, Tuft);
            Write("PH_Blob", 128, 128, Blob);
            Write("PH_Crack", 128, 128, Crack);
            AssetDatabase.Refresh();
            foreach (var f in Directory.GetFiles(Dir, "*.png")) ConfigureImporter(f.Replace('\\', '/'));
            AssetDatabase.SaveAssets();
        }

        public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(PathOf(name));

        static void Write(string name, int w, int h, Func<float, float, float> alpha, bool black = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = Mathf.Clamp01(alpha((x + 0.5f) / w, (y + 0.5f) / h));
                byte c = black ? (byte)0 : (byte)255;
                px[y * w + x] = new Color32(c, c, c, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(PathOf(name), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static void WriteGray(string name, int w, int h, Func<float, float, float> luminance)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte c = (byte)(Mathf.Clamp01(luminance((x + 0.5f) / w, (y + 0.5f) / h)) * 255);
                px[y * w + x] = new Color32(c, c, c, 255);
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(PathOf(name), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static void ConfigureImporter(string path)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = PixelsPerUnit;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }

        static float Edge(float signedDistancePx) => Mathf.Clamp01(signedDistancePx + 0.5f);

        static float Disc(float u, float v, float cx, float cy, float r) =>
            Edge((r - Vector2.Distance(new Vector2(u, v), new Vector2(cx, cy))) * 128f);

        static float Capsule(float u, float v)
        {
            // 128x256: circle radius 0.48 (in u) at both ends.
            float x = (u - 0.5f) * 128f, y = v * 256f;
            float r = 61f;
            float cy = Mathf.Clamp(y, 64f, 192f);
            return Edge(r - new Vector2(x, y - cy).magnitude);
        }

        static float Triangle(float u, float v)
        {
            // Points right: apex (0.95, 0.5), base x = 0.1.
            float halfHeight = (0.95f - u) / 0.85f * 0.42f;
            if (u < 0.1f || u > 0.95f) return 0f;
            return Edge((halfHeight - Mathf.Abs(v - 0.5f)) * 128f);
        }

        static float SoftEllipse(float u, float v)
        {
            float dx = (u - 0.5f) / 0.5f, dy = (v - 0.5f) / 0.5f;
            return Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)) * 1.6f;
        }

        /// <summary>Seamless soft grain: only whole sine periods per tile, so neighbouring tiles join.</summary>
        static float Speckle(float u, float v)
        {
            const float Tau = Mathf.PI * 2f;
            float n = Mathf.Sin(Tau * (3 * u + 2 * v)) * 0.35f + Mathf.Sin(Tau * (5 * u - 4 * v) + 1.3f) * 0.25f
                      + Mathf.Sin(Tau * (11 * u + 7 * v) + 0.4f) * 0.2f + Mathf.Sin(Tau * (17 * u - 13 * v) + 2.1f) * 0.2f;
            return 0.86f + n * 0.1f;
        }

        /// <summary>Metal floor plate: dark seams on the edges, a light top-left bevel and four rivets.</summary>
        static float Plate(float u, float v)
        {
            float px = u * 128f, py = v * 128f;
            if (px < 3 || py < 3 || px > 125 || py > 125) return 0.45f;
            if (px < 6 || py > 122) return 1f;
            foreach (var c in new[] { new Vector2(14, 14), new Vector2(114, 14), new Vector2(14, 114), new Vector2(114, 114) })
                if (Vector2.Distance(new Vector2(px, py), c) < 4f) return 0.6f;
            return 0.82f + Speckle(u, v) * 0.1f;
        }

        static float Tuft(float u, float v)
        {
            float a = 0f;
            float[] baseX = { 0.3f, 0.4f, 0.5f, 0.58f, 0.68f };
            float[] lean = { -0.18f, -0.06f, 0.02f, 0.1f, 0.2f };
            float[] height = { 0.55f, 0.8f, 0.95f, 0.75f, 0.5f };
            for (int i = 0; i < baseX.Length; i++)
            {
                float t = (v - 0.05f) / height[i];
                if (t < 0 || t > 1) continue;
                float x = baseX[i] + lean[i] * t;
                float half = 0.035f * (1f - t);
                a = Mathf.Max(a, Edge((half - Mathf.Abs(u - x)) * 128f));
            }
            return a;
        }

        static float Blob(float u, float v)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float ang = Mathf.Atan2(dy, dx);
            float r = 0.36f + Mathf.Sin(ang * 3f) * 0.05f + Mathf.Sin(ang * 5f + 1f) * 0.03f;
            return Mathf.Clamp01((r - Mathf.Sqrt(dx * dx + dy * dy)) * 18f);
        }

        static float Crack(float u, float v)
        {
            Vector2[] pts = { new(0.05f, 0.55f), new(0.25f, 0.45f), new(0.42f, 0.6f), new(0.6f, 0.42f), new(0.78f, 0.52f), new(0.95f, 0.4f) };
            Vector2[] branch = { new(0.42f, 0.6f), new(0.5f, 0.82f) };
            var p = new Vector2(u, v);
            float d = Mathf.Min(PolylineDistance(p, pts), PolylineDistance(p, branch));
            return Edge((0.012f - d) * 128f);
        }

        static float PolylineDistance(Vector2 p, Vector2[] pts)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static float Reticle(float u, float v)
        {
            float ring = Mathf.Min(Disc(u, v, .5f, .5f, .40f), 1f - Disc(u, v, .5f, .5f, .35f));
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f));
            float cross = (Mathf.Abs(u - .5f) < 0.02f || Mathf.Abs(v - .5f) < 0.02f) && d > 0.12f && d < 0.48f ? 1f : 0f;
            float dot = Disc(u, v, .5f, .5f, 0.03f);
            return Mathf.Max(ring, Mathf.Max(cross, dot));
        }
    }
}
