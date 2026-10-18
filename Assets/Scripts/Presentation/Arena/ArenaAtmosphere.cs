using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hollow.Presentation
{
    /// <summary>
    /// Arena light, mist and post-processing per phase (D18, §14, D92): a global 2D light that dims phase by phase and
    /// swells and browns out, searchlights sweeping the human base, a glow carried by the player AI so dark phases stay
    /// readable, two layers of drifting mist, a graded world-only volume with a hurt pulse, and the fade to full
    /// darkness in the ending (prop lamps included). Cosmetic only; never touches the simulation.
    /// </summary>
    public sealed class ArenaAtmosphere : MonoBehaviour
    {
        [SerializeField] Transform follow;
        [SerializeField] Sprite fogSprite;
        [SerializeField, Tooltip("Mist drawn unlit reads as haze in the dark phases instead of vanishing with the light.")] Material unlitMaterial;
        [SerializeField] Vector2 arenaSize = new(48, 32);
        [SerializeField] int fogPatches = 22;
        [SerializeField] int highFogPatches = 10;
        [SerializeField] int maxSearchlights = 3;
        [SerializeField] float searchlightRange = 30f;
        [SerializeField] float searchlightSweep = 38f;
        [SerializeField] float searchlightSpeed = 0.22f;
        [SerializeField] float hurtDecay = 2.5f;
        [SerializeField] float heartbeatRate = 1.25f;

        static readonly Color HurtColor = new(0.55f, 0.03f, 0.05f);

        /// <summary>Everything that fades between phases.</summary>
        struct Look
        {
            public float Light, Glow, Fog, HighFog, Props, Pulse, Flickers, Beams, Vignette;
            public Color FogColor;

            public static Look Lerp(in Look a, in Look b, float t) => new()
            {
                Light = Mathf.Lerp(a.Light, b.Light, t), Glow = Mathf.Lerp(a.Glow, b.Glow, t),
                Fog = Mathf.Lerp(a.Fog, b.Fog, t), HighFog = Mathf.Lerp(a.HighFog, b.HighFog, t),
                Props = Mathf.Lerp(a.Props, b.Props, t), Pulse = Mathf.Lerp(a.Pulse, b.Pulse, t),
                Flickers = Mathf.Lerp(a.Flickers, b.Flickers, t), Beams = Mathf.Lerp(a.Beams, b.Beams, t),
                Vignette = Mathf.Lerp(a.Vignette, b.Vignette, t),
                FogColor = Color.Lerp(a.FogColor, b.FogColor, t),
            };
        }

        Light2D global, playerLight;
        Volume postVolume;
        VolumeProfile postProfile;
        Bloom bloom;
        Vignette vignette;
        ColorAdjustments grading;
        SplitToning toning;
        FilmGrain grain;
        ChromaticAberration fringe;
        AtmosphereData data;
        readonly List<(Light2D Light, float Intensity)> propLights = new();
        readonly List<Light2D> beams = new();
        readonly List<SpriteRenderer> fog = new();
        readonly List<Vector2> fogDrift = new();
        readonly List<float> fogSeed = new();

        Look from, to, now = new() { Light = 1f, Props = 1f, FogColor = Color.white };
        float fadeTime, fadeStart;
        float flickerStart = -10f, flickerLength, flickerSeed, nextFlickerAt;
        float hurt, lowHealth, lastHealth = -1f;

        void Awake()
        {
            global = NewLight("GlobalLight", Light2D.LightType.Global);
            playerLight = NewLight("PlayerGlow", Light2D.LightType.Point);
            playerLight.color = new Color(0.75f, 0.95f, 1f);
            playerLight.falloffIntensity = 0.6f;
            playerLight.intensity = 0f;

            for (int i = 0; i < maxSearchlights; i++)
            {
                var beam = NewLight($"Searchlight_{i}", Light2D.LightType.Point);
                float across = maxSearchlights == 1 ? 0f : i / (maxSearchlights - 1f) - 0.5f;
                beam.transform.position = new Vector3(across * arenaSize.x * 0.7f, arenaSize.y / 2f + 3f, 0f);
                beam.color = new Color(0.8f, 0.9f, 1f);
                beam.pointLightInnerRadius = 0f;
                beam.pointLightOuterRadius = searchlightRange;
                beam.pointLightInnerAngle = 11f;
                beam.pointLightOuterAngle = 20f;
                beam.falloffIntensity = 0.3f;
                beam.intensity = 0f;
                beams.Add(beam);
            }

            // Low mist hugs the ground under every body; the high banks drift over them, faint enough to read through.
            var rng = new System.Random(11);
            for (int i = 0; i < fogPatches + highFogPatches; i++)
            {
                bool high = i >= fogPatches;
                var go = new GameObject(high ? $"MistHigh_{i - fogPatches}" : $"Mist_{i}");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = fogSprite;
                if (unlitMaterial != null) r.sharedMaterial = unlitMaterial;
                if (high) { r.sortingLayerName = "Effects"; r.sortingOrder = -100; } // over bodies, under every effect and warning
                else r.sortingOrder = -50; // above the ground and decor, below every character and warning
                float size = high ? 14f + (float)rng.NextDouble() * 10f : 9f + (float)rng.NextDouble() * 8f;
                go.transform.localScale = new Vector3(size, size * 0.55f, 1f);
                go.transform.position = new Vector3(((float)rng.NextDouble() - 0.5f) * arenaSize.x, ((float)rng.NextDouble() - 0.5f) * arenaSize.y, 0);
                fog.Add(r);
                float speed = high ? 0.35f : 0.15f;
                fogDrift.Add(new Vector2(speed + (float)rng.NextDouble() * 0.25f, ((float)rng.NextDouble() - 0.5f) * 0.1f));
                fogSeed.Add((float)rng.NextDouble() * 100f);
            }
            Apply(now, 1f, 1f);
        }

        void Start()
        {
            // Lamps and lit machines on props: they go dark with the world in the ending.
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Include))
            {
                if (l == global || l == playerLight || beams.Contains(l)) continue;
                bool known = false;
                foreach (var entry in propLights) if (entry.Light == l) { known = true; break; }
                if (!known) propLights.Add((l, l.intensity));
            }
        }

        /// <summary>A small cyan pool grounds each service terminal in the arena.</summary>
        public void AddShopLights(ArenaAuthoring arena)
        {
            if (arena == null) return;
            foreach (var station in arena.AllShops())
            {
                var light = NewLight("Shop Screen Glow", Light2D.LightType.Point);
                light.transform.position = station.position + Vector3.up * 0.8f;
                light.color = new Color(0.24f, 0.88f, 0.95f);
                light.intensity = 0.42f;
                light.pointLightInnerRadius = 0.5f;
                light.pointLightOuterRadius = 3.2f;
                light.falloffIntensity = 0.65f;
                propLights.Add((light, light.intensity));
            }
        }

        /// <summary>Attach a world-only URP volume. The overlay HUD stays crisp.</summary>
        public void BindCamera(Camera camera)
        {
            if (camera == null || postVolume != null) return;
            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = 1 << gameObject.layer;

            postProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            bloom = postProfile.Add<Bloom>(true);
            vignette = postProfile.Add<Vignette>(true);
            grading = postProfile.Add<ColorAdjustments>(true);
            toning = postProfile.Add<SplitToning>(true);
            grain = postProfile.Add<FilmGrain>(true);
            fringe = postProfile.Add<ChromaticAberration>(true);
            var go = new GameObject("World Post Processing");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            postVolume = go.AddComponent<Volume>();
            postVolume.isGlobal = true;
            postVolume.priority = 10f;
            postVolume.sharedProfile = postProfile;
        }

        void OnDestroy()
        {
            if (postProfile != null) Destroy(postProfile);
        }

        Light2D NewLight(string name, Light2D.LightType type)
        {
            // Inactive while configuring: the light type must be set before the light registers itself.
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<Light2D>();
            l.lightType = type;
            go.SetActive(true);
            return l;
        }

        static Color Hex(string hex, Color fallback) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;

        /// <summary>Moves toward the look of <paramref name="phase"/>; instantly after a load or a scene change.</summary>
        public void SetPhase(AtmosphereData a, int phase, bool instant)
        {
            data = a;
            if (global != null) global.color = Hex(a.LightColor(phase), Color.white);
            if (postVolume != null)
            {
                bloom.intensity.Override(Mathf.Clamp(a.BloomIntensity, 0f, 1.5f));
                bloom.threshold.Override(Mathf.Clamp(a.BloomThreshold, 0f, 2f));
                bloom.scatter.Override(Mathf.Clamp01(a.BloomScatter));
                vignette.smoothness.Override(0.5f);
                grading.contrast.Override(Mathf.Clamp(a.Contrast(phase), -20f, 30f));
                grading.saturation.Override(-Mathf.Clamp(a.Desaturation(phase), 0f, 30f));
                toning.shadows.Override(Hex(a.ShadowTint(phase), Color.gray));
                toning.highlights.Override(Hex(a.HighlightTint(phase), Color.gray));
                grain.type.Override(FilmGrainLookup.Medium1);
                grain.intensity.Override(Mathf.Clamp01(a.Grain(phase)));
                grain.response.Override(0.8f);
            }
            if (playerLight != null) playerLight.pointLightOuterRadius = a.PlayerLightRadius;
            if (instant) lastHealth = -1f;

            // The glow grows as the phase gets darker (none at full light, full at half light or less).
            var target = new Look
            {
                Light = a.Light(phase), Glow = a.PlayerLightIntensity * Mathf.Clamp01((1f - a.Light(phase)) * 2f),
                Fog = a.Fog(phase), HighFog = a.HighFog(phase), Props = 1f, Pulse = a.LightPulse(phase),
                Flickers = a.FlickersPerMinute(phase), Beams = Mathf.Min(a.Searchlights(phase), maxSearchlights),
                FogColor = Hex(a.FogColor(phase), new Color(0.8f, 0.82f, 0.86f)), Vignette = Mathf.Clamp(a.Vignette(phase), 0f, 0.5f),
            };
            StartFade(target, instant ? 0f : a.PhaseFadeTime);
        }

        /// <summary>The world goes dark (ending): every light, beam and the mist fade to nothing.</summary>
        public void FadeToDark(AtmosphereData a) =>
            StartFade(new Look { FogColor = now.FogColor, Vignette = now.Vignette }, a.EndingFadeTime);

        /// <summary>Feeds the hurt pulse: a drop in health flashes the edges red; low health beats like a heart.</summary>
        public void SyncPlayer(float health, float maxHealth, bool dead)
        {
            if (lastHealth >= 0f && health < lastHealth - 0.01f && maxHealth > 0f)
                hurt = Mathf.Min(1f, hurt + 0.55f + (lastHealth - health) / maxHealth * 3f);
            lastHealth = health;
            float threshold = data != null ? data.LowHealthFraction : 0f;
            lowHealth = dead || maxHealth <= 0f || threshold <= 0f ? 0f : Mathf.Clamp01(1f - health / maxHealth / threshold);
        }

        void StartFade(in Look target, float time)
        {
            from = now;
            to = target;
            fadeTime = time;
            fadeStart = Time.unscaledTime;
            if (time <= 0f) now = target;
        }

        void LateUpdate()
        {
            float t = Time.unscaledTime;
            if (fadeTime > 0f)
            {
                float k = Mathf.Clamp01((t - fadeStart) / fadeTime);
                now = Look.Lerp(from, to, k * k * (3f - 2f * k));
                if (k >= 1f) fadeTime = 0f;
            }

            float swell = 1f + now.Pulse * (Mathf.PerlinNoise(t * (data != null ? data.LightPulseSpeed : 0f), 0.37f) * 2f - 1f);
            Apply(now, swell, Brownout(t));
            if (follow != null && playerLight != null) playerLight.transform.position = follow.position;
            SyncPost(t);

            float hx = arenaSize.x / 2f + 8f, hy = arenaSize.y / 2f + 5f;
            for (int i = 0; i < fog.Count; i++)
            {
                var tr = fog[i].transform;
                var p = (Vector2)tr.position + fogDrift[i] * Time.deltaTime;
                if (p.x > hx) p.x = -hx;
                if (p.y > hy) p.y = -hy; else if (p.y < -hy) p.y = hy;
                tr.position = p;
            }
        }

        /// <summary>The power stutters: a short burst of dips at random moments, more often in darker phases.</summary>
        float Brownout(float t)
        {
            if (now.Flickers <= 0.01f || data == null) return 1f;
            if (t >= nextFlickerAt)
            {
                bool first = nextFlickerAt <= 0f;
                if (!first)
                {
                    flickerStart = t;
                    flickerLength = Random.Range(0.35f, 1.1f);
                    flickerSeed = Random.Range(0f, 100f);
                }
                // Exponential gaps average out to the configured rate; never two bursts back to back.
                float mean = 60f / now.Flickers;
                nextFlickerAt = t + Mathf.Max(2f, -Mathf.Log(1f - Random.value * 0.98f) * mean);
            }
            float age = t - flickerStart;
            if (age < 0f || age > flickerLength) return 1f;
            float envelope = Mathf.Sin(age / flickerLength * Mathf.PI);
            float off = Mathf.PerlinNoise(age * 24f, flickerSeed) > 0.48f ? 1f : 0.25f;
            return 1f - Mathf.Clamp01(data.FlickerDepth) * off * envelope;
        }

        void Apply(in Look look, float swell, float brownout)
        {
            if (global != null) global.intensity = look.Light * swell * brownout;
            if (playerLight != null) playerLight.intensity = look.Glow; // the AI's own light never stutters
            foreach (var (l, intensity) in propLights) if (l != null) l.intensity = intensity * look.Props * brownout;

            float t = Time.unscaledTime;
            float beamPower = data != null ? data.SearchlightIntensity : 0f;
            for (int i = 0; i < beams.Count; i++)
            {
                float weight = Mathf.Clamp01(look.Beams - i);
                beams[i].intensity = beamPower * weight * brownout;
                beams[i].enabled = weight > 0f;
                if (weight <= 0f) continue;
                // Each beam swings across the arena from the north edge, out of step with the others.
                float swing = Mathf.Sin(t * searchlightSpeed * Mathf.PI * 2f * (1f + i * 0.17f) + i * 2.1f) * searchlightSweep;
                float lean = (i - (beams.Count - 1) / 2f) * -12f;
                beams[i].transform.rotation = Quaternion.Euler(0f, 0f, 180f + lean + swing);
            }

            float breath = data != null ? Mathf.Clamp01(data.FogBreath) : 0f;
            for (int i = 0; i < fog.Count; i++)
            {
                bool high = i >= fogPatches;
                float swellFog = 1f - breath + breath * 2f * Mathf.PerlinNoise(t * 0.08f, fogSeed[i]);
                var c = look.FogColor;
                c.a = (high ? look.HighFog : look.Fog) * swellFog;
                fog[i].color = c;
            }
        }

        void SyncPost(float t)
        {
            if (postVolume == null || data == null) return;
            hurt = Mathf.Max(0f, hurt - hurtDecay * Time.unscaledDeltaTime);
            // Heartbeat: two quick thumps per beat, stronger the closer to death.
            float beat = 0f;
            if (lowHealth > 0f)
            {
                float phase = t * heartbeatRate % 1f;
                beat = Mathf.Max(Mathf.Exp(-phase * 14f), 0.7f * Mathf.Exp(-Mathf.Abs(phase - 0.22f) * 18f)) * (0.35f + 0.65f * lowHealth);
            }
            float red = Mathf.Clamp01(Mathf.Max(hurt, beat));
            vignette.intensity.Override(Mathf.Clamp(now.Vignette + data.HurtPulse * red, 0f, 0.65f));
            vignette.color.Override(Color.Lerp(Color.black, HurtColor, red));
            fringe.intensity.Override(Mathf.Clamp01(data.ChromaticAberration + hurt * 0.55f));
        }
    }
}
