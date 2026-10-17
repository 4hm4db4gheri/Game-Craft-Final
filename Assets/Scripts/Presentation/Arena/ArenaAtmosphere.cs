using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hollow.Presentation
{
    /// <summary>
    /// Arena light and ground mist per phase (D18, §14): a global 2D light that dims phase by phase, a glow carried
    /// by the player AI so phase three stays readable, drifting mist patches, and the fade to full darkness in
    /// the ending (prop lamps included). Cosmetic only; never touches the simulation.
    /// </summary>
    public sealed class ArenaAtmosphere : MonoBehaviour
    {
        [SerializeField] Transform follow;
        [SerializeField] Sprite fogSprite;
        [SerializeField] Vector2 arenaSize = new(48, 32);
        [SerializeField] int fogPatches = 14;

        Light2D global, playerLight;
        Volume postVolume;
        VolumeProfile postProfile;
        Bloom bloom;
        Vignette vignette;
        ColorAdjustments grading;
        readonly List<(Light2D Light, float Intensity)> propLights = new();
        readonly List<SpriteRenderer> fog = new();
        readonly List<Vector2> fogDrift = new();

        float lightFrom, lightTo, glowFrom, glowTo, fogFrom, fogTo, propsFrom, propsTo, fadeTime, fadeStart;
        float lightNow = 1f, glowNow, fogNow, propsNow = 1f;

        void Awake()
        {
            global = NewLight("GlobalLight", Light2D.LightType.Global);
            playerLight = NewLight("PlayerGlow", Light2D.LightType.Point);
            playerLight.color = new Color(0.75f, 0.95f, 1f);
            playerLight.falloffIntensity = 0.6f;
            playerLight.intensity = 0f;

            var rng = new System.Random(11);
            for (int i = 0; i < fogPatches; i++)
            {
                var go = new GameObject($"Mist_{i}");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = fogSprite;
                r.sortingOrder = -50; // above the ground and decor, below every character and warning
                float size = 9f + (float)rng.NextDouble() * 8f;
                go.transform.localScale = new Vector3(size, size * 0.55f, 1f);
                go.transform.position = new Vector3(((float)rng.NextDouble() - 0.5f) * arenaSize.x, ((float)rng.NextDouble() - 0.5f) * arenaSize.y, 0);
                fog.Add(r);
                fogDrift.Add(new Vector2(0.15f + (float)rng.NextDouble() * 0.25f, ((float)rng.NextDouble() - 0.5f) * 0.1f));
            }
            ApplyValues(1f, 0f, 0f, 1f);
        }

        void Start()
        {
            // Lamps and lit machines on props: they go dark with the world in the ending.
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Include))
            {
                if (l == global || l == playerLight) continue;
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

        /// <summary>Moves toward the look of <paramref name="phase"/>; instantly after a load or a scene change.</summary>
        public void SetPhase(AtmosphereData a, int phase, bool instant)
        {
            if (global != null && ColorUtility.TryParseHtmlString(a.LightColor(phase), out var tint)) global.color = tint;
            if (postVolume != null)
            {
                bloom.intensity.Override(Mathf.Clamp(a.BloomIntensity, 0f, 1f));
                bloom.threshold.Override(Mathf.Clamp(a.BloomThreshold, 0f, 2f));
                vignette.intensity.Override(Mathf.Clamp(a.Vignette(phase), 0f, 0.3f));
                vignette.smoothness.Override(0.45f);
                grading.contrast.Override(Mathf.Clamp(a.Contrast(phase), -20f, 25f));
                grading.saturation.Override(-Mathf.Clamp(a.Desaturation(phase), 0f, 30f));
            }
            // The glow grows as the phase gets darker (none at full light, full at half light or less).
            float glow = a.PlayerLightIntensity * Mathf.Clamp01((1f - a.Light(phase)) * 2f);
            StartFade(a.Light(phase), glow, a.Fog(phase), 1f, instant ? 0f : a.PhaseFadeTime);
            if (playerLight != null) playerLight.pointLightOuterRadius = a.PlayerLightRadius;
        }

        /// <summary>The world goes dark (ending): every light and the mist fade to nothing.</summary>
        public void FadeToDark(AtmosphereData a) => StartFade(0f, 0f, 0f, 0f, a.EndingFadeTime);

        void StartFade(float light, float glow, float fogAlpha, float props, float time)
        {
            lightFrom = lightNow; glowFrom = glowNow; fogFrom = fogNow; propsFrom = propsNow;
            lightTo = light; glowTo = glow; fogTo = fogAlpha; propsTo = props;
            fadeTime = time;
            fadeStart = Time.unscaledTime;
            if (time <= 0f) ApplyValues(light, glow, fogAlpha, props);
        }

        void ApplyValues(float light, float glow, float fogAlpha, float props)
        {
            lightNow = light; glowNow = glow; fogNow = fogAlpha; propsNow = props;
            if (global != null) global.intensity = light;
            if (playerLight != null) playerLight.intensity = glow;
            foreach (var (l, intensity) in propLights) if (l != null) l.intensity = intensity * props;
            foreach (var r in fog) r.color = new Color(0.8f, 0.82f, 0.86f, fogAlpha);
        }

        void LateUpdate()
        {
            if (fadeTime > 0f)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - fadeStart) / fadeTime);
                float s = t * t * (3f - 2f * t);
                ApplyValues(Mathf.Lerp(lightFrom, lightTo, s), Mathf.Lerp(glowFrom, glowTo, s), Mathf.Lerp(fogFrom, fogTo, s), Mathf.Lerp(propsFrom, propsTo, s));
                if (t >= 1f) fadeTime = 0f;
            }
            if (follow != null && playerLight != null) playerLight.transform.position = follow.position;

            float hx = arenaSize.x / 2f + 6f, hy = arenaSize.y / 2f + 4f;
            for (int i = 0; i < fog.Count; i++)
            {
                var tr = fog[i].transform;
                var p = (Vector2)tr.position + fogDrift[i] * Time.deltaTime;
                if (p.x > hx) p.x = -hx;
                if (p.y > hy) p.y = -hy; else if (p.y < -hy) p.y = hy;
                tr.position = p;
            }
        }
    }
}
