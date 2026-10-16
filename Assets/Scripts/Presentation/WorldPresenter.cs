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

        readonly EntityViews<EnemyView> enemies = new();
        readonly EntityViews<ProjectileView> projectiles = new();
        readonly EntityViews<CoinView> coins = new();
        readonly EntityViews<ArcView> hazards = new();
        readonly ArcView[] flashes = new ArcView[12];
        int nextFlash;
        readonly EntityViews<ArcView> mines = new();
        ArcView previewArea, previewRay, shieldRing;
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
            enemies.ReleaseUnseen();
            projectiles.ReleaseUnseen();
            coins.ReleaseUnseen();
            hazards.ReleaseUnseen();
            mines.ReleaseUnseen();
        }

        public void Sync(World w, float alpha)
        {
            HandleEvents(w);
            playerView.Sync(w, alpha, catalog.Weapon(w.Player.Loadout.ActiveId));
            if (companionView != null) companionView.Sync(w, alpha);

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
                if (created) v.Begin(p, ProjectileColor(p));
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
                c.a = Mathf.Lerp(0.15f, 0.55f, progress);
                v.Set(h.Position, Vector2.right, h.Radius, 360f, c, 400);
            }
            hazards.ReleaseUnseen();

            SyncItems(w, alpha);
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
                        previewRay.Set(pos, to, to.magnitude, 1.5f, new Color(0.6f, 0.95f, 1f, 0.5f), 450);
                        previewArea.Set(target, Vector2.right, d.Radius, 360f, cyan, 449);
                        break;
                    case ItemId.Mine:
                        previewArea.Set(pos, Vector2.right, d.TriggerRadius, 360f, cyan, 449);
                        break;
                    case ItemId.PersonalShield:
                        previewArea.Set(pos, Vector2.right, 0.7f, 360f, cyan, 449);
                        break;
                    default:
                        previewArea.Set(pos, Vector2.right, d.Radius, 360f, cyan, 449);
                        break;
                }
            }

            bool shield = w.Clock.Tick < inv.ShieldUntilTick;
            if (shield) shieldRing.Set(pos + Vector2.up * 0.35f, Vector2.right, 0.6f, 360f, new Color(0.6f, 0.95f, 1f, 0.3f + 0.1f * Mathf.Sin(Time.time * 8f)), 460);
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
            foreach (var ev in w.Events)
            {
                switch (ev.Type)
                {
                    case SimEventType.EnemyHit:
                        if (enemies.TryGet(ev.EntityId, out var ev1)) ev1.Flash(flash);
                        break;
                    case SimEventType.Explosion:
                        var f = flashes[nextFlash];
                        nextFlash = (nextFlash + 1) % flashes.Length;
                        f.Flash(ev.Position, Vector2.right, ev.Value, 360f, new Color(1f, 0.85f, 0.6f, 0.6f), 0.15f);
                        break;
                    case SimEventType.CompanionHit:
                        if (companionView != null) companionView.Flash(flash);
                        break;
                    case SimEventType.MeleeSwing:
                        float reach = ev.Direction.magnitude;
                        swingArc.Flash(ev.Position, ev.Direction, reach, ev.Value, new Color(0.7f, 0.95f, 1f, 0.35f), 0.1f);
                        break;
                }
            }
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

        Color ProjectileColor(ProjectileState p)
        {
            if (p.Owner == ProjectileOwner.Enemy) return catalog.Telegraph;
            var wv = catalog.Weapon(p.Weapon);
            return wv != null ? wv.ProjectileColor : Color.white;
        }
    }
}
