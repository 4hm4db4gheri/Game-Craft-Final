using System.Collections.Generic;
using Hollow.Data;
using Hollow.Sim;
using TMPro;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>
    /// Game feel (T05, §14, the 8 layers of video B) driven by simulation events; purely cosmetic and never read
    /// by gameplay:
    /// 1 charge — muzzle flash (never delays the shot), 2 core and 3 trail — projectile prefabs,
    /// 4 burst — pooled impact sparks and flashes, 5 reaction — enemy flash and squash (EnemyView),
    /// 6 sound — the audio director listens to the same events (T19),
    /// 7 aftermath — capped corpses and blood, oil or scorch decals, 8 — pooled, rate-limited damage numbers.
    /// Camera shake is short, scaled by the player's shake setting, and never a hit-stop.
    /// Everything here is on the Effects sorting layer, under the Telegraphs layer.
    /// </summary>
    public sealed class FeedbackDirector : MonoBehaviour
    {
        [SerializeField] WorldPresenter presenter;
        [SerializeField] PlayerView playerView;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] Material unlitMaterial;
        [SerializeField] Sprite sparkSprite;
        [SerializeField] Sprite glowSprite;
        [SerializeField] Sprite blobSprite;

        static readonly Color Blood = new(0.29f, 0.08f, 0.09f, 0.85f);  // dark and desaturated (ART brief)
        static readonly Color Oil = new(0.04f, 0.04f, 0.05f, 0.8f);
        static readonly Color Scorch = new(0.05f, 0.04f, 0.04f, 0.7f);

        float shakeIntensity = 1f;
        bool numbersOn = true;
        int budgetLevel = 2;

        ParticleSystem sparks;
        readonly List<(SpriteRenderer R, float Until, float Length)> flashes = new();
        int nextFlash;
        readonly List<SpriteRenderer> decals = new(), corpses = new();
        int nextDecal, nextCorpse;
        /// <summary>Per corpse slot: death launch from → to with a turn, then hold, then fade (v0.19 §19).</summary>
        readonly List<(float Born, Vector3 From, Vector3 To, float Rot0, float Rot1, Color Color)> corpseMotion = new();
        readonly List<(SpriteRenderer R, float Until, float Length)> tracers = new();
        int nextTracer;
        readonly List<(TextMeshPro T, float Born, Vector3 From)> numbers = new();
        int nextNumber;
        float numberTokens;
        float lastShotShake = -1f;
        readonly Dictionary<int, (float Sum, Vector2 Pos)> pendingNumbers = new();
        readonly Dictionary<EnemyId, (Sprite Sprite, Color Color, Vector2 Scale)> corpseLooks = new();
        FeedbackData data;

        /// <summary>Player settings (§11): shake 0–1, damage numbers on/off, effect budget 0–2.</summary>
        public void Configure(float shake, bool damageNumbers, int effectBudget)
        {
            shakeIntensity = Mathf.Clamp01(shake);
            numbersOn = damageNumbers;
            budgetLevel = effectBudget;
            if (data != null) ApplyCaps(data);
        }

        void Awake()
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            var emission = sparks.emission;
            emission.enabled = false;
            var shape = sparks.shape;
            shape.enabled = false;
            var drag = sparks.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 7f;
            var size = sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            // Sprite material + sprite sheet: the 2D renderer does not draw the 3D particle shaders.
            var sheet = sparks.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            sheet.AddSprite(sparkSprite);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = unlitMaterial;
            r.sortingLayerID = SortingLayers.Effects;
            r.sortingOrder = 10;
            sparks.Play();
        }

        void ApplyCaps(FeedbackData f)
        {
            float scale = f.BudgetScale(budgetLevel);
            var main = sparks.main;
            main.maxParticles = Mathf.Max(16, Mathf.RoundToInt(f.MaxParticles * scale));
            Resize(decals, Mathf.RoundToInt(f.MaxDecals * scale), "Decal", -95);
            Resize(corpses, Mathf.RoundToInt(f.MaxCorpses * scale), "Corpse", -60);
            while (corpseMotion.Count > corpses.Count) corpseMotion.RemoveAt(corpseMotion.Count - 1);
            while (corpseMotion.Count < corpses.Count) corpseMotion.Add(default);
            nextDecal %= Mathf.Max(1, decals.Count);
            nextCorpse %= Mathf.Max(1, corpses.Count);
        }

        void Resize(List<SpriteRenderer> list, int count, string name, int order)
        {
            while (list.Count > count) { Destroy(list[^1].gameObject); list.RemoveAt(list.Count - 1); }
            while (list.Count < count)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>(); // lit: aftermath darkens with the arena
                r.sortingOrder = order;
                r.enabled = false;
                list.Add(r);
            }
        }

        /// <summary>Clears corpses, decals and numbers (load, retry, phase change: the arena changed).</summary>
        public void Clear()
        {
            foreach (var d in decals) d.enabled = false;
            foreach (var c in corpses) c.enabled = false;
            foreach (var t in tracers) t.R.enabled = false;
            foreach (var n in numbers) n.T.enabled = false;
            sparks.Clear();
        }

        // ------------------------------------------------------------------ events

        public void OnEvent(World w, in SimEvent ev)
        {
            if (data != w.Profile.Feedback) { data = w.Profile.Feedback; ApplyCaps(data); }
            var f = data;
            switch (ev.Type)
            {
                case SimEventType.ShotFired:
                    if (ev.EntityId == PlayerState.EntityId)
                    {
                        var wv = presenter.Catalog.Weapon((WeaponId)ev.Code);
                        Color c = wv != null ? wv.ProjectileColor : Color.white;
                        Vector2 muzzle = playerView.Muzzle;
                        bool spread = ev.Value > 1;
                        Flash(muzzle, spread ? 0.75f : 0.5f, c, f.MuzzleFlashTime);
                        Tracer(muzzle, ev.Direction, c, f);
                        Burst(muzzle, ev.Direction, spread ? 50f : 25f, 3, c, 5f);
                        if (Time.unscaledTime - lastShotShake >= f.ShotShakeMinInterval)
                        {
                            lastShotShake = Time.unscaledTime;
                            Shake(f.ShakeOnShot * (spread ? 2f : 1f), ev.Direction);
                        }
                    }
                    else Flash(ev.Position + Vector2.up * 0.95f + ev.Direction * 0.6f, 0.4f, new Color(1f, 0.9f, 0.6f), f.MuzzleFlashTime);
                    break;

                case SimEventType.EnemyAttack when ev.Code >= 0 && ev.Direction.sqrMagnitude > 0.01f && w.Profile.Enemy((EnemyId)ev.Code).Role == EnemyRole.Ranged:
                    Flash(ev.Position + Vector2.up * 0.6f + ev.Direction.normalized * 0.5f, 0.4f, presenter.Catalog.Telegraph, f.MuzzleFlashTime);
                    break;

                case SimEventType.EnemyAttack when ev.Code == -1: // boss impact
                    Shake(f.ShakeOnBossImpact, ev.Direction);
                    break;

                case SimEventType.ProjectileImpact:
                    if (presenter.TryProjectileColor(ev.EntityId, out var pc))
                    {
                        Burst(ev.Position, -ev.Direction, 70f, f.ImpactSparks, pc, 4f);
                        Flash(ev.Position, 0.35f, pc, 0.06f);
                    }
                    break;

                case SimEventType.EnemyHit:
                {
                    Vector2 at = ev.Position + Vector2.up * 0.5f;
                    var faction = FactionOf(w, ev.EntityId);
                    Burst(at, ev.Direction, 60f, f.ImpactSparks, SparkColor(faction), 5f);
                    if (ev.Value > 0f)
                    {
                        pendingNumbers.TryGetValue(ev.EntityId, out var acc);
                        pendingNumbers[ev.EntityId] = (acc.Sum + ev.Value, ev.Position);
                    }
                    break;
                }

                case SimEventType.EnemyDied:
                {
                    var type = (EnemyId)ev.Code;
                    var d = w.Profile.Enemy(type);
                    Burst(ev.Position + Vector2.up * 0.4f, ev.Direction, 180f, f.DeathSparks, SparkColor(d.Faction), 6f);
                    if (d.Role == EnemyRole.Explosive) break; // its explosion leaves the scorch mark
                    Decal(ev.Position + Random.insideUnitCircle * 0.2f, d.BodyRadius * 2.6f, d.Faction == Faction.Robots ? Oil : Blood);
                    Corpse(type, ev.Position, ev.Direction, f);
                    break;
                }

                case SimEventType.Explosion:
                    Burst(ev.Position, Vector2.right, 360f, f.ExplosionSparks, new Color(1f, 0.7f, 0.35f), ev.Value * 4f);
                    Flash(ev.Position, ev.Value * 1.6f, new Color(1f, 0.85f, 0.6f), 0.12f);
                    Decal(ev.Position, ev.Value * 1.5f, Scorch);
                    Shake(ev.EntityId == BossState.EntityId ? f.ShakeOnBossImpact : f.ShakeOnExplosion, Vector2.zero);
                    break;

                case SimEventType.PlayerHit:
                    Burst(ev.Position + Vector2.up * 0.4f, ev.Direction, 90f, f.ImpactSparks, new Color(0.7f, 0.95f, 1f), 5f);
                    Shake(f.ShakeOnPlayerHit, ev.Direction);
                    break;

                case SimEventType.CompanionHit:
                    Burst(ev.Position + Vector2.up * 0.9f, ev.Direction, 90f, f.ImpactSparks, Blood, 4f);
                    break;

                case SimEventType.BossDefeated:
                    Burst(ev.Position + Vector2.up, Vector2.right, 360f, f.ExplosionSparks * 2, new Color(1f, 0.8f, 0.5f), 9f);
                    Decal(ev.Position, 5f, (BossId)ev.Code == BossId.IndustrialRobot ? Oil : Blood);
                    Shake(f.ShakeOnBossDefeated, Vector2.zero);
                    break;
            }
        }

        /// <summary>After a frame's events: damage numbers (one per enemy per frame, so shotgun pellets add up).</summary>
        public void EndFrame(World w)
        {
            var f = w.Profile.Feedback;
            float scale = f.BudgetScale(budgetLevel);
            float perSecond = f.DamageNumbersMaxPerSecond * scale;
            numberTokens = Mathf.Min(perSecond, numberTokens + perSecond * Time.unscaledDeltaTime);
            if (numbersOn)
                foreach (var kv in pendingNumbers)
                {
                    if (numberTokens < 1f) break; // crowds: drop numbers rather than flood the screen (§14)
                    numberTokens -= 1f;
                    Number(kv.Value.Pos, kv.Value.Sum, Mathf.RoundToInt(f.MaxDamageNumbersOnScreen * scale));
                }
            pendingNumbers.Clear();
        }

        // ------------------------------------------------------------------ layers

        void Burst(Vector2 at, Vector2 dir, float spreadDegrees, int count, Color color, float speed)
        {
            if (count <= 0) return;
            float baseAngle = dir.sqrMagnitude > 1e-4f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 0f;
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                float a = (baseAngle + Random.Range(-spreadDegrees, spreadDegrees) * 0.5f) * Mathf.Deg2Rad;
                ep.position = new Vector3(at.x, at.y, 0);
                ep.velocity = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * speed * Random.Range(0.5f, 1.2f);
                ep.startSize = Random.Range(0.06f, 0.14f);
                ep.startLifetime = Random.Range(0.15f, 0.32f);
                ep.startColor = color;
                sparks.Emit(ep, 1);
            }
        }

        void Flash(Vector2 at, float size, Color color, float seconds)
        {
            if (flashes.Count < 24)
            {
                var go = new GameObject("Flash");
                go.transform.SetParent(transform, false);
                var added = go.AddComponent<SpriteRenderer>();
                added.sprite = glowSprite;
                added.sharedMaterial = unlitMaterial;
                added.sortingLayerID = SortingLayers.Effects;
                added.sortingOrder = 20;
                flashes.Add((added, 0, 0));
            }
            int i = nextFlash;
            nextFlash = (nextFlash + 1) % 24; // the slot just added is always at i: slots are added in order
            var r = flashes[i].R;
            r.transform.position = new Vector3(at.x, at.y, 0);
            r.transform.localScale = new Vector3(size, size, 1);
            r.color = new Color(Mathf.Min(1f, color.r + 0.3f), Mathf.Min(1f, color.g + 0.3f), Mathf.Min(1f, color.b + 0.3f), 0.9f);
            r.enabled = true;
            float length = Mathf.Max(0.02f, seconds);
            flashes[i] = (r, Time.time + length, length);
        }

        /// <summary>A short streak from the muzzle along the shot (v0.19 §19), separate from the projectile itself.</summary>
        void Tracer(Vector2 from, Vector2 dir, Color color, FeedbackData f)
        {
            if (f.TracerTime <= 0f || dir.sqrMagnitude < 1e-4f) return;
            const int Pool = 16;
            if (tracers.Count < Pool)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(transform, false);
                var added = go.AddComponent<SpriteRenderer>();
                added.sprite = glowSprite;
                added.sharedMaterial = unlitMaterial;
                added.sortingLayerID = SortingLayers.Effects;
                added.sortingOrder = 15;
                tracers.Add((added, 0, 0));
            }
            int i = nextTracer;
            nextTracer = (nextTracer + 1) % Pool;
            var r = tracers[i].R;
            Vector2 n = dir.normalized;
            r.transform.position = (Vector3)(from + n * (f.TracerLength * 0.5f));
            r.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(f.TracerLength, f.TracerWidth, 1);
            r.color = new Color(Mathf.Min(1f, color.r + 0.4f), Mathf.Min(1f, color.g + 0.4f), Mathf.Min(1f, color.b + 0.4f), 0.9f);
            r.enabled = true;
            tracers[i] = (r, Time.time + f.TracerTime, f.TracerTime);
        }

        void Decal(Vector2 at, float size, Color color)
        {
            if (decals.Count == 0) return;
            var r = decals[nextDecal];
            nextDecal = (nextDecal + 1) % decals.Count; // the oldest mark makes room (cap, G7)
            r.sprite = blobSprite;
            r.color = color;
            r.transform.position = new Vector3(at.x, at.y, 0);
            r.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            r.transform.localScale = new Vector3(size * Random.Range(0.9f, 1.2f), size * Random.Range(0.55f, 0.75f), 1);
            r.enabled = true;
        }

        void Corpse(EnemyId type, Vector2 at, Vector2 dir, FeedbackData f)
        {
            if (corpses.Count == 0) return;
            if (!corpseLooks.TryGetValue(type, out var look))
            {
                var prefab = presenter.Catalog.EnemyPrefab(type);
                var main = prefab != null ? prefab.MainRenderer : null;
                look = main != null ? (main.sprite, main.color, (Vector2)main.transform.lossyScale) : (null, Color.clear, Vector2.one);
                corpseLooks[type] = look;
            }
            if (look.Sprite == null) return;
            int i = nextCorpse;
            var r = corpses[i];
            nextCorpse = (nextCorpse + 1) % corpses.Count;
            r.sprite = look.Sprite;
            var color = new Color(look.Color.r * 0.45f, look.Color.g * 0.45f, look.Color.b * 0.45f, 1f);
            r.color = color;
            float side = dir.x < 0 ? 1f : -1f;
            // Death launch (v0.19 §19): visual only, thrown along the killing hit; it blocks nothing and deals no damage.
            Vector2 push = dir.sqrMagnitude > 1e-4f ? dir.normalized * f.DeathLaunchDistance : Vector2.zero;
            var from = new Vector3(at.x, at.y + 0.1f, 0);
            var to = from + (Vector3)push;
            float rot1 = 90f * side + Random.Range(-12f, 12f); // lying on its side
            r.transform.position = from;
            r.transform.rotation = Quaternion.identity;
            r.transform.localScale = new Vector3(look.Scale.x * 0.8f, look.Scale.y * 0.8f, 1);
            r.enabled = true;
            corpseMotion[i] = (Time.time, from, to, 0f, rot1, color);
        }

        void Number(Vector2 at, float amount, int maxOnScreen)
        {
            if (maxOnScreen <= 0) return;
            if (numbers.Count < maxOnScreen)
            {
                var go = new GameObject("DamageNumber");
                go.transform.SetParent(transform, false);
                var t = go.AddComponent<TextMeshPro>();
                if (PersianText.Font != null) t.font = PersianText.Font;
                t.fontSize = 3.2f;
                t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.rectTransform.sizeDelta = new Vector2(3, 1);
                t.sortingLayerID = SortingLayers.Effects;
                t.sortingOrder = 100;
                t.outlineWidth = 0.25f;
                t.outlineColor = new Color32(20, 18, 24, 255);
                numbers.Add((t, 0, Vector3.zero));
            }
            int i = nextNumber % numbers.Count;
            nextNumber = (i + 1) % Mathf.Max(1, maxOnScreen);
            var n = numbers[i].T;
            n.text = PersianText.Number(Mathf.RoundToInt(amount));
            n.color = new Color(1f, 0.96f, 0.9f, 1f);
            n.enabled = true;
            var from = new Vector3(at.x + Random.Range(-0.25f, 0.25f), at.y + 1.3f, 0);
            n.transform.position = from;
            numbers[i] = (n, Time.time, from);
        }

        void AnimateCorpses(float now, FeedbackData f)
        {
            for (int i = 0; i < corpses.Count && i < corpseMotion.Count; i++)
            {
                var r = corpses[i];
                if (!r.enabled) continue;
                var m = corpseMotion[i];
                float age = now - m.Born;
                if (age < f.DeathLaunchTime)
                {
                    float k = age / Mathf.Max(0.01f, f.DeathLaunchTime);
                    float ease = 1f - (1f - k) * (1f - k);
                    r.transform.position = Vector3.Lerp(m.From, m.To, ease);
                    r.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(m.Rot0, m.Rot1, ease));
                    continue;
                }
                float fadeStart = f.DeathLaunchTime + f.CorpseHoldTime;
                if (age < fadeStart)
                {
                    if (r.transform.position != m.To) { r.transform.position = m.To; r.transform.rotation = Quaternion.Euler(0, 0, m.Rot1); }
                    continue;
                }
                float fade = (age - fadeStart) / Mathf.Max(0.01f, f.CorpseFadeTime);
                if (fade >= 1f) { r.enabled = false; continue; }
                var c = m.Color;
                c.a = 1f - fade;
                r.color = c;
            }
        }

        void Shake(float amount, Vector2 dir)
        {
            if (amount <= 0f || shakeIntensity <= 0f || cameraRig == null) return;
            Vector2 d = dir.sqrMagnitude > 1e-4f ? dir.normalized : Random.insideUnitCircle.normalized;
            cameraRig.Shake(d * amount * shakeIntensity, data.ShakeDuration);
        }

        static Faction? FactionOf(World w, int entityId)
        {
            if (entityId == BossState.EntityId) return null;
            foreach (var e in w.Enemies) if (e.Id == entityId) return w.Profile.Enemy(e.Type).Faction;
            return null;
        }

        static Color SparkColor(Faction? f) => f switch
        {
            Faction.Robots => new Color(1f, 0.82f, 0.45f),
            Faction.Animals => new Color(0.55f, 0.16f, 0.16f),
            Faction.Humans => new Color(0.6f, 0.18f, 0.18f),
            _ => new Color(1f, 0.9f, 0.75f),
        };

        void LateUpdate()
        {
            float now = Time.time;
            for (int i = 0; i < flashes.Count; i++)
            {
                var (r, until, length) = flashes[i];
                if (!r.enabled) continue;
                float left = until - now;
                if (left <= 0f) { r.enabled = false; continue; }
                var c = r.color;
                c.a = 0.9f * (left / length);
                r.color = c;
            }
            for (int i = 0; i < tracers.Count; i++)
            {
                var (r, until, length) = tracers[i];
                if (!r.enabled) continue;
                float left = until - now;
                if (left <= 0f) { r.enabled = false; continue; }
                var c = r.color;
                c.a = 0.9f * (left / length);
                r.color = c;
            }
            if (data != null) AnimateCorpses(now, data);
            float life = data != null ? data.DamageNumberLifetime : 0.6f;
            for (int i = 0; i < numbers.Count; i++)
            {
                var (t, born, from) = numbers[i];
                if (!t.enabled) continue;
                float k = (now - born) / life;
                if (k >= 1f) { t.enabled = false; continue; }
                t.transform.position = from + Vector3.up * (0.6f * (1f - (1f - k) * (1f - k)));
                var c = t.color;
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                t.color = c;
            }
        }
    }
}
