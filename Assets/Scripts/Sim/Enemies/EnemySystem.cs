using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>Shape the presentation draws for a telegraph; carried in <see cref="SimEvent.Code"/>.</summary>
    public enum TelegraphShape { MeleeArc, Line, Lane, Circle, Countdown }

    /// <summary>
    /// Enemy behaviour (GDD §5): every damaging attack is preceded by a telegraph (windup), ranged attacks
    /// only start inside the player's view, and movement uses the shared flow field plus local separation.
    /// </summary>
    public static partial class EnemySystem
    {
        static readonly List<EnemyState> Neighbours = new(32);

        public static EnemyState Spawn(World w, EnemyId type, Vector2 position, bool warning = true, int encounterGroup = 0)
        {
            var d = w.Profile.Enemy(type);
            float health = WaveSystem.SpawnHealth(w, type);
            var e = new EnemyState
            {
                Id = w.AllocateId(), Type = type, Position = position, PrevPosition = position,
                Radius = d.BodyRadius, Health = health, MaxHealth = health,
                SpawnTick = w.Clock.Tick, EncounterGroup = encounterGroup, Facing = Vector2.down,
            };
            float warn = warning ? w.Profile.Spawning.SpawnWarning : 0f;
            if (warn > 0) { e.Ai = EnemyAi.Spawning; e.AiUntilTick = w.Clock.Tick + w.Ticks(warn); }
            else e.Ai = EnemyAi.Chase;
            if (d.HasChild) e.NextProduceTick = w.Clock.Tick + w.Ticks(warn + d.ProduceDelay);
            w.Enemies.Add(e);
            w.Emit(SimEventType.EnemySpawned, e.Id, position, default, warn, (int)type);
            return e;
        }

        public static void Step(World w)
        {
            w.PlayerField.SetTarget(w.Player.Position);
            bool companion = CompanionSystem.Active(w);
            if (companion) w.CompanionField.SetTarget(w.Companion.Position);
            companionAttackers = 0;
            foreach (var o in w.Enemies)
            {
                if (!companion && o.Target == TargetKind.Companion) o.Target = TargetKind.Player;
                if (o.IsActive && o.Target == TargetKind.Companion) companionAttackers++;
            }
            int count = w.Enemies.Count; // children spawned this tick start next tick
            for (int i = 0; i < count; i++)
            {
                var e = w.Enemies[i];
                if (!e.Alive) continue;
                e.PrevPosition = e.Position;
                if (e.Id == w.TutorialGrenadeEnemyId && (w.Tutorial == TutorialStage.Grenade || w.Tutorial == TutorialStage.GrenadeThrown))
                {
                    e.Velocity = Vector2.zero;
                    continue;
                }

                if (e.Ai == EnemyAi.Spawning)
                {
                    if (w.Clock.Tick >= e.AiUntilTick) e.Ai = EnemyAi.Chase;
                    continue;
                }
                if (w.Clock.Tick < e.StunnedUntilTick) { e.Velocity = Vector2.zero; continue; }

                var d = w.Profile.Enemy(e.Type);
                ConsiderCompanion(w, e, d);
                if (w.Player.Dead) { e.Velocity = Vector2.zero; }
                else
                {
                    switch (d.Role)
                    {
                        case EnemyRole.Ranged:
                            if (d.ExplosionRadius > 0 && d.FuseTime > 0) StepLobber(w, e, d);
                            else StepRanged(w, e, d);
                            break;
                        case EnemyRole.Charger: StepCharger(w, e, d); break;
                        case EnemyRole.Explosive: StepExplosive(w, e, d); break;
                        case EnemyRole.Support: StepSupport(w, e, d); break;
                        case EnemyRole.Producer: StepProducer(w, e, d); break;
                        default: StepMelee(w, e, d); break;
                    }
                }
                ApplyKnockback(w, e);
            }
        }

        // ---------------------------------------------------------------- targets

        static int companionAttackers;

        static bool TargetsCompanion(World w, EnemyState e) => e.Target == TargetKind.Companion && CompanionSystem.Active(w);

        public static Vector2 TargetPosition(World w, EnemyState e) => TargetsCompanion(w, e) ? w.Companion.Position : w.Player.Position;

        static float TargetRadius(World w, EnemyState e) =>
            TargetsCompanion(w, e) ? w.Profile.Companion.BodyRadius : w.Profile.Player.BodyRadius;

        /// <summary>
        /// Opportunistic companion attacks (§4, D25): an enemy closer to the companion than to the player and within
        /// range may switch, while fewer than the cap already target the companion. Only switches while chasing.
        /// </summary>
        static void ConsiderCompanion(World w, EnemyState e, EnemyData d)
        {
            if (e.Target != TargetKind.Player || e.Ai != EnemyAi.Chase || !d.CanTargetCompanion || !CompanionSystem.Active(w)) return;
            if (companionAttackers >= w.Profile.Companion.AttackerCap) return;
            float toCompanion = Vector2.Distance(e.Position, w.Companion.Position);
            if (toCompanion > w.Profile.Companion.OpportunisticRange) return;
            if (toCompanion >= Vector2.Distance(e.Position, w.Player.Position)) return;
            e.Target = TargetKind.Companion;
            companionAttackers++;
        }

        static float Gap(World w, EnemyState e) =>
            Vector2.Distance(e.Position, TargetPosition(w, e)) - e.Radius - TargetRadius(w, e);

        // ---------------------------------------------------------------- melee

        static void StepMelee(World w, EnemyState e, EnemyData d)
        {
            long now = w.Clock.Tick;
            switch (e.Ai)
            {
                case EnemyAi.Chase:
                    if (d.Damage > 0 && Gap(w, e) <= d.AttackRange && now >= e.NextAttackTick)
                    {
                        BeginWindup(w, e, d, TelegraphShape.MeleeArc, d.AttackRange + e.Radius);
                        break;
                    }
                    MoveToward(w, e, d, TargetPosition(w, e));
                    break;

                case EnemyAi.Windup:
                    e.Velocity = Vector2.zero;
                    TurnToward(w, e, d, e.AttackDir);
                    if (now >= e.AiUntilTick) MeleeStrike(w, e, d);
                    break;

                case EnemyAi.Recover:
                    e.Velocity = Vector2.zero;
                    if (now >= e.AiUntilTick) e.Ai = EnemyAi.Chase;
                    break;

                default:
                    e.Ai = EnemyAi.Chase;
                    break;
            }
        }

        static void MeleeStrike(World w, EnemyState e, EnemyData d)
        {
            long now = w.Clock.Tick;
            Vector2 target = TargetPosition(w, e);
            Vector2 to = target - e.Position;
            var rules = w.Profile.Combat;
            float reach = d.AttackRange + e.Radius + TargetRadius(w, e) + rules.EnemyMeleeHitTolerance;
            bool hit = to.magnitude <= reach && Vector2.Angle(e.AttackDir, to) <= rules.EnemyMeleeHitHalfAngle;
            w.Emit(SimEventType.EnemyAttack, e.Id, e.Position, e.AttackDir, hit ? 1 : 0, (int)e.Type);
            if (hit) DamageTarget(w, e, d.Damage);
            e.Ai = EnemyAi.Recover;
            e.AiUntilTick = now + w.Ticks(d.AttackRecovery);
            e.NextAttackTick = e.AttackStartTick + w.Ticks(d.AttackInterval);
        }

        // ---------------------------------------------------------------- ranged

        static void StepRanged(World w, EnemyState e, EnemyData d)
        {
            long now = w.Clock.Tick;
            Vector2 target = TargetPosition(w, e);
            switch (e.Ai)
            {
                case EnemyAi.Chase:
                {
                    float dist = Vector2.Distance(e.Position, target);
                    bool los = w.Arena.LineOfSight(e.Position, target);
                    bool inRange = dist <= d.AttackRange;
                    if (inRange && los && InPlayerView(w, e.Position) && now >= e.NextAttackTick)
                    {
                        BeginWindup(w, e, d, TelegraphShape.Line, d.ProjectileRange);
                        break;
                    }
                    // Hold a firing position a little inside range; otherwise close in.
                    if (!los || dist > d.AttackRange * w.Profile.Combat.RangedHoldFraction) MoveToward(w, e, d, target);
                    else { e.Velocity = Vector2.zero; e.Facing = SimMath.SafeNormal(target - e.Position, e.Facing); }
                    break;
                }

                case EnemyAi.Windup:
                    e.Velocity = Vector2.zero;
                    if (now >= e.AiUntilTick)
                    {
                        e.BurstLeft = Mathf.Max(1, d.BurstCount);
                        e.Ai = EnemyAi.Attack;
                        e.NextBurstTick = now;
                        goto case EnemyAi.Attack;
                    }
                    break;

                case EnemyAi.Attack:
                    e.Velocity = Vector2.zero;
                    if (now >= e.NextBurstTick && e.BurstLeft > 0)
                    {
                        FireEnemyProjectile(w, e, d, e.AttackDir);
                        e.BurstLeft--;
                        e.NextBurstTick = now + w.Ticks(d.BurstInterval);
                    }
                    if (e.BurstLeft <= 0)
                    {
                        e.Ai = EnemyAi.Recover;
                        e.AiUntilTick = now + w.Ticks(d.AttackRecovery);
                        e.NextAttackTick = e.AttackStartTick + w.Ticks(d.AttackInterval);
                    }
                    break;

                case EnemyAi.Recover:
                    e.Velocity = Vector2.zero;
                    if (now >= e.AiUntilTick) e.Ai = EnemyAi.Chase;
                    break;

                default:
                    e.Ai = EnemyAi.Chase;
                    break;
            }
        }

        public static void FireEnemyProjectile(World w, EnemyState e, EnemyData d, Vector2 dir)
        {
            var p = new ProjectileState
            {
                Id = w.AllocateId(), Owner = ProjectileOwner.Enemy, SourceId = e.Id, EnemyType = e.Type,
                Position = e.Position, PrevPosition = e.Position, Origin = e.Position,
                Velocity = dir * d.ProjectileSpeed, Damage = d.Damage, Range = d.ProjectileRange,
                Radius = w.Profile.Combat.EnemyProjectileRadius, TargetsLeft = 1,
            };
            w.Projectiles.Add(p);
            w.Emit(SimEventType.EnemyAttack, e.Id, e.Position, dir, 1, (int)e.Type);
        }

        /// <summary>"Inside the view" = inside the camera rectangle around the player, minus a margin (§6).</summary>
        public static bool InPlayerView(World w, Vector2 p)
        {
            var cam = w.Profile.Camera;
            Vector2 d = p - w.Player.Position;
            float halfH = cam.OrthographicSize - cam.ViewMargin;
            float halfW = cam.OrthographicSize * cam.ViewAspect - cam.ViewMargin;
            return Mathf.Abs(d.x) <= halfW && Mathf.Abs(d.y) <= halfH;
        }

        // ---------------------------------------------------------------- shared

        static void BeginWindup(World w, EnemyState e, EnemyData d, TelegraphShape shape, float length)
        {
            e.Ai = EnemyAi.Windup;
            e.AttackStartTick = w.Clock.Tick;
            e.AiUntilTick = w.Clock.Tick + w.Ticks(d.AttackWindup);
            e.AttackPoint = TargetPosition(w, e);
            e.AttackDir = SimMath.SafeNormal(e.AttackPoint - e.Position, e.Facing);
            e.Velocity = Vector2.zero;
            w.Emit(SimEventType.EnemyTelegraph, e.Id, e.Position, e.AttackDir * length, d.AttackWindup, (int)shape);
        }

        static void MoveToward(World w, EnemyState e, EnemyData d, Vector2 target)
        {
            Vector2 dir = (TargetsCompanion(w, e) ? w.CompanionField : w.PlayerField).Direction(e.Position, target);
            float speed = d.MoveSpeed * (w.Clock.Tick < e.SpeedBoostUntilTick ? 1f + w.Profile.Enemy(EnemyId.A5_HowlingWolf).AuraSpeedBonus : 1f);
            e.Velocity = dir * speed;
            if (dir.sqrMagnitude > 1e-6f) TurnToward(w, e, d, dir);
            e.Position = w.Arena.MoveCircle(e.Position, e.Velocity * Core.SimClock.Dt, e.Radius);
        }

        /// <summary>Turns the facing toward a direction at the enemy's turn rate (slow turners can be flanked).</summary>
        static void TurnToward(World w, EnemyState e, EnemyData d, Vector2 dir)
        {
            if (dir.sqrMagnitude < 1e-8f) return;
            float maxDeg = d.TurnRateDeg * Core.SimClock.Dt;
            float current = Mathf.Atan2(e.Facing.y, e.Facing.x) * Mathf.Rad2Deg;
            float target = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float next = Mathf.MoveTowardsAngle(current, target, maxDeg) * Mathf.Deg2Rad;
            e.Facing = new Vector2(Mathf.Cos(next), Mathf.Sin(next));
        }

        static void ApplyKnockback(World w, EnemyState e)
        {
            if (w.Clock.Tick >= e.KnockbackUntilTick) return;
            e.Position = w.Arena.MoveCircle(e.Position, e.KnockbackVelocity * Core.SimClock.Dt, e.Radius);
        }

        static void DamageTarget(World w, EnemyState e, float amount)
        {
            Vector2 dir = SimMath.SafeNormal(TargetPosition(w, e) - e.Position, e.Facing);
            if (TargetsCompanion(w, e)) CompanionSystem.Damage(w, amount, dir);
            else PlayerSystem.Damage(w, amount, dir);
        }

        /// <summary>Pushes the enemy away from a hit (visual reaction with a small, data-driven displacement).</summary>
        public static void Knockback(World w, EnemyState e, Vector2 dir, float distance)
        {
            var d = w.Profile.Enemy(e.Type);
            float dist = distance * (1f - Mathf.Clamp01(d.KnockbackResistance));
            float duration = Mathf.Max(Core.SimClock.Dt, w.Profile.Combat.KnockbackDuration);
            if (dist <= 0) return;
            e.KnockbackVelocity = SimMath.SafeNormal(dir, Vector2.zero) * (dist / duration);
            e.KnockbackUntilTick = w.Clock.Tick + System.Math.Max(1, w.Ticks(duration));
        }

        // ---------------------------------------------------------------- separation

        /// <summary>Keeps enemies from stacking and resolves player–enemy overlap (player takes CrowdPushShare).</summary>
        public static void Separate(World w)
        {
            w.Crowd.Rebuild(w.Enemies);
            float maxR = ProjectileSystem.MaxEnemyRadius(w);
            foreach (var e in w.Enemies)
            {
                if (!e.IsActive) continue;
                w.Crowd.Query(e.Position, e.Radius + maxR, Neighbours);
                foreach (var o in Neighbours)
                {
                    if (o.Id <= e.Id) continue; // each pair once
                    Vector2 delta = o.Position - e.Position;
                    float min = e.Radius + o.Radius;
                    float d2 = delta.sqrMagnitude;
                    if (d2 >= min * min) continue;
                    float d = Mathf.Sqrt(d2);
                    Vector2 n = d > 1e-5f ? delta / d : new Vector2(1, 0);
                    Vector2 push = n * ((min - d) * 0.5f);
                    e.Position = w.Arena.Resolve(e.Position - push, e.Radius);
                    o.Position = w.Arena.Resolve(o.Position + push, o.Radius);
                }
            }

            var p = w.Player;
            var boss = w.Boss;
            if (boss.Active && !boss.Dead && !BossSystem.InAir(boss))
            {
                // The boss does not yield: push the player and adds out of its body.
                float br = boss.Radius + w.Profile.Player.BodyRadius;
                Vector2 dp = p.Position - boss.Position;
                if (!p.Dashing && dp.sqrMagnitude < br * br) p.Position = w.Arena.Resolve(boss.Position + SimMath.SafeNormal(dp, Vector2.down) * br, w.Profile.Player.BodyRadius);
                foreach (var e in w.Enemies)
                {
                    if (!e.IsActive) continue;
                    float er = boss.Radius + e.Radius;
                    Vector2 de = e.Position - boss.Position;
                    if (de.sqrMagnitude < er * er) e.Position = w.Arena.Resolve(boss.Position + SimMath.SafeNormal(de, Vector2.right) * er, e.Radius);
                }
            }
            if (p.Dead || p.Dashing) return;
            float pr = w.Profile.Player.BodyRadius;
            float share = Mathf.Clamp01(w.Profile.Player.CrowdPushShare);
            w.Crowd.Query(p.Position, pr + maxR, Neighbours);
            foreach (var e in Neighbours)
            {
                Vector2 delta = e.Position - p.Position;
                float min = pr + e.Radius;
                float d2 = delta.sqrMagnitude;
                if (d2 >= min * min) continue;
                float d = Mathf.Sqrt(d2);
                Vector2 n = d > 1e-5f ? delta / d : new Vector2(0, 1);
                float overlap = min - d;
                p.Position = w.Arena.Resolve(p.Position - n * overlap * share, pr);
                e.Position = w.Arena.Resolve(e.Position + n * overlap * (1f - share), e.Radius);
            }
        }
    }
}
