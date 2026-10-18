using Hollow.Data;
using Hollow.Sim;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>
    /// Mirrors the simulation into pooled views every rendered frame and reacts to simulation events.
    /// Read-only with respect to gameplay.
    /// </summary>
    public sealed class WorldPresenter : MonoBehaviour
    {
        [SerializeField] VisualCatalog catalog;
        [SerializeField] PlayerView playerView;
        [SerializeField] CompanionView companionView;
        [SerializeField] FeedbackDirector feedback;

        readonly EntityViews<EnemyView> enemies = new();
        readonly EntityViews<ProjectileView> projectiles = new();
        readonly EntityViews<CoinView> coins = new();
        readonly EntityViews<ArcView> hazards = new();
        readonly ArcView[] flashes = new ArcView[12];
        int nextFlash;
        readonly EntityViews<ArcView> mines = new();
        /// <summary>Thrown grenades: the art and its ground shadow, one pair per hazard still to go off.</summary>
        readonly System.Collections.Generic.List<(SpriteRenderer Body, SpriteRenderer Shadow)> grenades = new();
        ArcView previewArea, previewRay, shieldRing;
        readonly System.Collections.Generic.Dictionary<BossId, BossView> bossViews = new();
        readonly System.Collections.Generic.Dictionary<EnemyId, ViewPool<EnemyView>> poolsByType = new();
        ViewPool<ProjectileView> playerShots, enemyShots;
        ViewPool<CoinView> coinPool;
        ViewPool<ArcView> arcPool;
        ArcView swingArc;

        public VisualCatalog Catalog => catalog;

        void Awake()
        {
            playerShots = new ViewPool<ProjectileView>(catalog.PlayerProjectile, transform);
            enemyShots = new ViewPool<ProjectileView>(catalog.EnemyProjectile, transform);
            coinPool = new ViewPool<CoinView>(catalog.Coin, transform);
            arcPool = new ViewPool<ArcView>(catalog.Arc, transform);
            swingArc = arcPool.Get();
            swingArc.Hide();
            for (int i = 0; i < flashes.Length; i++) { flashes[i] = Instantiate(catalog.Arc, transform); flashes[i].Hide(); }
            previewArea = Instantiate(catalog.Arc, transform);
            previewRay = Instantiate(catalog.Arc, transform);
            shieldRing = Instantiate(catalog.Arc, transform);
        }

        /// <summary>Drops every entity view (used when the world is replaced by a load or a retry).</summary>
        public void ResetViews()
        {
            AudioDirector.Instance?.ResetWorldSounds();
            enemies.ReleaseUnseen();
            projectiles.ReleaseUnseen();
            coins.ReleaseUnseen();
            hazards.ReleaseUnseen();
            mines.ReleaseUnseen();
            if (feedback != null) feedback.Clear();
        }

        /// <summary>Tip of the Operator's rifle for a shot in this direction (muzzle flash position).</summary>
        public Vector2 CompanionMuzzle(Vector2 shotDirection, Vector2 fallback) =>
            companionView != null && companionView.isActiveAndEnabled ? companionView.Muzzle(shotDirection) : fallback;

        /// <summary>Colour of a projectile that is still shown (its impact sparks use it).</summary>
        public bool TryProjectileColor(int id, out Color color)
        {
            color = Color.white;
            if (!projectiles.TryGet(id, out var v)) return false;
            color = v.Color;
            return true;
        }

        public void Sync(World w, float alpha)
        {
            HandleEvents(w);
            playerView.Sync(w, alpha, catalog.Weapon(w.Player.Loadout.ActiveId));
            if (companionView != null) companionView.Sync(w, alpha);
            SyncBoss(w, alpha);

            foreach (var e in w.Enemies)
            {
                if (!e.Alive) continue;
                var v = enemies.GetOrCreate(e.Id, PoolFor(e.Type), out bool created);
                if (created) v.ResetView();
                v.Sync(w, e, alpha, catalog.Telegraph);
            }
            enemies.ReleaseUnseen();

            foreach (var p in w.Projectiles)
            {
                var pool = p.Owner == ProjectileOwner.Enemy ? enemyShots : playerShots;
                var v = projectiles.GetOrCreate(p.Id, pool, out bool created);
                if (created) v.Begin(p, ProjectileColor(p), MuzzleOf(w, p), catalog.ProjectileArt(p, w.Phase));
                if (created && p.Owner == ProjectileOwner.Companion && companionView != null) companionView.Fired(p.Velocity);
                v.Sync(p, alpha);
            }
            projectiles.ReleaseUnseen();

            foreach (var c in w.Coins)
            {
                var v = coins.GetOrCreate(c.Id, coinPool, out bool created);
                if (created) v.Begin(c.Id);
                v.Sync(w, c, alpha);
            }
            coins.ReleaseUnseen();

            // Marked hazard zones: visible for the whole delay, brighter as detonation nears (§5, §7).
            foreach (var h in w.Hazards)
            {
                var v = hazards.GetOrCreate(h.Id, arcPool, out _);
                float progress = Mathf.Clamp01((w.Clock.Tick - h.CreatedTick) / (float)Mathf.Max(1, h.DetonateTick - h.CreatedTick));
                var c = h.Owner == HazardOwner.Enemy ? catalog.Telegraph : new Color(0.6f, 0.95f, 1f);
                c.a = h.Owner == HazardOwner.Enemy ? Mathf.Lerp(0.1f, 0.4f, progress) : Mathf.Lerp(0.15f, 0.55f, progress);
                v.Set(h.Position, Vector2.right, h.Radius, 360f, c, 400, SortingLayers.Telegraphs);
            }
            hazards.ReleaseUnseen();
            SyncGrenades(w, alpha);

            SyncItems(w, alpha);
        }

        /// <summary>
        /// A thrown grenade flies in an arc from the thrower to its marked point, then lies there blinking faster until
        /// it goes off (the marked circle is drawn by the hazard view). Visual only.
        /// </summary>
        void SyncGrenades(World w, float alpha)
        {
            int used = 0;
            float now = w.Clock.Tick + alpha;
            foreach (var h in w.Hazards)
            {
                if (h.Cancelled || h.LandTick <= h.CreatedTick) continue;
                var art = h.Owner == HazardOwner.Player && catalog.PlayerGrenadeArt != null ? catalog.PlayerGrenadeArt : catalog.EnemyGrenadeArt;
                if (art == null) continue;
                if (used == grenades.Count) grenades.Add((NewGrenadeRenderer("Grenade", SortingLayers.Effects, 20), NewGrenadeRenderer("GrenadeShadow", 0, -40)));
                var (body, shadow) = grenades[used++];
                float t = Mathf.Clamp01((now - h.CreatedTick) / (h.LandTick - h.CreatedTick));
                Vector2 ground = Vector2.Lerp(h.LaunchFrom, h.Position, t);
                float distance = Vector2.Distance(h.LaunchFrom, h.Position);
                float height = Mathf.Clamp(0.5f + distance * 0.18f, 0.5f, 2.6f) * 4f * t * (1f - t);
                float lift = Mathf.Lerp(0.6f, 0.12f, t); // leaves the hand, comes to rest on the ground
                body.sprite = shadow.sprite = art;
                body.transform.position = ground + Vector2.up * (height + lift);
                float spin = t < 1f ? -540f * t * Mathf.Sign(h.Position.x - h.LaunchFrom.x + 0.001f) : 0f;
                body.transform.rotation = Quaternion.Euler(0, 0, spin);
                // On the ground it blinks red, faster as the fuse runs out.
                float left = Mathf.Max(0f, (h.DetonateTick - now) * Core.SimClock.Dt);
                float blink = t < 1f ? 0f : Mathf.Max(0f, Mathf.Sin(Time.time * Mathf.Lerp(28f, 9f, Mathf.Clamp01(left))));
                body.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.35f), blink * 0.7f);
                shadow.transform.position = ground;
                float shrink = 1f / (1f + height * 0.6f);
                shadow.transform.localScale = new Vector3(0.9f * shrink, 0.35f * shrink, 1);
                shadow.color = new Color(0, 0, 0, 0.45f * shrink);
                body.enabled = shadow.enabled = true;
            }
            for (int i = used; i < grenades.Count; i++) grenades[i].Body.enabled = grenades[i].Shadow.enabled = false;
        }

        SpriteRenderer NewGrenadeRenderer(string name, int layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            var shot = catalog.EnemyProjectile != null ? catalog.EnemyProjectile.GetComponentInChildren<SpriteRenderer>(true) : null;
            if (shot != null) r.sharedMaterial = shot.sharedMaterial; // unlit, like every shot: readable in the dark phases
            if (layer != 0) r.sortingLayerID = layer;
            r.sortingOrder = order;
            return r;
        }

        void SyncBoss(World w, float alpha)
        {
            foreach (var kv in bossViews) kv.Value.gameObject.SetActive(w.Boss.Active && kv.Key == w.Boss.Id);
            if (!w.Boss.Active) return;
            if (!bossViews.TryGetValue(w.Boss.Id, out var view))
            {
                var prefab = catalog.BossPrefab(w.Boss.Id);
                if (prefab == null) return;
                view = Instantiate(prefab, transform);
                bossViews[w.Boss.Id] = view;
            }
            view.gameObject.SetActive(true);
            view.Sync(w, alpha, catalog.Telegraph);
        }

        /// <summary>Item previews (§10): grenade landing point and blast, mine under the player, pulse/shield ranges.</summary>
        void SyncItems(World w, float alpha)
        {
            var inv = w.Inventory;
            Vector2 pos = Vector2.Lerp(w.Player.PrevPosition, w.Player.Position, alpha);
            var cyan = new Color(0.6f, 0.95f, 1f, 0.15f);
            previewRay.Hide();
            previewArea.Hide();
            if (inv.Holding)
            {
                var d = w.Profile.Item(inv.Held);
                switch (inv.Held)
                {
                    case ItemId.Grenade:
                        Vector2 target = ItemSystem.GrenadeTarget(w);
                        Vector2 to = target - pos;
                        previewRay.Set(pos, to, to.magnitude, 1.5f, new Color(0.6f, 0.95f, 1f, 0.5f), 450, SortingLayers.Telegraphs);
                        previewArea.Set(target, Vector2.right, d.Radius, 360f, cyan, 449, SortingLayers.Telegraphs);
                        break;
                    case ItemId.Mine:
                        previewArea.Set(pos, Vector2.right, d.TriggerRadius, 360f, cyan, 449, SortingLayers.Telegraphs);
                        break;
                    case ItemId.PersonalShield:
                        previewArea.Set(pos, Vector2.right, 0.7f, 360f, cyan, 449, SortingLayers.Telegraphs);
                        break;
                    default:
                        previewArea.Set(pos, Vector2.right, d.Radius, 360f, cyan, 449, SortingLayers.Telegraphs);
                        break;
                }
            }

            // Visible shield time (§14): the ring empties as the shield runs out.
            bool shield = w.Clock.Tick < inv.ShieldUntilTick;
            if (shield)
            {
                long total = System.Math.Max(1, w.Ticks(w.Profile.Item(ItemId.PersonalShield).Duration));
                float left = Mathf.Clamp01((inv.ShieldUntilTick - w.Clock.Tick) / (float)total);
                shieldRing.Set(pos + Vector2.up * 0.35f, Vector2.up, 0.6f, 360f * left, new Color(0.6f, 0.95f, 1f, 0.35f + 0.1f * Mathf.Sin(Time.time * 8f)), 460, SortingLayers.Effects);
            }
            else shieldRing.Hide();

            foreach (var m in inv.Mines)
            {
                var v = mines.GetOrCreate(m.Id, arcPool, out _);
                bool armed = w.Clock.Tick >= m.ArmedTick;
                float a = armed ? 0.8f : 0.3f + 0.3f * Mathf.Sin(Time.time * 20f);
                v.Set(m.Position, Vector2.right, 0.22f, 360f, new Color(0.6f, 0.95f, 1f, a), 10);
            }
            mines.ReleaseUnseen();
        }

        void HandleEvents(World w)
        {
            float flash = w.Profile.Feedback.HitFlashDuration;
            var audio = AudioDirector.Instance;
            foreach (var ev in w.Events)
            {
                if (feedback != null) feedback.OnEvent(w, ev);
                if (audio != null) audio.OnSimEvent(w, ev);
                switch (ev.Type)
                {
                    case SimEventType.EnemyHit:
                        if (enemies.TryGet(ev.EntityId, out var ev1)) ev1.Flash(flash, w.Profile.Feedback.HitSquash);
                        else if (ev.EntityId == BossState.EntityId && w.Boss.Active && bossViews.TryGetValue(w.Boss.Id, out var bv)) bv.Flash(flash);
                        break;
                    case SimEventType.Explosion:
                        var f = flashes[nextFlash];
                        nextFlash = (nextFlash + 1) % flashes.Length;
                        f.Flash(ev.Position, Vector2.right, ev.Value, 360f, new Color(1f, 0.85f, 0.6f, 0.6f), 0.15f, SortingLayers.Effects);
                        break;
                    case SimEventType.CompanionHit:
                        if (companionView != null) companionView.Flash(flash);
                        break;
                    case SimEventType.EnemyAttack when ev.Code >= 0 && ev.Direction.sqrMagnitude > 0.01f && FeedbackDirector.FiresAGun(w, (EnemyId)ev.Code):
                        if (enemies.TryGet(ev.EntityId, out var shooter)) shooter.Kick(ev.Direction);
                        break;
                    // Support actions have their own drawings (heal, produce, build): shown for a moment.
                    case SimEventType.EnemyHealed:
                        if (enemies.TryGet(ev.Code, out var medic)) medic.Act("heal");
                        break;
                    case SimEventType.EnemySpawned:
                        foreach (var child in w.Enemies)
                            if (child.Id == ev.EntityId)
                            {
                                if (child.ParentId != 0 && enemies.TryGet(child.ParentId, out var maker)) maker.Act("produce", "build");
                                break;
                            }
                        break;
                    case SimEventType.MeleeSwing:
                        float reach = ev.Direction.magnitude;
                        swingArc.Flash(ev.Position, ev.Direction, reach, ev.Value, new Color(0.7f, 0.95f, 1f, 0.35f), 0.1f);
                        break;
                }
            }
            if (feedback != null) feedback.EndFrame(w);
            if (audio != null) audio.Ambient(w); // presence sounds of visible enemies (v0.19 §20)
        }

        ViewPool<EnemyView> PoolFor(EnemyId id)
        {
            if (!poolsByType.TryGetValue(id, out var pool))
            {
                pool = new ViewPool<EnemyView>(catalog.EnemyPrefab(id), transform);
                poolsByType[id] = pool;
            }
            return pool;
        }

        /// <summary>Where a new shot leaves its weapon on screen; enemy shots start at their body.</summary>
        Vector2? MuzzleOf(World w, ProjectileState p) => p.Owner switch
        {
            ProjectileOwner.Player => playerView.Muzzle,
            ProjectileOwner.Companion when companionView != null => companionView.Muzzle(p.Velocity),
            _ => null,
        };

        Color ProjectileColor(ProjectileState p)
        {
            if (p.Owner == ProjectileOwner.Enemy) return catalog.Telegraph;
            var wv = catalog.Weapon(p.Weapon);
            return wv != null ? wv.ProjectileColor : Color.white;
        }
    }
}
