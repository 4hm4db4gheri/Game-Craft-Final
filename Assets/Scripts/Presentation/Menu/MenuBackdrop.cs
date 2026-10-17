using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollow.Presentation.Menu
{
    /// <summary>
    /// The main menu's 2D parallax backdrop: sprite layers drawn by the menu camera behind the UI canvas. Each layer
    /// shifts with the pointer by its depth, tiled strips drift sideways and loop, and the factory strip carries
    /// smoke, blinking warning lights and lit windows. Presentation only; numbers here are look, not gameplay.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        [Serializable]
        public sealed class Layer
        {
            public string Name;
            public Sprite Sprite;
            public Color Tint = Color.white;
            [Tooltip("0 = fixed to the screen, 1 = follows the pointer the most.")]
            public float Depth;
            [Tooltip("Tiled: y of the bottom edge (x unused). Single: centre.")]
            public Vector2 Position;
            [Tooltip("World height; width follows the sprite.")]
            public float Height = 1f;
            [Tooltip("Tiled: drift to the left in world units per second.")]
            public float Scroll;
            public bool Tiled = true;
            [Tooltip("Stretched over the whole view (sky, vignette).")]
            public bool FillScreen;
            public int Order;
            public float Bob, BobSpeed = 1f;
            [Tooltip("Alpha breathing, 0..1 of the tint alpha.")]
            public float Pulse, PulseSpeed = 1f;
        }

        [SerializeField] Camera view;
        [SerializeField] Material material;
        [SerializeField] Layer[] layers = Array.Empty<Layer>();
        [SerializeField] float pointerShift = 1.1f;
        [SerializeField] float pointerSmoothing = 2.5f;

        [Header("Factory strip")]
        [SerializeField] string factoryLayer = "Factory";
        [SerializeField] Vector2[] smokePoints = Array.Empty<Vector2>();
        [SerializeField] Vector2[] lightPoints = Array.Empty<Vector2>();
        [SerializeField] Vector2[] windowPoints = Array.Empty<Vector2>();
        [SerializeField] Sprite smokeSprite, dotSprite, glowSprite;
        [SerializeField] Color smokeColor = new(0.32f, 0.26f, 0.32f, 0.45f);
        [SerializeField] Color lightColor = new(1f, 0.25f, 0.2f, 1f);
        [SerializeField] Color windowColor = new(1f, 0.74f, 0.36f, 0.9f);

        [Header("Drifting embers")]
        [SerializeField] int emberCount = 36;
        [SerializeField] Color emberColor = new(1f, 0.62f, 0.32f, 0.8f);
        [SerializeField] float emberDepth = 0.65f;
        [SerializeField] int emberOrder = -55;

        sealed class Built
        {
            public Layer Data;
            public Transform Root, Strip;
            public readonly List<SpriteRenderer> Copies = new();
            public float TileWidth, Scroll, Scale;
        }

        struct Puff { public SpriteRenderer R; public float Age, Life; public Vector2 Velocity; }
        struct Ember { public SpriteRenderer R; public float Age, Life, Phase; public Vector2 Velocity; }
        struct Blinker { public SpriteRenderer R; public float Phase, Rate, Alpha; public bool Window; }

        readonly List<Built> built = new();
        readonly List<Blinker> blinkers = new();
        Puff[] puffs = Array.Empty<Puff>();
        Ember[] embers = Array.Empty<Ember>();
        Built factory;
        Transform emberRoot;
        float[] nextPuff = Array.Empty<float>();
        Vector2 pointer;
        System.Random rnd;

        /// <summary>Scene builder entry point (Hollow > Setup > Rebuild Main Menu Scene).</summary>
        public void Configure(Camera camera, Material spriteMaterial, Layer[] backdropLayers, Sprite smoke, Sprite dot, Sprite glow,
            Vector2[] smokeAt, Vector2[] lightsAt, Vector2[] windowsAt)
        {
            view = camera;
            material = spriteMaterial;
            layers = backdropLayers;
            smokeSprite = smoke; dotSprite = dot; glowSprite = glow;
            smokePoints = smokeAt; lightPoints = lightsAt; windowPoints = windowsAt;
        }

        void Start()
        {
            if (view == null) view = Camera.main;
            rnd = new System.Random(11);
            foreach (var layer in layers) if (layer.Sprite != null) built.Add(Build(layer));
            factory = built.Find(b => b.Data.Name == factoryLayer);
            if (factory != null) BuildFactoryDetails();
            BuildEmbers();
        }

        // ---------------------------------------------------------------- build

        SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            if (material != null) r.sharedMaterial = material;
            return r;
        }

        Built Build(Layer layer)
        {
            var b = new Built { Data = layer, Root = new GameObject("Layer " + layer.Name).transform };
            b.Root.SetParent(transform, false);
            float spriteHeight = layer.Sprite.bounds.size.y;
            b.Scale = layer.Height / Mathf.Max(0.0001f, spriteHeight);
            if (!layer.Tiled || layer.FillScreen)
            {
                var r = Renderer(b.Root, layer.Name, layer.Sprite, layer.Tint, layer.Order);
                r.transform.localScale = Vector3.one * b.Scale;
                b.Copies.Add(r);
                return b;
            }
            b.Strip = new GameObject("Strip").transform;
            b.Strip.SetParent(b.Root, false);
            b.TileWidth = layer.Sprite.bounds.size.x * b.Scale;
            // Enough copies for an ultra-wide view plus the pointer shift on both sides.
            float widest = view.orthographicSize * 2f * 2.6f + pointerShift * 2f;
            int count = Mathf.CeilToInt(widest / b.TileWidth) + 2;
            for (int i = 0; i < count; i++)
            {
                var r = Renderer(b.Strip, $"{layer.Name} {i}", layer.Sprite, layer.Tint, layer.Order);
                r.transform.localScale = Vector3.one * b.Scale;
                r.transform.localPosition = new Vector3((i - count / 2) * b.TileWidth, 0, 0);
                b.Copies.Add(r);
            }
            return b;
        }

        Vector2 OnTile(Vector2 uv) => new((uv.x - 0.5f) * factory.TileWidth, uv.y * factory.Data.Height);

        void BuildFactoryDetails()
        {
            int order = factory.Data.Order;
            foreach (var copy in factory.Copies)
            {
                for (int i = 0; i < lightPoints.Length; i++)
                {
                    var p = OnTile(lightPoints[i]) / factory.Scale;
                    var glow = Renderer(copy.transform, "Light glow", glowSprite, lightColor * new Color(1, 1, 1, 0.35f), order + 2);
                    glow.transform.localPosition = p;
                    glow.transform.localScale = Vector3.one * (0.5f / factory.Scale);
                    var dot = Renderer(copy.transform, "Light", dotSprite, lightColor, order + 3);
                    dot.transform.localPosition = p;
                    dot.transform.localScale = Vector3.one * (0.12f / factory.Scale);
                    float phase = (float)rnd.NextDouble() * 6f, rate = 0.6f + (float)rnd.NextDouble() * 0.5f;
                    blinkers.Add(new Blinker { R = glow, Phase = phase, Rate = rate, Alpha = 0.35f });
                    blinkers.Add(new Blinker { R = dot, Phase = phase, Rate = rate, Alpha = 1f });
                }
                for (int i = 0; i < windowPoints.Length; i++)
                {
                    var w = Renderer(copy.transform, "Window", dotSprite, windowColor, order + 1);
                    w.transform.localPosition = OnTile(windowPoints[i]) / factory.Scale;
                    w.transform.localScale = new Vector3(0.09f, 0.12f, 1f) / factory.Scale;
                    blinkers.Add(new Blinker { R = w, Phase = (float)rnd.NextDouble() * 100f, Rate = 0.05f + (float)rnd.NextDouble() * 0.1f, Alpha = windowColor.a, Window = true });
                }
            }
            puffs = new Puff[Mathf.Max(8, smokePoints.Length * factory.Copies.Count * 5)];
            for (int i = 0; i < puffs.Length; i++)
            {
                puffs[i].R = Renderer(factory.Root, "Smoke", smokeSprite, Color.clear, order - 1);
                puffs[i].R.enabled = false;
            }
            nextPuff = new float[smokePoints.Length * factory.Copies.Count];
            for (int i = 0; i < nextPuff.Length; i++) nextPuff[i] = (float)rnd.NextDouble() * 2f;
        }

        void BuildEmbers()
        {
            if (dotSprite == null || emberCount <= 0) return;
            emberRoot = new GameObject("Embers").transform;
            emberRoot.SetParent(transform, false);
            embers = new Ember[emberCount];
            for (int i = 0; i < embers.Length; i++)
            {
                embers[i].R = Renderer(emberRoot, "Ember", dotSprite, Color.clear, emberOrder);
                Respawn(ref embers[i], scatter: true);
            }
        }

        // ---------------------------------------------------------------- frame

        float HalfWidth => view.orthographicSize * view.aspect;

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float time = Time.unscaledTime;
            var mouse = Mouse.current;
            Vector2 target = pointer;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 p = mouse.position.ReadValue();
                target = new Vector2(Mathf.Clamp(p.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(p.y / Screen.height * 2f - 1f, -1f, 1f));
            }
            pointer = Vector2.Lerp(pointer, target, 1f - Mathf.Exp(-pointerSmoothing * dt));

            foreach (var b in built) Place(b, dt, time);
            if (factory != null) { Smoke(dt); Blink(time); }
            if (emberRoot != null)
            {
                emberRoot.localPosition = -pointer * (emberDepth * pointerShift);
                for (int i = 0; i < embers.Length; i++) StepEmber(ref embers[i], dt);
            }
        }

        void Place(Built b, float dt, float time)
        {
            var d = b.Data;
            if (d.FillScreen)
            {
                var size = d.Sprite.bounds.size;
                float h = view.orthographicSize * 2f, w = h * view.aspect;
                b.Root.localPosition = Vector3.zero;
                b.Copies[0].transform.localScale = new Vector3(w / size.x * 1.02f, h / size.y * 1.02f, 1f);
                return;
            }
            Vector2 shift = -pointer * (d.Depth * pointerShift);
            float bob = d.Bob != 0 ? Mathf.Sin(time * d.BobSpeed) * d.Bob : 0f;
            b.Root.localPosition = new Vector3((d.Tiled ? 0f : d.Position.x) + shift.x, d.Position.y + shift.y * 0.5f + bob, 0f);
            if (b.Strip != null)
            {
                b.Scroll += d.Scroll * dt;
                b.Strip.localPosition = new Vector3(-Mathf.Repeat(b.Scroll, b.TileWidth), 0f, 0f);
            }
            if (d.Pulse != 0)
            {
                float a = d.Tint.a * (1f - d.Pulse + d.Pulse * (0.5f + 0.5f * Mathf.Sin(time * d.PulseSpeed)));
                foreach (var r in b.Copies) r.color = new Color(d.Tint.r, d.Tint.g, d.Tint.b, a);
            }
        }

        void Smoke(float dt)
        {
            float half = HalfWidth + 2f;
            int n = 0;
            foreach (var copy in factory.Copies)
            {
                Vector2 copyPos = (Vector2)copy.transform.localPosition + (Vector2)factory.Strip.localPosition;
                for (int i = 0; i < smokePoints.Length; i++, n++)
                {
                    nextPuff[n] -= dt;
                    if (nextPuff[n] > 0) continue;
                    nextPuff[n] = 0.55f + (float)rnd.NextDouble() * 0.5f;
                    Vector2 local = copyPos + OnTile(smokePoints[i]);
                    float worldX = factory.Root.localPosition.x + local.x;
                    if (worldX < -half || worldX > half) continue;
                    Emit(local);
                }
            }
            for (int i = 0; i < puffs.Length; i++)
            {
                ref var p = ref puffs[i];
                if (!p.R.enabled) continue;
                p.Age += dt;
                if (p.Age >= p.Life) { p.R.enabled = false; continue; }
                float t = p.Age / p.Life;
                var pos = p.R.transform.localPosition;
                pos += (Vector3)(p.Velocity * dt) + Vector3.left * (factory.Data.Scroll * dt);
                p.R.transform.localPosition = pos;
                p.R.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.9f, t);
                p.R.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, smokeColor.a * Mathf.Sin(Mathf.PI * Mathf.Sqrt(t)));
            }
        }

        void Emit(Vector2 local)
        {
            for (int i = 0; i < puffs.Length; i++)
            {
                if (puffs[i].R.enabled) continue;
                puffs[i].R.enabled = true;
                puffs[i].Age = 0;
                puffs[i].Life = 4.5f + (float)rnd.NextDouble() * 2f;
                puffs[i].Velocity = new Vector2(-0.25f - (float)rnd.NextDouble() * 0.2f, 0.35f + (float)rnd.NextDouble() * 0.2f);
                puffs[i].R.transform.localPosition = local;
                return;
            }
        }

        void Blink(float time)
        {
            foreach (var b in blinkers)
            {
                float a;
                if (b.Window) a = Mathf.PerlinNoise(b.Phase, time * b.Rate) > 0.38f ? b.Alpha : b.Alpha * 0.15f; // rooms switch on and off
                else a = b.Alpha * (Mathf.Repeat(time * b.Rate + b.Phase, 1f) < 0.5f ? 1f : 0.12f);
                var c = b.R.color;
                b.R.color = new Color(c.r, c.g, c.b, a);
            }
        }

        void Respawn(ref Ember e, bool scatter)
        {
            float half = HalfWidth + 1f;
            float h = view.orthographicSize;
            e.Age = 0;
            e.Life = 6f + (float)rnd.NextDouble() * 6f;
            e.Phase = (float)rnd.NextDouble() * 10f;
            e.Velocity = new Vector2(-0.15f - (float)rnd.NextDouble() * 0.3f, 0.25f + (float)rnd.NextDouble() * 0.35f);
            float y = scatter ? Mathf.Lerp(-h, h * 0.4f, (float)rnd.NextDouble()) : -h - 0.3f;
            e.R.transform.localPosition = new Vector3(Mathf.Lerp(-half, half + 3f, (float)rnd.NextDouble()), y, 0f);
            e.R.transform.localScale = Vector3.one * (0.05f + (float)rnd.NextDouble() * 0.07f);
            if (scatter) e.Age = (float)rnd.NextDouble() * e.Life;
        }

        void StepEmber(ref Ember e, float dt)
        {
            e.Age += dt;
            if (e.Age >= e.Life) Respawn(ref e, scatter: false);
            float t = e.Age / e.Life;
            var pos = e.R.transform.localPosition;
            pos += new Vector3(e.Velocity.x + Mathf.Sin(e.Age * 1.7f + e.Phase) * 0.2f, e.Velocity.y, 0f) * dt;
            e.R.transform.localPosition = pos;
            float flicker = 0.75f + 0.25f * Mathf.Sin(e.Age * 9f + e.Phase);
            e.R.color = new Color(emberColor.r, emberColor.g, emberColor.b, emberColor.a * Mathf.Sin(Mathf.PI * t) * flicker);
        }
    }
}
