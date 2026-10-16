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
