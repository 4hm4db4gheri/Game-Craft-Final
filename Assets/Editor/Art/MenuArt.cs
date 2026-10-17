using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Builds the main-menu parallax art (menu backdrop). Kenney's CC0 "Background Elements Redux" pieces in
    /// <c>ArtSource/</c> are turned into grey-scale silhouettes (so each layer takes its dusk tint in the scene) and
    /// composed with trees; the industrial layers, fog, smoke, the AI helper and the vignette are drawn here.
    /// Deterministic: the same seed always gives the same files. Strips tile horizontally.
    /// </summary>
    public static class MenuArt
    {
        public const string Dir = "Assets/Art/Menu";
        const string KenneyDir = "ArtSource/Kenney_BackgroundElementsRedux";
        public const int PixelsPerUnit = 64;

        /// <summary>Where the skyline's chimneys, warning lights and lit windows are, in 0..1 of the factory strip.</summary>
        public sealed class Info
        {
            public readonly List<Vector2> Smoke = new(), Lights = new(), Windows = new();
        }

        [MenuItem("Hollow/Art/Generate Menu Backdrop")]
        static void GenerateMenu() => Debug.Log($"[Hollow art] menu backdrop written to {Dir}: {Generate().Smoke.Count} chimneys.");

        public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");

        public static Info Generate()
        {
            Directory.CreateDirectory(Dir);
            var info = new Info();
            var tiled = new List<string>();

            Save("Sky", Sky(), false);
            Save("Stars", Stars(), true); tiled.Add("Stars");
            Save("Moon", Silhouette(Kenney("moonFull"), 0.75f), false);
            Save("Mountains", Silhouette(Kenney("mountains"), 0.8f), true); tiled.Add("Mountains");
            Save("Clouds", FadeBottom(Silhouette(Kenney("cloudLayerB1"), 0.7f), 0.75f), true); tiled.Add("Clouds");
            Save("CloudsThin", FadeBottom(Silhouette(Kenney("cloudLayer2"), 0.7f), 0.85f), true); tiled.Add("CloudsThin");
            Save("Factory", PadBottom(Factory(info), 128, info), true); tiled.Add("Factory");
            Save("HillsFar", HillsWithTrees("hillsLarge", 11, dead: 0.3f, scale: 0.24f), true); tiled.Add("HillsFar");
            Save("Pylons", Pylons(), true); tiled.Add("Pylons");
            Save("HillsNear", HillsWithTrees("hills", 23, dead: 1f, scale: 0.5f), true); tiled.Add("HillsNear");
            Save("Fog", Fog(), true); tiled.Add("Fog");
            Save("Smoke", Smoke(), false);
            Save("Dot", Radial(32, 1.6f), false);
            Save("Glow", Radial(256, 2.2f), false);
            Save("AiShell", AiShell(), false);
            Save("AiCore", AiCore(), false);
            Save("Vignette", Vignette(), false);

            AssetDatabase.Refresh();
            foreach (var f in Directory.GetFiles(Dir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(f);
                Configure($"{Dir}/{name}.png", tiled.Contains(name), name == "Sky" || name == "Vignette" || name == "Fog");
            }
            AssetDatabase.SaveAssets();
            return info;
        }

        // ---------------------------------------------------------------- canvas

        /// <summary>Grey value + alpha painted with anti-aliased signed-distance shapes; x wraps when tiling.</summary>
        sealed class Canvas
        {
            public readonly int W, H;
            public readonly float[] A, L;
            public readonly Color[] Rgb; // only for the sky
            readonly bool wrap;

            public Canvas(int w, int h, bool wrap, bool rgb = false)
            {
                W = w; H = h; this.wrap = wrap;
                A = new float[w * h];
                L = new float[w * h];
                if (rgb) Rgb = new Color[w * h];
            }

            public void Plot(int x, int y, float cov, float lum)
            {
                if (cov <= 0f || y < 0 || y >= H) return;
                if (wrap) x = ((x % W) + W) % W;
                else if (x < 0 || x >= W) return;
                int i = y * W + x;
                L[i] = A[i] <= 0f ? lum : Mathf.Lerp(L[i], lum, cov);
                A[i] = Mathf.Max(A[i], cov);
            }

            /// <summary>Fills where <paramref name="sdf"/> (pixels, positive inside) covers the pixel centre.</summary>
            public void Fill(float x0, float y0, float x1, float y1, Func<float, float, float> sdf, float lum)
            {
                for (int y = Mathf.FloorToInt(y0) - 1; y <= Mathf.CeilToInt(y1) + 1; y++)
                for (int x = Mathf.FloorToInt(x0) - 1; x <= Mathf.CeilToInt(x1) + 1; x++)
                    Plot(x, y, Mathf.Clamp01(sdf(x + 0.5f, y + 0.5f) + 0.5f), lum);
            }

            public void Box(float x0, float y0, float x1, float y1, float lum = 1f) =>
                Fill(x0, y0, x1, y1, (x, y) => Mathf.Min(Mathf.Min(x - x0, x1 - x), Mathf.Min(y - y0, y1 - y)), lum);

            public void Disc(float cx, float cy, float r, float lum = 1f) =>
                Fill(cx - r, cy - r, cx + r, cy + r, (x, y) => r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)), lum);

            public void Line(float ax, float ay, float bx, float by, float width, float lum = 1f)
            {
                float h = width * 0.5f;
                Fill(Mathf.Min(ax, bx) - h, Mathf.Min(ay, by) - h, Mathf.Max(ax, bx) + h, Mathf.Max(ay, by) + h,
                    (x, y) => h - SegmentDistance(x, y, ax, ay, bx, by), lum);
            }

            /// <summary>Convex or concave polygon (points in order).</summary>
            public void Poly(Vector2[] p, float lum = 1f)
            {
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                foreach (var v in p) { x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y); }
                Fill(x0, y0, x1, y1, (x, y) =>
                {
                    bool inside = false;
                    float d = float.MaxValue;
                    for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                    {
                        if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x) inside = !inside;
                        d = Mathf.Min(d, SegmentDistance(x, y, p[i].x, p[i].y, p[j].x, p[j].y));
                    }
                    return inside ? d : -d;
                }, lum);
            }

            /// <summary>Alpha-composites a source image (grey = its luminance mapped to <paramref name="minLum"/>..1).</summary>
            public void Stamp(Color[] src, int sw, int sh, float px, float py, float scale, float minLum, float lumRange)
            {
                int dw = Mathf.CeilToInt(sw * scale), dh = Mathf.CeilToInt(sh * scale);
                for (int y = 0; y < dh; y++)
                for (int x = 0; x < dw; x++)
                {
                    var c = Bilinear(src, sw, sh, (x + 0.5f) / scale - 0.5f, (y + 0.5f) / scale - 0.5f);
                    if (c.a <= 0.01f) continue;
                    float lum = Mathf.Lerp(minLum, 1f, Mathf.Clamp01(Luma(c) / lumRange));
                    Plot(Mathf.RoundToInt(px) + x, Mathf.RoundToInt(py) + y, c.a, lum);
                }
            }

            /// <summary>Highest covered row in a column, or -1.</summary>
            public int Top(int x)
            {
                x = ((x % W) + W) % W;
                for (int y = H - 1; y >= 0; y--) if (A[y * W + x] > 0.5f) return y;
                return -1;
            }
        }

        static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / Mathf.Max(1e-6f, abx * abx + aby * aby));
            float dx = px - (ax + abx * t), dy = py - (ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        static Color Bilinear(Color[] src, int w, int h, float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            Color S(int ix, int iy) => ix < 0 || iy < 0 || ix >= w || iy >= h ? Color.clear : src[iy * w + ix];
            var top = Color.Lerp(S(x0, y0 + 1), S(x0 + 1, y0 + 1), fx);
            var bottom = Color.Lerp(S(x0, y0), S(x0 + 1, y0), fx);
            return Color.Lerp(bottom, top, fy);
        }

        // ---------------------------------------------------------------- Kenney silhouettes

        sealed class Image
        {
            public Color[] Px; public int W, H;
        }

        static Image Kenney(string name)
        {
            string path = $"{KenneyDir}/{name}.png";
            if (!File.Exists(path)) throw new FileNotFoundException($"Menu art source missing: {path}");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            var img = new Image { Px = tex.GetPixels(), W = tex.width, H = tex.height };
            UnityEngine.Object.DestroyImmediate(tex);
            return img;
        }

        static float MaxLuma(Image img)
        {
            float m = 0.01f;
            foreach (var c in img.Px) if (c.a > 0.5f) m = Mathf.Max(m, Luma(c));
            return m;
        }

        /// <summary>The source shape in grey: its own light/dark detail kept between <paramref name="minLum"/> and white.</summary>
        static Canvas Silhouette(Image img, float minLum)
        {
            var c = new Canvas(img.W, img.H, wrap: false);
            c.Stamp(img.Px, img.W, img.H, 0, 0, 1f, minLum, MaxLuma(img));
            return c;
        }

        /// <summary>A Kenney hill strip with trees standing on its crest; trees wrap so the strip still tiles.</summary>
        static Canvas HillsWithTrees(string hills, int seed, float dead, float scale)
        {
            var src = Kenney(hills);
            var pine = Kenney("treePine");
            var deadTree = Kenney("treeDead");
            var tall = Kenney("treeLong");
            var bush = Kenney("bush1");
            // Room above the crest for the tallest tree at its largest size.
            int headroom = Mathf.CeilToInt(Mathf.Max(pine.H, Mathf.Max(deadTree.H, tall.H)) * scale * 1.2f) + 8;
            var c = new Canvas(src.W, src.H + headroom, wrap: true);
            c.Stamp(src.Px, src.W, src.H, 0, 0, 1f, 0.8f, MaxLuma(src));
            var crest = new int[src.W];
            for (int x = 0; x < src.W; x++) crest[x] = c.Top(x);

            var rnd = new System.Random(seed);
            float pineMax = MaxLuma(pine), deadMax = MaxLuma(deadTree), tallMax = MaxLuma(tall), bushMax = MaxLuma(bush);
            float x0 = (float)rnd.NextDouble() * 40f;
            while (x0 < src.W)
            {
                bool isDead = rnd.NextDouble() < dead;
                var t = isDead ? deadTree : rnd.NextDouble() < 0.3 ? tall : pine;
                float s = scale * (0.7f + (float)rnd.NextDouble() * 0.5f);
                float max = t == deadTree ? deadMax : t == tall ? tallMax : pineMax;
                int baseX = Mathf.RoundToInt(x0 + t.W * s * 0.5f);
                int ground = crest[((baseX % src.W) + src.W) % src.W];
                c.Stamp(t.Px, t.W, t.H, x0, ground - 10 * s, s, 0.82f, max);
                if (rnd.NextDouble() < 0.35)
                {
                    float bs = scale * 0.8f;
                    int bx = Mathf.RoundToInt(x0 + t.W * s);
                    c.Stamp(bush.Px, bush.W, bush.H, bx, crest[((bx % src.W) + src.W) % src.W] - 6 * bs, bs, 0.85f, bushMax);
                }
                x0 += t.W * s * (0.9f + (float)rnd.NextDouble() * 2.6f);
            }
            return c;
        }

        /// <summary>Solid ground under the skyline so the bright horizon never shows between it and the hills.</summary>
        static Canvas PadBottom(Canvas src, int rows, Info info)
        {
            var c = new Canvas(src.W, src.H + rows, wrap: true);
            for (int i = 0; i < rows * c.W; i++) { c.A[i] = 1f; c.L[i] = 1f; }
            Array.Copy(src.A, 0, c.A, rows * c.W, src.A.Length);
            Array.Copy(src.L, 0, c.L, rows * c.W, src.L.Length);
            foreach (var list in new[] { info.Smoke, info.Lights, info.Windows })
                for (int i = 0; i < list.Count; i++) list[i] = new Vector2(list[i].x, (list[i].y * src.H + rows) / c.H);
            return c;
        }

        /// <summary>Kenney's cloud bands are solid down to the bottom edge; fade them so no straight line shows.</summary>
        static Canvas FadeBottom(Canvas c, float portion)
        {
            for (int y = 0; y < c.H; y++)
            {
                float f = Mathf.SmoothStep(0f, 1f, y / (c.H * portion));
                for (int x = 0; x < c.W; x++) c.A[y * c.W + x] *= f;
            }
            return c;
        }

        // ---------------------------------------------------------------- drawn layers

        static Canvas Sky()
        {
            var c = new Canvas(8, 512, wrap: false, rgb: true);
            var keys = new[]
            {
                (0.00f, new Color(0.98f, 0.62f, 0.36f)),
                (0.22f, new Color(0.78f, 0.36f, 0.30f)),
                (0.40f, new Color(0.38f, 0.18f, 0.30f)),
                (0.65f, new Color(0.14f, 0.10f, 0.22f)),
                (1.00f, new Color(0.04f, 0.05f, 0.11f)),
            };
            var rnd = new System.Random(3);
            for (int y = 0; y < c.H; y++)
            {
                float v = y / (c.H - 1f);
                int k = 1;
                while (k < keys.Length - 1 && v > keys[k].Item1) k++;
                float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(keys[k - 1].Item1, keys[k].Item1, v));
                var col = Color.Lerp(keys[k - 1].Item2, keys[k].Item2, t);
                for (int x = 0; x < c.W; x++)
                {
                    float dither = ((float)rnd.NextDouble() - 0.5f) / 255f; // no banding on the long gradient
                    c.Rgb[y * c.W + x] = new Color(col.r + dither, col.g + dither, col.b + dither, 1f);
                    c.A[y * c.W + x] = 1f;
                }
            }
            return c;
        }

        static Canvas Stars()
        {
            var c = new Canvas(1024, 512, wrap: true);
            var rnd = new System.Random(17);
            for (int i = 0; i < 260; i++)
            {
                float x = (float)rnd.NextDouble() * c.W;
                float v = Mathf.Pow((float)rnd.NextDouble(), 0.6f); // more stars higher up
                float r = 0.6f + (float)rnd.NextDouble() * (rnd.NextDouble() < 0.08 ? 1.8f : 0.9f);
                c.Disc(x, v * c.H, r, 0.6f + (float)rnd.NextDouble() * 0.4f);
            }
            for (int i = 0; i < c.A.Length; i++) c.A[i] *= Mathf.Clamp01((i / c.W) / (c.H * 0.35f)); // fade near the horizon
            return c;
        }

        /// <summary>Night factory skyline: blocks, saw-tooth halls, chimneys, cooling towers, tanks, a crane and pipes.</summary>
        static Canvas Factory(Info info)
        {
            var c = new Canvas(2048, 512, wrap: true);
            var rnd = new System.Random(2026);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            c.Box(0, 0, c.W, 36);

            // Low continuous blocks so no sky shows at the bottom.
            float x = 0;
            while (x < c.W)
            {
                float w = R(70, 210), h = R(60, 165);
                c.Box(x, 0, x + w, h, R(0.88f, 1f));
                double roof = rnd.NextDouble();
                if (roof < 0.3)
                {
                    for (float tx = x; tx + 26 <= x + w; tx += 30)
                        c.Poly(new[] { new Vector2(tx, h - 1), new Vector2(tx + 26, h - 1), new Vector2(tx + 26, h + 22) });
                }
                else if (roof < 0.55) c.Box(x + w * 0.2f, h - 1, x + w * 0.7f, h + R(20, 50), 0.92f);
                else c.Box(x + w * R(0.1f, 0.6f), h - 1, x + w * 0.85f, h + 10, 0.9f);
                int lit = rnd.Next(0, 4);
                for (int i = 0; i < lit; i++) info.Windows.Add(new Vector2((x + R(8, w - 8)) / c.W, R(18, h - 12) / c.H));
                x += w + R(-10, 14);
            }

            // Landmarks on top, spaced so the silhouette stays readable.
            x = R(40, 120);
            int kind = 0;
            while (x < c.W - 120)
            {
                switch (kind++ % 5)
                {
                    case 0: // chimney cluster
                    {
                        int n = rnd.Next(2, 4);
                        for (int i = 0; i < n; i++)
                        {
                            float cx = x + i * R(34, 52), w = R(20, 32), h = R(250, 410);
                            c.Poly(new[] { new Vector2(cx - w * 0.5f, 0), new Vector2(cx + w * 0.5f, 0), new Vector2(cx + w * 0.38f, h), new Vector2(cx - w * 0.38f, h) });
                            c.Box(cx - w * 0.48f, h * 0.84f, cx + w * 0.48f, h * 0.84f + 8, 0.82f);
                            c.Box(cx - w * 0.44f, h - 6, cx + w * 0.44f, h + 2, 0.86f);
                            info.Smoke.Add(new Vector2(cx / c.W, (h + 2) / c.H));
                            info.Lights.Add(new Vector2(cx / c.W, (h - 14) / c.H));
                        }
                        x += n * 50 + R(160, 260);
                        break;
                    }
                    case 1: // cooling tower
                    {
                        float cx = x + 80, bw = R(70, 90), h = R(200, 260);
                        c.Fill(cx - bw, 0, cx + bw, h, (px, py) =>
                        {
                            float t = py / h;
                            float half = bw * (1f - 0.42f * Mathf.Sin(t * Mathf.PI * 0.85f)); // waisted hyperboloid
                            return Mathf.Min(half - Mathf.Abs(px - cx), Mathf.Min(py, h - py));
                        }, 0.95f);
                        info.Smoke.Add(new Vector2(cx / c.W, h / c.H));
                        x += bw * 2 + R(120, 200);
                        break;
                    }
                    case 2: // storage tanks
                    {
                        int n = rnd.Next(2, 4);
                        for (int i = 0; i < n; i++)
                        {
                            float cx = x + i * 74, r = R(30, 38), h = R(110, 150);
                            c.Box(cx - r, 0, cx + r, h, 0.93f);
                            c.Disc(cx, h, r, 0.93f);
                            c.Line(cx - r - 4, h * 0.55f, cx + r + 4, h * 0.55f, 4, 0.8f);
                        }
                        x += n * 74 + R(120, 200);
                        break;
                    }
                    case 3: // gantry crane
                    {
                        float h = R(300, 360), w = 150;
                        c.Line(x, 0, x, h, 8); c.Line(x + 26, 0, x + 26, h, 8);
                        for (float y = 20; y < h - 20; y += 40) { c.Line(x, y, x + 26, y + 40, 3); c.Line(x + 26, y, x, y + 40, 3); }
                        c.Line(x - 60, h, x + w, h, 10);
                        c.Line(x + 13, h + 50, x - 60, h, 3); c.Line(x + 13, h + 50, x + w, h, 3);
                        c.Line(x + w - 30, h, x + w - 30, h - 110, 2);
                        c.Box(x + w - 42, h - 130, x + w - 18, h - 110, 0.9f);
                        info.Lights.Add(new Vector2((x + 13) / c.W, (h + 50) / c.H));
                        x += w + R(140, 220);
                        break;
                    }
                    default: // office tower with antenna
                    {
                        float w = R(60, 90), h = R(220, 300);
                        c.Box(x, 0, x + w, h, 0.97f);
                        c.Box(x + w * 0.2f, h, x + w * 0.8f, h + 24, 0.9f);
                        c.Line(x + w * 0.5f, h + 24, x + w * 0.5f, h + 110, 4);
                        info.Lights.Add(new Vector2((x + w * 0.5f) / c.W, (h + 110) / c.H));
                        for (int i = 0; i < 6; i++) info.Windows.Add(new Vector2((x + R(10, w - 10)) / c.W, R(40, h - 20) / c.H));
                        x += w + R(140, 220);
                        break;
                    }
                }
            }

            // Pipe runs between the buildings.
            for (int i = 0; i < 5; i++)
            {
                float px = R(0, c.W), len = R(220, 520), py = R(70, 130);
                c.Line(px, py, px + len, py, 7, 0.9f);
                c.Line(px + len, py, px + len, R(20, 60), 7, 0.9f);
                for (float s = px + 40; s < px + len; s += 90) c.Line(s, py, s, 30, 3, 0.85f);
            }
            return c;
        }

        /// <summary>Lattice pylons with sagging cables; four spans so the strip joins itself.</summary>
        static Canvas Pylons()
        {
            var c = new Canvas(2048, 512, wrap: true);
            const int Spans = 4;
            float span = c.W / (float)Spans;
            var arms = new[] { 0.74f, 0.88f };
            for (int i = 0; i < Spans; i++)
            {
                float cx = span * i + span * 0.5f, h = 420, baseHalf = 46, topHalf = 12;
                float Lx(float t) => cx - Mathf.Lerp(baseHalf, topHalf, t);
                float Rx(float t) => cx + Mathf.Lerp(baseHalf, topHalf, t);
                c.Line(Lx(0), 0, Lx(1), h, 6); c.Line(Rx(0), 0, Rx(1), h, 6);
                for (float t = 0; t < 0.95f; t += 0.12f)
                {
                    float t2 = t + 0.12f;
                    c.Line(Lx(t), t * h, Rx(t2), t2 * h, 2.5f); c.Line(Rx(t), t * h, Lx(t2), t2 * h, 2.5f);
                    c.Line(Lx(t), t * h, Rx(t), t * h, 2.5f);
                }
                c.Poly(new[] { new Vector2(cx - topHalf, h), new Vector2(cx + topHalf, h), new Vector2(cx, h + 46) });
                foreach (float a in arms)
                {
                    float y = a * h, reach = a > 0.8f ? 70 : 96;
                    c.Line(cx - reach, y, cx + reach, y, 6);
                    c.Line(cx - reach, y, cx - topHalf, y + 30, 2.5f); c.Line(cx + reach, y, cx + topHalf, y + 30, 2.5f);
                    // Insulators, then cables to the next pylon (catenary approximated by a parabola).
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float ix = cx + side * (reach - 6);
                        c.Line(ix, y, ix, y - 18, 4);
                        float sag = a > 0.8f ? 60 : 80;
                        Vector2 prev = new(ix, y - 18);
                        for (int k = 1; k <= 48; k++)
                        {
                            float t = k / 48f;
                            var p = new Vector2(ix + span * t, y - 18 - sag * 4f * t * (1f - t));
                            c.Line(prev.x, prev.y, p.x, p.y, 2f);
                            prev = p;
                        }
                    }
                }
            }
            return c;
        }

        /// <summary>A soft horizontal band; only whole sine periods along x so it tiles.</summary>
        static Canvas Fog()
        {
            var c = new Canvas(1024, 256, wrap: true);
            const float Tau = Mathf.PI * 2f;
            for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++)
            {
                float u = x / (float)c.W, v = y / (float)c.H;
                float n = 0.55f + 0.2f * Mathf.Sin(Tau * (2 * u) + 0.7f) + 0.15f * Mathf.Sin(Tau * (5 * u) + v * 3f)
                          + 0.1f * Mathf.Sin(Tau * (11 * u) + 2.3f + v * 5f);
                float profile = Mathf.Pow(Mathf.Sin(Mathf.PI * v), 1.6f) * Mathf.Clamp01(1.3f - v);
                c.A[y * c.W + x] = Mathf.Clamp01(n * profile);
                c.L[y * c.W + x] = 1f;
            }
            return c;
        }

        static Canvas Smoke()
        {
            var c = new Canvas(128, 128, wrap: false);
            var rnd = new System.Random(5);
            for (int i = 0; i < 9; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2, d = (float)rnd.NextDouble() * 22f;
                float cx = 64 + Mathf.Cos(a) * d, cy = 64 + Mathf.Sin(a) * d, r = 20 + (float)rnd.NextDouble() * 16;
                for (int y = 0; y < c.H; y++)
                for (int x = 0; x < c.W; x++)
                {
                    float f = Mathf.Clamp01(1f - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r);
                    int idx = y * c.W + x;
                    c.A[idx] = Mathf.Max(c.A[idx], f * f * (3 - 2 * f) * 0.8f);
                    c.L[idx] = 1f;
                }
            }
            return c;
        }

        static Canvas Radial(int size, float power)
        {
            var c = new Canvas(size, size, wrap: false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                c.A[y * size + x] = Mathf.Pow(Mathf.Clamp01(1f - d), power);
                c.L[y * size + x] = 1f;
            }
            return c;
        }

        /// <summary>The little AI helper's body: a round shell with fins, an antenna and a stubby pistol arm.</summary>
        static Canvas AiShell()
        {
            var c = new Canvas(256, 256, wrap: false);
            const float cx = 128, cy = 124;
            c.Poly(new[] { new Vector2(cx - 62, cy + 10), new Vector2(cx - 98, cy + 40), new Vector2(cx - 84, cy - 14) }, 0.7f);
            c.Poly(new[] { new Vector2(cx + 62, cy + 10), new Vector2(cx + 98, cy + 40), new Vector2(cx + 84, cy - 14) }, 0.7f);
            c.Line(cx + 10, cy + 60, cx + 22, cy + 100, 5, 0.8f);  // antenna
            c.Disc(cx + 23, cy + 104, 7, 1f);
            c.Line(cx - 30, cy - 30, cx - 66, cy - 70, 9, 0.6f);   // arm, under the shell
            c.Box(cx - 106, cy - 84, cx - 58, cy - 64, 0.75f);     // compact pistol
            c.Box(cx - 74, cy - 100, cx - 62, cy - 82, 0.7f);
            c.Disc(cx, cy, 70, 0.62f);                 // shadow side
            c.Disc(cx - 6, cy + 6, 64, 0.86f);          // lit side
            c.Disc(cx, cy, 44, 0.32f);                  // socket the core sits in
            return c;
        }

        static Canvas AiCore()
        {
            var c = new Canvas(128, 128, wrap: false);
            c.Disc(64, 64, 40, 0.75f);
            c.Disc(64, 64, 26, 1f);
            c.Disc(56, 74, 8, 1f);
            return c;
        }

        static Canvas Vignette()
        {
            var c = new Canvas(256, 256, wrap: false);
            for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++)
            {
                float dx = (x + 0.5f) / c.W * 2 - 1, dy = (y + 0.5f) / c.H * 2 - 1;
                float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                c.A[y * c.W + x] = Mathf.Clamp01((d - 0.55f) / 0.75f) * 0.85f;
                c.L[y * c.W + x] = 0f;
            }
            return c;
        }

        // ---------------------------------------------------------------- output

        static void Save(string name, Canvas c, bool tiles)
        {
            var px = new Color32[c.W * c.H];
            for (int i = 0; i < px.Length; i++)
            {
                if (c.Rgb != null) { px[i] = c.Rgb[i]; continue; }
                byte l = (byte)(Mathf.Clamp01(c.L[i]) * 255f);
                px[i] = new Color32(l, l, l, (byte)(Mathf.Clamp01(c.A[i]) * 255f));
            }
            var tex = new Texture2D(c.W, c.H, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            File.WriteAllBytes($"{Dir}/{name}.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static void Configure(string path, bool tiles, bool uncompressed)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = PixelsPerUnit;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapModeU = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.wrapModeV = TextureWrapMode.Clamp; // strips repeat sideways only, or the top row bleeds into the bottom edge
            imp.alphaIsTransparency = true;
            imp.maxTextureSize = 2048;
            imp.textureCompression = uncompressed ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // tiled draw mode needs a full rect
            settings.spriteAlignment = (int)(tiles ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
        }
    }
}
