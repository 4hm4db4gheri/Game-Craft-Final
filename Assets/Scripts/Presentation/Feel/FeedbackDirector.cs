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
    /// 1 charge — muzzle flash, smoke and an ejected casing (never delays the shot), 2 core and 3 trail — projectile prefabs,
    /// 4 burst — pooled impact sparks and flashes, 5 reaction — enemy flash and squash (EnemyView),
    /// 6 sound — the audio director listens to the same events (T19),
    /// 7 aftermath — blood or oil spray, capped corpses, decals and small ground stains, 8 — pooled, rate-limited damage numbers.
    /// Extra moments: dash dust, ring and afterimages; level-up rings and rising motes; the player's death burst.
    /// Walking: the drone sheds glints from under its core, feet kick up dust tinted for each arena's ground,
    /// robots grind sparks, boots throw grit and hovering drones leave exhaust (all by distance moved).
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
        static readonly Color BloodSpray = new(0.52f, 0.06f, 0.07f, 1f);
        static readonly Color BloodChunk = new(0.33f, 0.05f, 0.06f, 1f);
        static readonly Color OilSpray = new(0.09f, 0.09f, 0.1f, 1f);
        static readonly Color MetalChunk = new(0.38f, 0.4f, 0.44f, 1f);
        static readonly Color Brass = new(0.9f, 0.7f, 0.32f, 1f);
        static readonly Color MuzzleWarm = new(1f, 0.82f, 0.45f, 1f);
        static readonly Color Smoke = new(0.7f, 0.7f, 0.7f, 0.28f);
        static readonly Color Dust = new(0.72f, 0.66f, 0.56f, 0.35f);
        static readonly Color DashCyan = new(0.45f, 0.92f, 1f, 1f);
        static readonly Color Gold = new(1f, 0.84f, 0.35f, 1f);
        // Walking dust per arena, a little lighter than its ground so it reads (unlit, so already toned to each
        // phase's light): dry forest earth, factory concrete, cold industrial grit.
        static readonly Color[] GroundDust =
        {
            new(0.82f, 0.72f, 0.55f, 0.8f), new(0.72f, 0.72f, 0.69f, 0.7f), new(0.58f, 0.62f, 0.68f, 0.65f),
        };
        static readonly Color Grit = new(0.2f, 0.2f, 0.22f, 1f);
        static readonly Color GrindSpark = new(1f, 0.66f, 0.3f, 1f);
        static readonly Color Exhaust = new(1f, 0.5f, 0.25f, 0.8f);

        // Cosmetic timing of the extra layers (not gameplay; tuned by eye).
        const float GhostInterval = 0.035f, GhostLife = 0.24f;
        const float StainHold = 4f, StainFade = 1.5f;
        const int RingPool = 16, GhostPool = 14, StainPool = 48;
        /// <summary>Distance between two glints of the drone's trail; footfall spacing is StrideBase plus StridePerRadius × body radius.</summary>
        const float GlintStride = 0.09f, StrideBase = 0.3f, StridePerRadius = 0.8f;
        /// <summary>Moves longer than this in one tick are teleports (load, phase change), not walking.</summary>
        const float TeleportStep = 1f;

        float shakeIntensity = 1f;
        bool numbersOn = true;
        int budgetLevel = 2;

        ParticleSystem sparks, gore, puffs, motes, dust, glints;
        long lastWalkTick = -1;
        ParticleSystem.EmitParams emit = new() { applyShapeToPosition = false };
        readonly List<(SpriteRenderer R, float Until, float Length)> flashes = new();
        int nextFlash;
        readonly List<SpriteRenderer> decals = new(), corpses = new();
        int nextDecal, nextCorpse;
        /// <summary>Per corpse slot: death launch from → to with a turn, then hold, then fade (v0.19 §19).</summary>
        readonly List<(float Born, Vector3 From, Vector3 To, float Rot0, float Rot1, Color Color)> corpseMotion = new();
        readonly List<(SpriteRenderer R, float Until, float Length)> tracers = new();
        /// <summary>Imported effect strips (muzzle flash, impact, explosion) playing once.</summary>
        readonly List<(SpriteRenderer R, SpriteSet Set, float Born)> fxPlays = new();
        int nextFx;
        int nextTracer;
        /// <summary>Expanding shock rings; Squash flattens a ring onto the ground (3/4 view). Life 0 = free.</summary>
        readonly List<(SpriteRenderer R, float Born, float Life, float From, float To, float Squash, Color Color)> rings = new();
        int nextRing;
        /// <summary>Dash afterimages: copies of the drone's current frame that fade in place.</summary>
        readonly List<(SpriteRenderer R, float Born, Color Color)> ghosts = new();
        int nextGhost;
        /// <summary>Small blood/oil stains where the spray lands; Born may lie ahead (the drop is still flying).</summary>
        readonly List<(SpriteRenderer R, float Born, Color Color)> stains = new();
        int nextStain;
        Sprite ringSprite;
        Texture2D ringTexture;
        float nextGhostAt;
        bool hadPendingCard, primed;
        readonly List<(TextMeshPro T, float Born, Vector3 From)> numbers = new();
        int nextNumber;
        float numberTokens;
        float lastShotShake = -1f;
        readonly Dictionary<int, (float Sum, Vector2 Pos)> pendingNumbers = new();
        readonly Dictionary<EnemyId, (Sprite Sprite, Color Color, Vector2 Scale, bool Art)> corpseLooks = new();
        FeedbackData data;

        /// <summary>Player settings (§11): shake 0–1, damage numbers on/off, effect budget 0–2.</summary>
        public void Configure(float shake, bool damageNumbers, int effectBudget)
        {
            shakeIntensity = Mathf.Clamp01(shake);
            numbersOn = damageNumbers;
            budgetLevel = effectBudget;
            if (data != null) ApplyCaps(data);
        }

        /// <summary>Enemies that fire a gun (robots and humans with ranged shots, not thrown bombs): muzzle flash and recoil.</summary>
        public static bool FiresAGun(World w, EnemyId id)
        {
            var d = w.Profile.Enemy(id);
            bool lobber = d.ExplosionRadius > 0 && d.FuseTime > 0;
            return d.Role == EnemyRole.Ranged && d.Faction != Faction.Animals && !lobber;
        }

        void Awake()
        {
            sparks = MakeSystem("Sparks", sparkSprite, 0f, 7f, AnimationCurve.Linear(0, 1, 1, 0), false, 10);
            gore = MakeSystem("Gore", blobSprite, 1.5f, 1.2f, AnimationCurve.Linear(0, 1, 1, 0.6f), true, 12);
            puffs = MakeSystem("Puffs", glowSprite, -0.04f, 3.5f, AnimationCurve.EaseInOut(0, 0.5f, 1, 1.8f), true, 8);
            motes = MakeSystem("Motes", sparkSprite, -0.3f, 0.8f, AnimationCurve.Linear(0, 1, 1, 0), true, 30);
            // Walking dust stays on the ground layer, under every body and wall; the drone's glints fall from under it.
            dust = MakeSystem("WalkDust", glowSprite, -0.02f, 2.5f, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.7f), true, -40);
            dust.GetComponent<ParticleSystemRenderer>().sortingLayerName = "Default";
            glints = MakeSystem("WalkGlints", glowSprite, 0.12f, 1.5f, AnimationCurve.Linear(0, 1, 1, 0.2f), true, -38);
            glints.GetComponent<ParticleSystemRenderer>().sortingLayerName = "Default";
            ringSprite = MakeRingSprite(out ringTexture);
        }

        void OnDestroy()
        {
            if (ringTexture != null) Destroy(ringTexture);
        }

        ParticleSystem MakeSystem(string systemName, Sprite sprite, float gravity, float drag, AnimationCurve size, bool fade, int order)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = gravity;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            if (drag > 0f)
            {
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.drag = drag;
            }
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, size);
            if (fade)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.55f), new GradientAlphaKey(0, 1) });
                col.color = g;
            }
            // Sprite material + sprite sheet: the 2D renderer does not draw the 3D particle shaders.
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            sheet.AddSprite(sprite);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = unlitMaterial;
            r.sortingLayerID = SortingLayers.Effects;
            r.sortingOrder = order;
            ps.Play();
            return ps;
        }

        /// <summary>A soft white ring one unit across, drawn in code so no art is needed.</summary>
        static Sprite MakeRingSprite(out Texture2D tex)
        {
            const int N = 128;
            tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "FxRing", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.84f) / 0.14f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        }

        void ApplyCaps(FeedbackData f)
        {
            float scale = f.BudgetScale(budgetLevel);
            int cap = Mathf.Max(16, Mathf.RoundToInt(f.MaxParticles * scale));
            var main = sparks.main;
            main.maxParticles = cap;
            var goreMain = gore.main;
            goreMain.maxParticles = Mathf.Max(16, cap / 2);
            var puffMain = puffs.main;
            puffMain.maxParticles = Mathf.Max(16, cap / 4);
            var moteMain = motes.main;
            moteMain.maxParticles = Mathf.Max(16, cap / 4);
            var dustMain = dust.main;
            dustMain.maxParticles = Mathf.Max(16, cap / 3);
            var glintMain = glints.main;
            glintMain.maxParticles = Mathf.Max(16, cap / 8);
            Resize(decals, Mathf.RoundToInt(f.MaxDecals * scale), "Decal", -95);
            Resize(corpses, Mathf.RoundToInt(f.MaxCorpses * scale), "Corpse", -60);
            while (corpseMotion.Count > corpses.Count) corpseMotion.RemoveAt(corpseMotion.Count - 1);
            while (corpseMotion.Count < corpses.Count) corpseMotion.Add(default);
            nextDecal %= Mathf.Max(1, decals.Count);
            nextCorpse %= Mathf.Max(1, corpses.Count);
        }

        void Resize(List<SpriteRenderer> list, int count, string objectName, int order)
        {
            while (list.Count > count) { Destroy(list[^1].gameObject); list.RemoveAt(list.Count - 1); }
            while (list.Count < count)
            {
                var go = new GameObject(objectName);
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
            foreach (var x in fxPlays) x.R.enabled = false;
            foreach (var n in numbers) n.T.enabled = false;
            for (int i = 0; i < rings.Count; i++) { rings[i].R.enabled = false; rings[i] = (rings[i].R, 0, 0, 0, 0, 1, default); }
            foreach (var g in ghosts) g.R.enabled = false;
            for (int i = 0; i < stains.Count; i++) { stains[i].R.enabled = false; stains[i] = (stains[i].R, float.MinValue, default); }
            sparks.Clear();
            gore.Clear();
            puffs.Clear();
            motes.Clear();
            dust.Clear();
            glints.Clear();
            lastWalkTick = -1;
            primed = false; // a loaded card is not a fresh level-up
        }

        int Scaled(int count) =>
            count <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(count * (data != null ? data.BudgetScale(budgetLevel) : 1f)));

        // ------------------------------------------------------------------ events

        public void OnEvent(World w, in SimEvent ev)
        {
            if (data != w.Profile.Feedback) { data = w.Profile.Feedback; ApplyCaps(data); }
            var f = data;
            switch (ev.Type)
            {
                case SimEventType.ShotFired:
                    if (ev.EntityId == PlayerState.EntityId) PlayerShot(ev, f);
                    else
                    {
                        Vector2 dir = Safe(ev.Direction);
                        Vector2 muzzle = presenter.CompanionMuzzle(dir, ev.Position + Vector2.up * 0.95f + dir * 0.6f);
                        GunFlash(muzzle, dir, 0.45f, MuzzleWarm, f);
                        Casing(muzzle - dir * 0.3f, dir);
                    }
                    break;

                case SimEventType.EnemyAttack when ev.Code >= 0 && ev.Direction.sqrMagnitude > 0.01f && w.Profile.Enemy((EnemyId)ev.Code).Role == EnemyRole.Ranged:
                {
                    Vector2 dir = Safe(ev.Direction);
                    Vector2 at = ev.Position + Vector2.up * 0.6f + dir * 0.5f;
                    if (FiresAGun(w, (EnemyId)ev.Code))
                    {
                        GunFlash(at, dir, 0.45f, MuzzleWarm, f);
                        Casing(at - dir * 0.3f, dir);
                    }
                    else Puff(at, dir, 2, Dust, 1.2f); // quills and thrown bombs: a puff, not a muzzle flash
                    break;
                }

                case SimEventType.EnemyAttack when ev.Code == -1: // boss impact
                    Shake(f.ShakeOnBossImpact, ev.Direction);
                    break;

                case SimEventType.MeleeSwing:
                {
                    Vector2 dir = Safe(ev.Direction);
                    float reach = ev.Direction.magnitude;
                    Burst(ev.Position + Vector2.up * 0.4f + dir * reach * 0.6f, dir, ev.Value, Scaled(7), new Color(0.75f, 0.95f, 1f), 4.5f);
                    Shake(f.ShakeOnMelee, dir);
                    break;
                }

                case SimEventType.ProjectileImpact:
                    if (presenter.TryProjectileColor(ev.EntityId, out var pc))
                    {
                        Burst(ev.Position, -ev.Direction, 70f, f.ImpactSparks, pc, 4f);
                        if (!Fx(presenter.Catalog.ImpactFx, ev.Position, 0.5f, -ev.Direction)) Flash(ev.Position, 0.35f, pc, 0.06f);
                    }
                    break;

                case SimEventType.EnemyHit:
                {
                    Vector2 at = ev.Position + Vector2.up * 0.5f;
                    var faction = FactionOf(w, ev.EntityId);
                    Burst(at, ev.Direction, 60f, f.ImpactSparks, SparkColor(faction), 5f);
                    if (faction != Faction.Robots && faction != null) Gore(at, ev.Direction, Scaled(3), BloodSpray, BloodChunk, 0.5f);
                    if (ev.Value > 0f)
                    {
                        pendingNumbers.TryGetValue(ev.EntityId, out var acc);
                        pendingNumbers[ev.EntityId] = (acc.Sum + ev.Value, ev.Position);
                    }
                    break;
                }

                case SimEventType.EnemyDied:
                    EnemyDeath(w, ev, f);
                    break;

                case SimEventType.Explosion:
                    Burst(ev.Position, Vector2.right, 360f, f.ExplosionSparks, new Color(1f, 0.7f, 0.35f), ev.Value * 4f);
                    if (!Fx(presenter.Catalog.ExplosionFx, ev.Position, ev.Value * 2.2f, Vector2.right))
                        Flash(ev.Position, ev.Value * 1.6f, new Color(1f, 0.85f, 0.6f), 0.12f);
                    Ring(ev.Position, 0.3f, ev.Value * 2.4f, 0.35f, new Color(1f, 0.8f, 0.55f, 0.7f), 0.55f);
                    Puff(ev.Position + Vector2.up * 0.3f, Vector2.up, Scaled(5), Smoke, ev.Value * 1.2f);
                    Decal(ev.Position, ev.Value * 1.5f, Scorch);
                    Shake(ev.EntityId == BossState.EntityId ? f.ShakeOnBossImpact : f.ShakeOnExplosion, Vector2.zero);
                    break;

                case SimEventType.PlayerHit:
                    Burst(ev.Position + Vector2.up * 0.4f, ev.Direction, 90f, f.ImpactSparks, new Color(0.7f, 0.95f, 1f), 5f);
                    Shake(f.ShakeOnPlayerHit, ev.Direction);
                    break;

                case SimEventType.PlayerDashStarted:
                    DashStart(ev, f);
                    break;

                case SimEventType.PlayerDashEnded:
                    Puff(ev.Position, Vector2.up, Scaled(3), Dust, 0.8f);
                    break;

                case SimEventType.PlayerDied:
                    PlayerDeath(ev, f);
                    break;

                case SimEventType.SkillChosen:
                {
                    Vector2 at = playerView.transform.position;
                    Ring(at, 0.3f, 2.2f, 0.45f, new Color(Gold.r, Gold.g, Gold.b, 0.8f), 0.55f);
                    Motes(at, Scaled(18), Gold, 0.6f);
                    break;
                }

                case SimEventType.CompanionHit:
                    Burst(ev.Position + Vector2.up * 0.9f, ev.Direction, 90f, f.ImpactSparks, Blood, 4f);
                    break;

                case SimEventType.BossDefeated:
                    Burst(ev.Position + Vector2.up, Vector2.right, 360f, f.ExplosionSparks * 2, new Color(1f, 0.8f, 0.5f), 9f);
                    bool robot = (BossId)ev.Code == BossId.IndustrialRobot;
                    Gore(ev.Position + Vector2.up, Vector2.zero, Scaled(f.DeathGore * 2), robot ? OilSpray : BloodSpray, robot ? MetalChunk : BloodChunk, 1.6f);
                    Ring(ev.Position, 0.5f, 6f, 0.6f, new Color(1f, 0.85f, 0.6f, 0.75f), 0.55f);
                    Decal(ev.Position, 5f, robot ? Oil : Blood);
                    Shake(f.ShakeOnBossDefeated, Vector2.zero);
                    break;
            }
        }

        void PlayerShot(in SimEvent ev, FeedbackData f)
        {
            var wv = presenter.Catalog.Weapon((WeaponId)ev.Code);
            Color c = wv != null ? wv.ProjectileColor : Color.white;
            Vector2 dir = Safe(ev.Direction);
            Vector2 muzzle = playerView.Muzzle;
            bool spread = ev.Value > 1;
            GunFlash(muzzle, dir, spread ? 0.75f : 0.55f, Color.Lerp(c, MuzzleWarm, 0.5f), f);
            Tracer(muzzle, ev.Direction, c, f);
            Casing((Vector2)playerView.transform.position + Vector2.up * 0.45f, dir);
            playerView.Kick(spread ? 1f : 0.5f);
            if (Time.unscaledTime - lastShotShake >= f.ShotShakeMinInterval)
            {
                lastShotShake = Time.unscaledTime;
                Shake(f.ShakeOnShot * (spread ? 2f : 1f), ev.Direction);
            }
        }

        /// <summary>Muzzle fire: the imported flash strip (or a glow), a light pop, a few sparks and a wisp of smoke.</summary>
        void GunFlash(Vector2 muzzle, Vector2 dir, float size, Color color, FeedbackData f)
        {
            if (!Fx(presenter.Catalog.MuzzleFlashFx, muzzle + dir * (size * 0.35f), size, dir))
                Flash(muzzle, size * 0.9f, color, f.MuzzleFlashTime);
            Flash(muzzle, size * 1.5f, color, f.MuzzleFlashTime * 1.6f); // the light the shot throws around
            Burst(muzzle, dir, 40f, Scaled(4), color, 5f);
            Puff(muzzle + dir * 0.1f, dir, Scaled(2), Smoke, 1.4f);
        }

        void EnemyDeath(World w, in SimEvent ev, FeedbackData f)
        {
            var type = (EnemyId)ev.Code;
            var d = w.Profile.Enemy(type);
            bool robot = d.Faction == Faction.Robots;
            Vector2 body = ev.Position + Vector2.up * 0.45f;
            Burst(body, ev.Direction, 200f, Scaled(f.DeathSparks), SparkColor(d.Faction), 7f);
            Flash(body, d.BodyRadius * 3f, Color.white, 0.07f); // the death pop
            Ring(ev.Position, 0.2f, d.BodyRadius * 4.5f, 0.3f, new Color(1f, 0.95f, 0.85f, 0.55f), 0.55f);
            if (d.Role == EnemyRole.Explosive) return; // its explosion leaves the scorch mark
            Gore(body, ev.Direction, Scaled(f.DeathGore), robot ? OilSpray : BloodSpray, robot ? MetalChunk : BloodChunk, 1f);
            Stains(ev.Position, ev.Direction, Scaled(7), robot ? Oil : Blood);
            if (!robot) Puff(body, ev.Direction, Scaled(3), new Color(BloodSpray.r, BloodSpray.g, BloodSpray.b, 0.45f), 1.6f); // blood mist
            if (robot)
            {
                Burst(body, Vector2.up, 120f, Scaled(10), new Color(1f, 0.7f, 0.3f), 6f);
                Puff(body, Vector2.up, Scaled(4), new Color(0.25f, 0.25f, 0.27f, 0.4f), 1.2f);
            }
            Decal(ev.Position + Random.insideUnitCircle * 0.2f, d.BodyRadius * 3.2f, robot ? Oil : Blood);
            Corpse(type, ev.Position, ev.Direction, f);
        }

        void DashStart(in SimEvent ev, FeedbackData f)
        {
            Vector2 dir = Safe(ev.Direction);
            Puff(ev.Position, -dir, Scaled(6), Dust, 2f);
            Ring(ev.Position, 0.2f, 1.7f, 0.25f, new Color(DashCyan.r, DashCyan.g, DashCyan.b, 0.6f), 0.55f);
            Burst(ev.Position + Vector2.up * 0.35f, -dir, 50f, Scaled(10), DashCyan, 6f);
            Shake(f.ShakeOnDash, dir);
            nextGhostAt = 0f;
        }

        void PlayerDeath(in SimEvent ev, FeedbackData f)
        {
            Vector2 at = ev.Position + Vector2.up * 0.5f;
            var pale = new Color(0.7f, 0.95f, 1f, 1f);
            Flash(at, 3f, pale, 0.3f);
            Ring(ev.Position, 0.3f, 4.5f, 0.6f, new Color(pale.r, pale.g, pale.b, 0.8f), 0.55f);
            Ring(at, 0.2f, 2.6f, 0.4f, new Color(1f, 1f, 1f, 0.7f), 1f, 0.05f);
            Ring(ev.Position, 0.3f, 3f, 0.7f, new Color(DashCyan.r, DashCyan.g, DashCyan.b, 0.6f), 0.55f, 0.18f);
            Burst(at, Vector2.up, 360f, Scaled(40), DashCyan, 9f);
            Gore(at, ev.Direction, Scaled(18), new Color(0.55f, 0.62f, 0.68f, 1f), new Color(0.2f, 0.22f, 0.25f, 1f), 1.2f);
            Puff(at, Vector2.up, Scaled(10), new Color(0.35f, 0.38f, 0.42f, 0.45f), 2.5f);
            Motes(at, Scaled(26), DashCyan, 1f);
            Decal(ev.Position, 2f, Scorch);
            Shake(f.ShakeOnPlayerDeath, Vector2.zero);
        }

        void LevelUp(Vector2 at)
        {
            Flash(at + Vector2.up * 0.5f, 2.4f, Gold, 0.35f);
            Ring(at, 0.3f, 3.4f, 0.55f, new Color(Gold.r, Gold.g, Gold.b, 0.9f), 0.55f);
            Ring(at, 0.3f, 2.2f, 0.5f, new Color(1f, 1f, 1f, 0.7f), 0.55f, 0.12f);
            Ring(at + Vector2.up * 0.5f, 0.2f, 1.8f, 0.35f, new Color(Gold.r, Gold.g, Gold.b, 0.7f), 1f);
            Burst(at + Vector2.up * 0.5f, Vector2.up, 360f, Scaled(28), Gold, 7f);
            Motes(at, Scaled(40), Gold, 1f);
        }

        /// <summary>After a frame's events: damage numbers (one per enemy per frame, so shotgun pellets add up), dash trail, level-up.</summary>
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

            var p = w.Player;
            if (p.Dashing && !p.Dead && Time.time >= nextGhostAt)
            {
                nextGhostAt = Time.time + GhostInterval;
                Ghost(playerView.DrawnBody);
                Motes((Vector2)playerView.transform.position + Vector2.up * 0.35f, Scaled(2), DashCyan, 0.3f);
            }

            Walking(w);

            // A level-up is the moment an XP card becomes due.
            bool pendingCard = w.Progress.Pending != null;
            if (primed && pendingCard && !hadPendingCard) LevelUp(playerView.transform.position);
            hadPendingCard = pendingCard;
            primed = true;
        }

        // ------------------------------------------------------------------ walking

        /// <summary>
        /// Movement trails for everything that walks, by distance covered in the ticks simulated since the last frame
        /// (nothing while the game is paused). Footfalls are scattered stochastically along the way.
        /// </summary>
        void Walking(World w)
        {
            long tick = w.Clock.Tick;
            long ticks = lastWalkTick < 0 ? 0 : tick - lastWalkTick;
            lastWalkTick = tick;
            if (ticks <= 0) return;

            Color ground = GroundDust[Mathf.Clamp(w.Phase - 1, 0, GroundDust.Length - 1)];

            var p = w.Player;
            if (!p.Dead && Moved(p.Position, p.PrevPosition, ticks, out Vector2 pd, out float pdist))
                for (int i = Steps(pdist, GlintStride); i > 0; i--) DroneGlint(p.Position, pd);

            var c = w.Companion;
            if (c.Present && !c.Dead && Moved(c.Position, c.PrevPosition, ticks, out Vector2 cd, out float cdist))
            {
                float r = w.Profile.Companion.BodyRadius;
                for (int i = Steps(cdist, Stride(r)); i > 0; i--) FootDust(c.Position, cd, r, ground);
            }

            foreach (var e in w.Enemies)
            {
                if (!e.Alive || e.Ai == EnemyAi.Spawning) continue;
                if (!Moved(e.Position, e.PrevPosition, ticks, out Vector2 ed, out float edist)) continue;
                var d = w.Profile.Enemy(e.Type);
                for (int i = Steps(edist, Stride(e.Radius)); i > 0; i--) EnemyStep(e.Type, d, e.Position, ed, e.Radius, ground);
            }

            var b = w.Boss;
            if (b.Active && !b.Dead && Moved(b.Position, b.PrevPosition, ticks, out Vector2 bd, out float bdist))
            {
                bool machine = BossSystem.Data(w).IsMachine;
                for (int i = Steps(bdist, Stride(b.Radius)); i > 0; i--)
                {
                    FootDust(b.Position, bd, b.Radius, ground);
                    if (machine && Random.value < 0.5f) Grind(b.Position, bd, b.Radius);
                }
            }
        }

        static float Stride(float radius) => StrideBase + radius * StridePerRadius;

        static bool Moved(Vector2 now, Vector2 before, long ticks, out Vector2 dir, out float distance)
        {
            Vector2 step = now - before;
            float len = step.magnitude;
            dir = len > 1e-5f ? step / len : Vector2.zero;
            distance = len * ticks;
            return len > 1e-4f && len < TeleportStep;
        }

        /// <summary>Whole footfalls in this distance, plus one more with the leftover as its chance.</summary>
        static int Steps(float distance, float stride)
        {
            float n = distance / Mathf.Max(0.01f, stride);
            int whole = (int)n;
            return whole + (Random.value < n - whole ? 1 : 0);
        }

        /// <summary>The drone hovers: a cyan glint drips from under its core and drifts back along its path.</summary>
        void DroneGlint(Vector2 at, Vector2 dir)
        {
            Vector2 from = at + Vector2.up * Random.Range(0.3f, 0.45f) + new Vector2(Random.Range(-0.18f, 0.18f), 0f);
            var v = -dir * Random.Range(0.3f, 0.9f) + Vector2.down * Random.Range(0.4f, 1f) + Random.insideUnitCircle * 0.2f;
            var col = Color.Lerp(DashCyan, Color.white, Random.Range(0f, 0.5f));
            col.a = Random.Range(0.75f, 1f);
            Emit(glints, from, v, Random.Range(0.14f, 0.3f), Random.Range(0.4f, 0.7f), col);
        }

        /// <summary>A soft puff of the arena's ground kicked up behind a footfall; heavier bodies raise bigger clouds.</summary>
        void FootDust(Vector2 at, Vector2 dir, float radius, Color ground)
        {
            float size = 0.3f + radius * 0.7f;
            Vector2 side = new Vector2(-dir.y, dir.x) * Random.Range(-radius, radius) * 0.6f;
            Vector2 foot = at + side + new Vector2(0f, Random.Range(-0.05f, 0.08f));
            var v = -dir * Random.Range(0.3f, 0.8f) + Vector2.up * Random.Range(0.1f, 0.35f) + Random.insideUnitCircle * 0.15f;
            var col = ground;
            col.a *= Random.Range(0.7f, 1.1f);
            Emit(dust, foot, v, size * Random.Range(0.8f, 1.2f), Random.Range(0.6f, 1.05f), col);
            // A smaller, denser puff right at the heel.
            Emit(dust, foot - dir * 0.05f, v * 0.4f, size * 0.55f, Random.Range(0.35f, 0.55f), col);
        }

        /// <summary>Metal feet and treads on concrete: a couple of short orange sparks low behind the body.</summary>
        void Grind(Vector2 at, Vector2 dir, float radius)
        {
            if (budgetLevel <= 0) return;
            Vector2 foot = at + new Vector2(Random.Range(-radius, radius) * 0.7f, Random.Range(0f, 0.12f));
            for (int i = 0; i < 2; i++)
            {
                var v = Rotate(-dir, Random.Range(-40f, 40f)) * Random.Range(1.5f, 3.2f) + Vector2.up * Random.Range(0.3f, 1.2f);
                Emit(sparks, foot, v, Random.Range(0.04f, 0.08f), Random.Range(0.1f, 0.22f), GrindSpark);
            }
        }

        /// <summary>Boots on gravel: a pebble hops out of the dust.</summary>
        void Pebble(Vector2 at, Vector2 dir)
        {
            if (budgetLevel <= 0) return;
            var v = -dir * Random.Range(0.4f, 1.1f) + Vector2.up * Random.Range(1f, 1.8f) + Random.insideUnitCircle * 0.3f;
            Emit(gore, at + Random.insideUnitCircle * 0.1f, v, Random.Range(0.04f, 0.07f), Random.Range(0.25f, 0.4f), Grit);
        }

        /// <summary>A hovering robot's warm exhaust, dropping from its belly.</summary>
        void Thruster(Vector2 at, Vector2 dir)
        {
            Vector2 from = at + Vector2.up * Random.Range(0.25f, 0.4f) + new Vector2(Random.Range(-0.1f, 0.1f), 0f);
            var v = -dir * Random.Range(0.3f, 0.8f) + Vector2.down * Random.Range(0.3f, 0.8f);
            var col = Color.Lerp(Exhaust, Gold, Random.Range(0f, 0.5f));
            Emit(glints, from, v, Random.Range(0.08f, 0.16f), Random.Range(0.25f, 0.45f), col);
        }

        void EnemyStep(EnemyId id, EnemyData d, Vector2 at, Vector2 dir, float radius, Color ground)
        {
            if (Hovers(id)) { Thruster(at, dir); return; }
            FootDust(at, dir, radius, ground);
            switch (d.Faction)
            {
                case Faction.Robots:
                    if (Random.value < (d.Role == EnemyRole.Charger ? 0.9f : 0.4f)) Grind(at, dir, radius);
                    break;
                case Faction.Humans:
                    if (Random.value < 0.5f) Pebble(at, dir);
                    break;
            }
        }

        /// <summary>Enemies that fly instead of walking (no dust).</summary>
        static bool Hovers(EnemyId id) => id == EnemyId.R7c_Drone;

        // ------------------------------------------------------------------ layers

        static Vector2 Safe(Vector2 dir) => dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.right;

        void Emit(ParticleSystem ps, Vector2 at, Vector2 velocity, float size, float life, Color color)
        {
            emit.position = new Vector3(at.x, at.y, 0);
            emit.velocity = new Vector3(velocity.x, velocity.y, 0);
            emit.startSize = size;
            emit.startLifetime = life;
            emit.startColor = color;
            ps.Emit(emit, 1);
        }

        void Burst(Vector2 at, Vector2 dir, float spreadDegrees, int count, Color color, float speed)
        {
            if (count <= 0) return;
            float baseAngle = dir.sqrMagnitude > 1e-4f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 0f;
            for (int i = 0; i < count; i++)
            {
                float a = (baseAngle + Random.Range(-spreadDegrees, spreadDegrees) * 0.5f) * Mathf.Deg2Rad;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.5f, 1.2f);
                Emit(sparks, at, v, Random.Range(0.06f, 0.14f), Random.Range(0.15f, 0.32f), color);
            }
        }

        /// <summary>Droplets thrown along the hit, arcing down onto the ground, plus a few heavier chunks.</summary>
        void Gore(Vector2 at, Vector2 dir, int count, Color spray, Color chunk, float power)
        {
            if (count <= 0) return;
            bool aimed = dir.sqrMagnitude > 1e-4f;
            Vector2 n = aimed ? dir.normalized : Vector2.up;
            for (int i = 0; i < count; i++)
            {
                Vector2 d = aimed ? Rotate(n, Random.Range(-55f, 55f)) : Random.insideUnitCircle.normalized;
                var v = d * Random.Range(2f, 7f) * power + Vector2.up * Random.Range(1.5f, 4f);
                float shade = Random.Range(0.8f, 1.15f);
                var c = new Color(Mathf.Min(1f, spray.r * shade), Mathf.Min(1f, spray.g * shade), Mathf.Min(1f, spray.b * shade), spray.a);
                Emit(gore, at + Random.insideUnitCircle * 0.1f, v, Random.Range(0.08f, 0.24f), Random.Range(0.35f, 0.65f), c);
            }
            for (int i = 0; i < Mathf.Max(1, count / 6); i++)
            {
                Vector2 d = aimed ? Rotate(n, Random.Range(-40f, 40f)) : Random.insideUnitCircle.normalized;
                Emit(gore, at, d * Random.Range(1.5f, 4f) * power + Vector2.up * Random.Range(2f, 4f), Random.Range(0.2f, 0.36f), Random.Range(0.5f, 0.8f), chunk);
            }
        }

        void Puff(Vector2 at, Vector2 dir, int count, Color color, float speed)
        {
            Vector2 n = Safe(dir);
            for (int i = 0; i < count; i++)
            {
                var v = Rotate(n, Random.Range(-35f, 35f)) * speed * Random.Range(0.4f, 1f) + Random.insideUnitCircle * 0.4f;
                Emit(puffs, at + Random.insideUnitCircle * 0.12f, v, Random.Range(0.35f, 0.6f), Random.Range(0.35f, 0.7f), color);
            }
        }

        /// <summary>Glints rising from a ring around the feet (3/4 view, so the ring is flattened).</summary>
        void Motes(Vector2 at, int count, Color color, float strength)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(0.2f, 0.9f);
                ring.y *= 0.55f;
                var v = Vector2.up * Random.Range(1.5f, 4f) * strength + ring * 0.6f;
                var c = Color.Lerp(color, Color.white, Random.Range(0f, 0.45f));
                Emit(motes, at + ring, v, Random.Range(0.08f, 0.2f), Random.Range(0.8f, 1.4f), c);
            }
        }

        /// <summary>One brass casing kicked out of the weapon, up and back.</summary>
        void Casing(Vector2 at, Vector2 shotDir)
        {
            if (budgetLevel <= 0) return;
            var v = Vector2.up * Random.Range(2f, 3f) - shotDir * Random.Range(0.6f, 1.4f) + Random.insideUnitCircle * 0.4f;
            Emit(gore, at, v, 0.08f, 0.45f, Brass);
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        void Ring(Vector2 at, float from, float to, float life, Color color, float squash, float delay = 0f)
        {
            if (rings.Count < RingPool)
            {
                var go = new GameObject("Ring");
                go.transform.SetParent(transform, false);
                var added = go.AddComponent<SpriteRenderer>();
                added.sprite = ringSprite;
                added.sharedMaterial = unlitMaterial;
                added.sortingLayerID = SortingLayers.Effects;
                added.sortingOrder = 18;
                added.enabled = false;
                rings.Add((added, 0, 0, 0, 0, 1, default));
            }
            int i = nextRing;
            nextRing = (nextRing + 1) % RingPool;
            var r = rings[i].R;
            r.transform.position = new Vector3(at.x, at.y, 0);
            r.enabled = false; // shown by LateUpdate once its delay has passed
            rings[i] = (r, Time.time + delay, Mathf.Max(0.05f, life), from, to, squash, color);
        }

        void Ghost(SpriteRenderer source)
        {
            if (source == null || source.sprite == null) return;
            if (ghosts.Count < GhostPool)
            {
                var go = new GameObject("DashGhost");
                go.transform.SetParent(transform, false);
                var added = go.AddComponent<SpriteRenderer>();
                added.sharedMaterial = unlitMaterial;
                added.enabled = false;
                ghosts.Add((added, 0, default));
            }
            int i = nextGhost;
            nextGhost = (nextGhost + 1) % GhostPool;
            var r = ghosts[i].R;
            r.sprite = source.sprite;
            r.flipX = source.flipX;
            r.flipY = source.flipY;
            r.sortingLayerID = source.sortingLayerID;
            r.sortingOrder = source.sortingOrder - 1;
            var t = source.transform;
            r.transform.SetPositionAndRotation(t.position, t.rotation);
            r.transform.localScale = t.lossyScale;
            var c = new Color(DashCyan.r, DashCyan.g, DashCyan.b, 0.55f);
            r.color = c;
            r.enabled = true;
            ghosts[i] = (r, Time.time, c);
        }

        /// <summary>Small stains sprayed along the hit; each appears when its drop would land.</summary>
        void Stains(Vector2 at, Vector2 dir, int count, Color color)
        {
            Vector2 n = Safe(dir);
            for (int k = 0; k < count; k++)
            {
                if (stains.Count < StainPool)
                {
                    var go = new GameObject("Stain");
                    go.transform.SetParent(transform, false);
                    var added = go.AddComponent<SpriteRenderer>(); // lit, like the decals
                    added.sprite = blobSprite;
                    added.sortingOrder = -94;
                    added.enabled = false;
                    stains.Add((added, float.MinValue, default));
                }
                int i = nextStain;
                nextStain = (nextStain + 1) % StainPool;
                var r = stains[i].R;
                float distance = Random.Range(0.4f, 1.8f);
                Vector2 side = new Vector2(-n.y, n.x) * Random.Range(-0.35f, 0.35f);
                Vector2 p = at + n * distance + side;
                r.transform.position = new Vector3(p.x, p.y, 0);
                r.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                float s = Random.Range(0.2f, 0.6f);
                r.transform.localScale = new Vector3(s, s * 0.6f, 1);
                r.color = color;
                r.enabled = false;
                stains[i] = (r, Time.time + 0.1f + distance * 0.15f, color);
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

        /// <summary>Plays an imported effect strip once at a size in units; false when there is no art for it.</summary>
        bool Fx(SpriteSet set, Vector2 at, float size, Vector2 dir)
        {
            var state = set != null ? set.Resolve("burst") : null;
            if (state == null) return false;
            const int Pool = 24;
            if (fxPlays.Count < Pool)
            {
                var go = new GameObject("Fx");
                go.transform.SetParent(transform, false);
                var added = go.AddComponent<SpriteRenderer>();
                added.sharedMaterial = unlitMaterial;
                added.sortingLayerID = SortingLayers.Effects;
                added.sortingOrder = 20;
                fxPlays.Add((added, null, 0f));
            }
            int i = nextFx;
            nextFx = (nextFx + 1) % Pool;
            var r = fxPlays[i].R;
            var first = state.Frames[0];
            r.sprite = first;
            r.color = Color.white;
            float width = Mathf.Max(0.01f, first.bounds.size.x);
            r.transform.position = new Vector3(at.x, at.y, 0);
            r.transform.rotation = Quaternion.Euler(0, 0, dir.sqrMagnitude > 1e-4f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 0f);
            r.transform.localScale = new Vector3(size / width, size / width, 1);
            r.enabled = true;
            fxPlays[i] = (r, set, Time.time);
            return true;
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
                var anim = prefab != null ? prefab.GetComponent<SpriteSetAnimator>() : null;
                var death = anim != null && anim.Set != null ? anim.Set.Find("death") : null;
                look = death != null && main != null ? (death.Frames[^1], Color.white, (Vector2)main.transform.lossyScale, true)
                    : main != null ? (main.sprite, main.color, (Vector2)main.transform.lossyScale, false) : (null, Color.clear, Vector2.one, false);
                corpseLooks[type] = look;
            }
            if (look.Sprite == null) return;
            int i = nextCorpse;
            var r = corpses[i];
            nextCorpse = (nextCorpse + 1) % corpses.Count;
            r.sprite = look.Sprite;
            // Drawn death art already lies on the ground: slightly darker, facing back along the hit, no turn.
            float shade = look.Art ? 0.8f : 0.45f;
            var color = new Color(look.Color.r * shade, look.Color.g * shade, look.Color.b * shade, 1f);
            r.color = color;
            r.flipX = look.Art && dir.x > 0;
            float side = dir.x < 0 ? 1f : -1f;
            // Death launch (v0.19 §19): visual only, thrown along the killing hit; it blocks nothing and deals no damage.
            Vector2 push = dir.sqrMagnitude > 1e-4f ? dir.normalized * f.DeathLaunchDistance : Vector2.zero;
            var from = new Vector3(at.x, at.y + 0.1f, 0);
            var to = from + (Vector3)push;
            float rot1 = look.Art ? 0f : 90f * side + Random.Range(-12f, 12f); // lying on its side
            r.transform.position = from;
            r.transform.rotation = Quaternion.identity;
            float corpseScale = look.Art ? 1f : 0.8f;
            r.transform.localScale = new Vector3(look.Scale.x * corpseScale, look.Scale.y * corpseScale, 1);
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

        void AnimateExtras(float now)
        {
            for (int i = 0; i < rings.Count; i++)
            {
                var (r, born, life, from, to, squash, color) = rings[i];
                if (life <= 0f) continue;
                float k = (now - born) / life;
                if (k < 0f) continue;
                if (k >= 1f) { r.enabled = false; rings[i] = (r, 0, 0, 0, 0, 1, default); continue; }
                float ease = 1f - (1f - k) * (1f - k) * (1f - k);
                float s = Mathf.Lerp(from, to, ease);
                r.transform.localScale = new Vector3(s, s * squash, 1);
                var c = color;
                c.a = color.a * (1f - k) * (1f - k);
                r.color = c;
                r.enabled = true;
            }
            for (int i = 0; i < ghosts.Count; i++)
            {
                var (r, born, color) = ghosts[i];
                if (!r.enabled) continue;
                float k = (now - born) / GhostLife;
                if (k >= 1f) { r.enabled = false; continue; }
                var c = color;
                c.a = color.a * (1f - k);
                r.color = c;
            }
            for (int i = 0; i < stains.Count; i++)
            {
                var (r, born, color) = stains[i];
                if (born == float.MinValue) continue;
                float age = now - born;
                if (age < 0f) continue;
                if (age >= StainHold + StainFade) { r.enabled = false; stains[i] = (r, float.MinValue, default); continue; }
                var c = color;
                if (age > StainHold) c.a = color.a * (1f - (age - StainHold) / StainFade);
                r.color = c;
                r.enabled = true;
            }
        }

        void Shake(float amount, Vector2 dir)
        {
            if (amount <= 0f || shakeIntensity <= 0f || cameraRig == null || data == null) return;
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
            for (int i = 0; i < fxPlays.Count; i++)
            {
                var (r, set, born) = fxPlays[i];
                if (!r.enabled || set == null) continue;
                var frames = set.Resolve("burst").Frames;
                int frame = (int)((now - born) * set.FramesPerSecond);
                if (frame >= frames.Count) { r.enabled = false; continue; }
                r.sprite = frames[frame];
            }
            AnimateExtras(now);
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
