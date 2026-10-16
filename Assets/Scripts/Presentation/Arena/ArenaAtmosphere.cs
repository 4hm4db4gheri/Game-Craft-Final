using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;
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
                if (l != global && l != playerLight) propLights.Add((l, l.intensity));
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
