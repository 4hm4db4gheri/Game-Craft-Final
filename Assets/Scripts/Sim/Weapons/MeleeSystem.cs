using Hollow.Core.Input;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// Melee weapons (GDD §8): each attack hits every valid target inside the arc and reach exactly once,
    /// applies knockback, never locks movement, has no magazine. Obstacles block the swing (D23).
    /// </summary>
    public static class MeleeSystem
    {
        static readonly System.Collections.Generic.List<EnemyState> Candidates = new(32);

        public static void Step(World w, in PlayerCommand cmd, OwnedWeapon active, WeaponLevel l)
        {
            var L = w.Player.Loadout;
            if (w.Clock.Tick < L.SwapReadyTick) return;
            bool wants = cmd.FireHeld || cmd.FirePressed;
            double seconds = w.Clock.Seconds;
            if (!wants || seconds < L.NextFireTime - 1e-9) return;

            Swing(w, active, l);
            double interval = 1.0 / Mathf.Max(0.01f, l.Rate);
            L.NextFireTime = (seconds - L.NextFireTime < interval ? L.NextFireTime : seconds) + interval;
        }

        /// <returns>The enemies hit by this swing.</returns>
        public static int Swing(World w, OwnedWeapon active, WeaponLevel l)
        {
            var p = w.Player;
            var data = w.Profile.Weapon(active.Id);
            Vector2 dir = SimMath.SafeNormal(p.Aim - p.Position, p.LastMoveDir);
            float damage = WeaponSystem.ShotDamage(w, l);
            int hits = 0;

            w.Crowd.Query(p.Position, l.Range + ProjectileSystem.MaxEnemyRadius(w), Candidates);
            foreach (var e in Candidates)
            {
                if (!e.Alive || !InArc(p.Position, dir, l.Range, l.ArcDeg, e.Position, e.Radius)) continue;
                if (!w.Arena.LineOfSight(p.Position, e.Position)) continue;
                Vector2 travel = SimMath.SafeNormal(e.Position - p.Position, dir);
                Combat.DamageEnemy(w, e, damage, DamageSource.Melee, travel, data.IgnoresFrontalArmor);
                if (e.Alive) EnemySystem.Knockback(w, e, travel, l.Knockback);
                hits++;
            }
            var boss = w.Boss;
            if (boss.Active && !boss.Dead && InArc(p.Position, dir, l.Range, l.ArcDeg, boss.Position, boss.Radius) && w.Arena.LineOfSight(p.Position, boss.Position))
            {
                BossSystem.Damage(w, damage, DamageSource.Melee, dir);
                hits++;
            }
            w.Emit(SimEventType.MeleeSwing, PlayerState.EntityId, p.Position, dir * l.Range, l.ArcDeg, (int)active.Id);
            return hits;
        }

        /// <summary>True when a circle overlaps the attack sector (reach measured to the circle's edge).</summary>
        public static bool InArc(Vector2 origin, Vector2 dir, float reach, float arcDeg, Vector2 center, float radius)
        {
            Vector2 to = center - origin;
            float dist = to.magnitude;
            if (dist - radius > reach) return false;
            if (dist <= radius) return true;
            float allowance = Mathf.Asin(Mathf.Clamp01(radius / dist)) * Mathf.Rad2Deg;
            return Vector2.Angle(dir, to) <= arcDeg * 0.5f + allowance;
        }
    }
}
