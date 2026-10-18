using System.Collections.Generic;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// Moves projectiles with swept tests: walls stop them; player shots hit enemies (piercing up to
    /// TargetsLeft, e.g. "the first and one behind"), enemy shots hit the player. Enemy and boss bodies are
    /// upright capsules (feet circle raised by HitHeight), so a shot drawn across the chest or head connects.
    /// </summary>
    public static class ProjectileSystem
    {
        static readonly List<EnemyState> Candidates = new(32);
        static readonly List<(float t, EnemyState e)> Hits = new(8);
        static float maxEnemyRadius, maxEnemyHitHeight;

        public static float MaxEnemyRadius(World w)
        {
            float r = 0;
            foreach (var e in w.Enemies) if (e.Radius > r) r = e.Radius;
            return r;
        }

        /// <summary>Turns a homing shot toward the player, never faster than its rate; the speed stays the same.</summary>
        static void Home(World w, ProjectileState p, float dt)
        {
            Vector2 want = w.Player.Position - p.Position;
            if (want.sqrMagnitude < 1e-6f || p.Velocity.sqrMagnitude < 1e-6f) return;
            float max = p.Homing * dt;
            p.Velocity = SimMath.Rotate(p.Velocity, Mathf.Clamp(Vector2.SignedAngle(p.Velocity, want), -max, max));
        }

        public static void Step(World w)
        {
            float dt = Core.SimClock.Dt;
            maxEnemyRadius = MaxEnemyRadius(w);
            maxEnemyHitHeight = 0f;
            foreach (var e in w.Enemies) maxEnemyHitHeight = Mathf.Max(maxEnemyHitHeight, w.Profile.Enemy(e.Type).HitHeight);
            foreach (var p in w.Projectiles)
            {
                if (!p.Alive) continue;
                p.PrevPosition = p.Position;
                if (p.Homing > 0f && !w.Player.Dead) Home(w, p, dt);
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
            // Bodies stand up the screen from their feet (HitHeight), so look that much further for candidates.
            float reach = Vector2.Distance(from, to) * 0.5f + p.Radius + maxEnemyRadius + maxEnemyHitHeight;
            w.Crowd.Query(mid, reach, Candidates);
            Hits.Clear();
            foreach (var e in Candidates)
            {
                if (!e.Alive || p.HitIds.Contains(e.Id)) continue;
                float t = SimMath.SegmentCapsuleHit(from, to, e.Position, w.Profile.Enemy(e.Type).HitHeight, e.Radius + p.Radius);
                if (t >= 0 && t <= wallT) Hits.Add((t, e));
            }
            Hits.Sort((a, b) => a.t != b.t ? a.t.CompareTo(b.t) : a.e.Id.CompareTo(b.e.Id));

            var boss = w.Boss;
            if (BossSystem.Targetable(boss) && !p.HitIds.Contains(BossState.EntityId))
            {
                float tb = SimMath.SegmentCapsuleHit(from, to, boss.Position, BossSystem.Data(w).HitHeight, boss.Radius + p.Radius);
                if (tb >= 0 && tb <= wallT)
                {
                    p.HitIds.Add(BossState.EntityId);
                    Vector2 hitPos = Vector2.Lerp(from, to, tb);
                    BossSystem.Damage(w, p.Damage * Falloff(w, p, hitPos), p.Owner == ProjectileOwner.Companion ? DamageSource.Companion : DamageSource.Weapon, p.Velocity.normalized);
                    w.Emit(SimEventType.ProjectileImpact, p.Id, hitPos, p.Velocity.normalized, 1);
                    p.Damage *= w.Profile.Combat.PierceDamageRetained;
                    if (--p.TargetsLeft <= 0) { p.Position = hitPos; p.Alive = false; return; }
                }
            }

            foreach (var (t, e) in Hits)
            {
                Vector2 hitPos = Vector2.Lerp(from, to, t);
                p.HitIds.Add(e.Id);
                var source = p.Owner == ProjectileOwner.Companion ? DamageSource.Companion : DamageSource.Weapon;
                Combat.DamageEnemy(w, e, p.Damage * Falloff(w, p, hitPos), source, p.Velocity.normalized, p.IgnoresArmor);
                w.Emit(SimEventType.ProjectileImpact, p.Id, hitPos, p.Velocity.normalized, 1);
                p.Damage *= w.Profile.Combat.PierceDamageRetained; // each later target takes 75% of what remains (v0.19 §6)
                p.TargetsLeft--;
                if (p.TargetsLeft <= 0)
                {
                    p.Position = hitPos;
                    p.Alive = false;
                    return;
                }
            }
        }

        /// <summary>v0.19 §6: full damage up to FalloffStartFraction of the range, then linear down to FalloffAtMaxRange.</summary>
        public static float Falloff(World w, ProjectileState p, Vector2 hitPos)
        {
            float t = Mathf.Clamp01(Vector2.Distance(p.Origin, hitPos) / Mathf.Max(0.01f, p.Range));
            float start = w.Profile.Combat.FalloffStartFraction;
            if (t <= start) return 1f;
            return Mathf.Lerp(1f, p.FalloffAtMaxRange, (t - start) / Mathf.Max(0.01f, 1f - start));
        }

        static void Kill(World w, ProjectileState p, bool impact = true)
        {
            p.Alive = false;
            if (impact) w.Emit(SimEventType.ProjectileImpact, p.Id, p.Position, p.Velocity.normalized, 0);
        }
    }
}
