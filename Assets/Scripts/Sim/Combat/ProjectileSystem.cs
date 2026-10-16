using System.Collections.Generic;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// Moves projectiles with swept tests: walls stop them; player shots hit enemies (piercing up to
    /// TargetsLeft, e.g. "the first and one behind"), enemy shots hit the player.
    /// </summary>
    public static class ProjectileSystem
    {
        static readonly List<EnemyState> Candidates = new(32);
        static readonly List<(float t, EnemyState e)> Hits = new(8);
        static float maxEnemyRadius;

        public static float MaxEnemyRadius(World w)
        {
            float r = 0;
            foreach (var e in w.Enemies) if (e.Radius > r) r = e.Radius;
            return r;
        }

        public static void Step(World w)
        {
            float dt = Core.SimClock.Dt;
            maxEnemyRadius = MaxEnemyRadius(w);
            foreach (var p in w.Projectiles)
            {
                if (!p.Alive) continue;
                p.PrevPosition = p.Position;
                Vector2 from = p.Position;
                Vector2 to = from + p.Velocity * dt;

                // Clip the step to the remaining range.
                float travelled = Vector2.Distance(p.Origin, from);
                float stepLen = Vector2.Distance(from, to);
                bool rangeEnds = travelled + stepLen >= p.Range;
                if (rangeEnds && stepLen > 1e-6f) to = from + (to - from) * Mathf.Clamp01((p.Range - travelled) / stepLen);

                float wallT = w.Arena.SegmentHit(from, to, out float tw) ? tw : 2f;

                if (p.Owner == ProjectileOwner.Enemy) StepEnemyShot(w, p, from, to, wallT);
                else StepFriendlyShot(w, p, from, to, wallT);

                if (!p.Alive) continue;
                if (wallT <= 1f)
                {
                    p.Position = Vector2.Lerp(from, to, wallT);
                    Kill(w, p);
                }
                else
                {
                    p.Position = to;
                    if (rangeEnds) Kill(w, p, impact: false);
                }
            }
            w.Projectiles.RemoveAll(p => !p.Alive);
        }

        static void StepEnemyShot(World w, ProjectileState p, Vector2 from, Vector2 to, float wallT)
        {
            var pl = w.Player;
            float tPlayer = pl.Dead ? -1 : SimMath.SegmentCircleHit(from, to, pl.Position, w.Profile.Player.BodyRadius + p.Radius);
            float tCompanion = CompanionSystem.Active(w)
                ? SimMath.SegmentCircleHit(from, to, w.Companion.Position, w.Profile.Companion.BodyRadius + p.Radius) : -1;
            bool hitsPlayer = tPlayer >= 0 && tPlayer <= wallT;
            bool hitsCompanion = tCompanion >= 0 && tCompanion <= wallT;
            if (!hitsPlayer && !hitsCompanion) return;
            if (hitsPlayer && (!hitsCompanion || tPlayer <= tCompanion))
            {
                p.Position = Vector2.Lerp(from, to, tPlayer);
                PlayerSystem.Damage(w, p.Damage, p.Velocity.normalized);
            }
            else
            {
                p.Position = Vector2.Lerp(from, to, tCompanion);
                CompanionSystem.Damage(w, p.Damage, p.Velocity.normalized);
            }
            Kill(w, p);
        }
        static void StepFriendlyShot(World w, ProjectileState p, Vector2 from, Vector2 to, float wallT)
        {
            Vector2 mid = (from + to) * 0.5f;
            float reach = Vector2.Distance(from, to) * 0.5f + p.Radius + maxEnemyRadius;
            w.Crowd.Query(mid, reach, Candidates);
            Hits.Clear();
            foreach (var e in Candidates)
            {
                if (!e.Alive || p.HitIds.Contains(e.Id)) continue;
                float t = SimMath.SegmentCircleHit(from, to, e.Position, e.Radius + p.Radius);
                if (t >= 0 && t <= wallT) Hits.Add((t, e));
            }
            Hits.Sort((a, b) => a.t != b.t ? a.t.CompareTo(b.t) : a.e.Id.CompareTo(b.e.Id));

            var boss = w.Boss;
            if (boss.Active && !boss.Dead && !p.HitIds.Contains(BossState.EntityId))
            {
                float tb = SimMath.SegmentCircleHit(from, to, boss.Position, boss.Radius + p.Radius);
                if (tb >= 0 && tb <= wallT)
                {
                    p.HitIds.Add(BossState.EntityId);
                    Vector2 hitPos = Vector2.Lerp(from, to, tb);
                    float falloff = Mathf.Lerp(1f, p.FalloffAtMaxRange, Mathf.Clamp01(Vector2.Distance(p.Origin, hitPos) / Mathf.Max(0.01f, p.Range)));
                    BossSystem.Damage(w, p.Damage * falloff, p.Owner == ProjectileOwner.Companion ? DamageSource.Companion : DamageSource.Weapon, p.Velocity.normalized);
                    w.Emit(SimEventType.ProjectileImpact, p.Id, hitPos, p.Velocity.normalized, 1);
                    if (--p.TargetsLeft <= 0) { p.Position = hitPos; p.Alive = false; return; }
                }
            }

            foreach (var (t, e) in Hits)
            {
                Vector2 hitPos = Vector2.Lerp(from, to, t);
                float distance = Vector2.Distance(p.Origin, hitPos);
                float falloff = Mathf.Lerp(1f, p.FalloffAtMaxRange, Mathf.Clamp01(distance / Mathf.Max(0.01f, p.Range)));
                p.HitIds.Add(e.Id);
                var source = p.Owner == ProjectileOwner.Companion ? DamageSource.Companion : DamageSource.Weapon;
                Combat.DamageEnemy(w, e, p.Damage * falloff, source, p.Velocity.normalized, p.IgnoresArmor);
                w.Emit(SimEventType.ProjectileImpact, p.Id, hitPos, p.Velocity.normalized, 1);
                p.TargetsLeft--;
                if (p.TargetsLeft <= 0)
                {
                    p.Position = hitPos;
                    p.Alive = false;
                    return;
                }
            }
        }

        static void Kill(World w, ProjectileState p, bool impact = true)
        {
            p.Alive = false;
            if (impact) w.Emit(SimEventType.ProjectileImpact, p.Id, p.Position, p.Velocity.normalized, 0);
        }
    }
}
