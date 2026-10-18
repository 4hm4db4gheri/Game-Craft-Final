using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>Role behaviours beyond plain melee/ranged (GDD §5). Every damaging action is telegraphed.</summary>
    public static partial class EnemySystem
    {
        // ---------------------------------------------------------------- charger (boar, saw, assaulter)

        /// <summary>Marks a lane, charges straight along it, then stops and can be punished.</summary>
        static void StepCharger(World w, EnemyState e, EnemyData d)
        {
            long now = w.Clock.Tick;
            switch (e.Ai)
            {
                case EnemyAi.Chase:
                {
                    Vector2 target = TargetPosition(w, e);
                    float dist = Vector2.Distance(e.Position, target);
                    if (dist <= d.AttackRange && now >= e.NextAttackTick && w.Arena.LineOfSight(e.Position, target))
                    {
                        e.ChargeLength = dist + d.ChargeOvershoot;
                        BeginWindup(w, e, d, TelegraphShape.Lane, e.ChargeLength);
                        break;
                    }
                    MoveToward(w, e, d, target);
                    break;
                }
                case EnemyAi.Windup:
                    e.Velocity = Vector2.zero;
                    TurnToward(w, e, d, e.AttackDir);
                    if (now >= e.AiUntilTick)
                    {
                        e.Ai = EnemyAi.Charge;
                        e.ChargeTravelled = 0;
                        e.ChargeHit = false;
                        w.Emit(SimEventType.EnemyAttack, e.Id, e.Position, e.AttackDir, 1, (int)e.Type);
                    }
                    break;
                case EnemyAi.Charge:
                {
                    Vector2 from = e.Position;
                    float step = d.ChargeSpeed * Core.SimClock.Dt;
                    e.Velocity = e.AttackDir * d.ChargeSpeed;
                    e.Position = w.Arena.MoveCircle(from, e.AttackDir * step, e.Radius);
                    float moved = Vector2.Distance(from, e.Position);
                    e.ChargeTravelled += moved;
                    if (!e.ChargeHit) TryChargeHit(w, e, d, from);
                    bool blocked = moved < step * 0.5f;
                    if (e.ChargeTravelled >= e.ChargeLength || blocked)
                    {
                        e.Ai = EnemyAi.Recover;
                        e.Velocity = Vector2.zero;
                        e.AiUntilTick = now + w.Ticks(d.AttackRecovery);
                        e.NextAttackTick = e.AttackStartTick + w.Ticks(d.AttackInterval);
                    }
                    break;
                }
                case EnemyAi.Recover:
                    e.Velocity = Vector2.zero;
                    if (now >= e.AiUntilTick) e.Ai = EnemyAi.Chase;
                    break;
                default:
                    e.Ai = EnemyAi.Chase;
                    break;
            }
        }

        static void TryChargeHit(World w, EnemyState e, EnemyData d, Vector2 from)
        {
            if (!w.Player.Dead)
            {
                float r = e.Radius + w.Profile.Player.BodyRadius;
                if (SimMath.SegmentPointDistanceSq(from, e.Position, w.Player.Position) <= r * r)
                {
                    PlayerSystem.Damage(w, d.Damage, e.AttackDir);
                    e.ChargeHit = true; // one hit per charge
                }
            }
            if (!e.ChargeHit && CompanionSystem.Active(w))
            {
                float r = e.Radius + w.Profile.Companion.BodyRadius;
                if (SimMath.SegmentPointDistanceSq(from, e.Position, w.Companion.Position) <= r * r)
                {
                    CompanionSystem.Damage(w, d.Damage, e.AttackDir);
                    e.ChargeHit = true;
                }
            }
        }

        // ---------------------------------------------------------------- explosive (beetle, suicide robot)

        /// <summary>Approaches, stops, shows the blast area (and countdown) for the fuse, then detonates.</summary>
        static void StepExplosive(World w, EnemyState e, EnemyData d)
        {
            long now = w.Clock.Tick;
            switch (e.Ai)
            {
                case EnemyAi.Chase:
                    if (Gap(w, e) <= d.AttackRange)
                    {
                        BeginWindup(w, e, d, d.IsMachine ? TelegraphShape.Countdown : TelegraphShape.Circle, d.ExplosionRadius);
                        e.AiUntilTick = now + w.Ticks(d.FuseTime);
                        break;
                    }
                    MoveToward(w, e, d, TargetPosition(w, e));
                    break;
                case EnemyAi.Windup:
                    e.Velocity = Vector2.zero;
                    if (now >= e.AiUntilTick) Detonate(w, e, d);
                    break;
                default:
                    e.Ai = EnemyAi.Chase;
                    break;
            }
        }

        static void Detonate(World w, EnemyState e, EnemyData d)
        {
            w.Emit(SimEventType.EnemyAttack, e.Id, e.Position, Vector2.zero, 1, (int)e.Type);
            w.Emit(SimEventType.Explosion, e.Id, e.Position, default, d.ExplosionRadius, (int)HazardOwner.Enemy);
            if (!w.Player.Dead && Vector2.Distance(w.Player.Position, e.Position) <= d.ExplosionRadius + w.Profile.Player.BodyRadius)
                PlayerSystem.Damage(w, d.Damage, SimMath.SafeNormal(w.Player.Position - e.Position, Vector2.up));
            if (CompanionSystem.Active(w) && Vector2.Distance(w.Companion.Position, e.Position) <= d.ExplosionRadius + w.Profile.Companion.BodyRadius)
                CompanionSystem.Damage(w, d.Damage, SimMath.SafeNormal(w.Companion.Position - e.Position, Vector2.up));
            // The blast destroys the enemy; its reward still drops so phase budgets stay exact.
            e.Health = 0;
            e.Alive = false;
            w.Emit(SimEventType.EnemyDied, e.Id, e.Position, Vector2.zero, 0, (int)e.Type);
            Rewards.OnEnemyKilled(w, e);
        }

        // ---------------------------------------------------------------- grenadier

        /// <summary>Marks the landing zone, throws, and the explosive goes off after its delay (§5 H6).</summary>
        static void StepLobber(World w, EnemyState e, EnemyData d)
        {
            long now = w.Clock.Tick;
            Vector2 target = TargetPosition(w, e);
            switch (e.Ai)
            {
                case EnemyAi.Chase:
                {
                    float dist = Vector2.Distance(e.Position, target);
                    if (dist <= d.AttackRange && InPlayerView(w, e.Position) && now >= e.NextAttackTick)
                    {
                        // The landing zone (AttackPoint) is marked from the start of the windup.
                        BeginWindup(w, e, d, TelegraphShape.Circle, d.ExplosionRadius);
                        break;
                    }
                    if (dist > d.AttackRange * w.Profile.Combat.RangedHoldFraction) MoveToward(w, e, d, target);
                    else e.Velocity = Vector2.zero;
                    break;
                }
                case EnemyAi.Windup:
                    e.Velocity = Vector2.zero;
                    if (now >= e.AiUntilTick)
                    {
                        // Thrown: the marked zone stays until the delayed explosive goes off (§5 H6).
                        float flight = Mathf.Min(d.FuseTime, Vector2.Distance(e.Position, e.AttackPoint) / Mathf.Max(0.1f, d.ProjectileSpeed));
                        HazardSystem.Create(w, HazardOwner.Enemy, e.Position, e.AttackPoint, d.ExplosionRadius, d.Damage, d.FuseTime, e.Id, e.Type, flight);
                        w.Emit(SimEventType.EnemyAttack, e.Id, e.Position, e.AttackDir, 1, (int)e.Type);
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

        // ---------------------------------------------------------------- support (howl, repair, medic)

        static void StepSupport(World w, EnemyState e, EnemyData d)
        {
            StayWithAllies(w, e, d);
            long now = w.Clock.Tick;
            if (d.HealFraction > 0) StepHealer(w, e, d, now);
            else StepHowler(w, e, d, now);
        }

        /// <summary>Howl: nearby animals move faster for a while; the bonus never stacks (§5).</summary>
        static void StepHowler(World w, EnemyState e, EnemyData d, long now)
        {
            if (e.Ai == EnemyAi.Windup)
            {
                if (now < e.AiUntilTick) return;
                foreach (var o in w.Enemies)
                {
                    if (!o.IsActive || o == e || w.Profile.Enemy(o.Type).Faction != d.Faction) continue;
                    if (Vector2.Distance(o.Position, e.Position) > d.AuraRadius) continue;
                    o.SpeedBoostUntilTick = now + w.Ticks(d.AuraDuration); // refresh, never stack
                }
                w.Emit(SimEventType.EnemyAttack, e.Id, e.Position, Vector2.zero, 0, (int)e.Type);
                e.Ai = EnemyAi.Chase;
                e.NextAttackTick = e.AttackStartTick + w.Ticks(d.AttackInterval);
                return;
            }
            if (now >= e.NextAttackTick && AnyAllyNear(w, e, d))
            {
                e.Ai = EnemyAi.Windup;
                e.AttackStartTick = now;
                e.AiUntilTick = now + w.Ticks(d.AttackWindup);
                w.Emit(SimEventType.EnemyTelegraph, e.Id, e.Position, Vector2.right * d.AuraRadius, d.AttackWindup, (int)TelegraphShape.Circle);
            }
        }

        /// <summary>
        /// Repairs/heals the most damaged nearby ally by a share of its maximum health (v0.19 §9). Each target has
        /// one healing budget per encounter shared by every healer, so two healers cannot reset it; the boss uses
        /// its own cap (BossSystem.Heal).
        /// </summary>
        static void StepHealer(World w, EnemyState e, EnemyData d, long now)
        {
            if (now < e.NextAttackTick) return;
            var boss = w.Boss;
            if (boss.Active && !boss.Dead && boss.Health < boss.MaxHealth && Vector2.Distance(boss.Position, e.Position) <= d.AuraRadius + boss.Radius)
            {
                float healed = BossSystem.Heal(w, d.HealFraction * boss.MaxHealth);
                if (healed > 0)
                {
                    e.HealGiven += healed;
                    e.NextAttackTick = now + w.Ticks(d.HealInterval);
                    w.Emit(SimEventType.EnemyHealed, BossState.EntityId, boss.Position, e.Position - boss.Position, healed, e.Id);
                    return;
                }
            }
            EnemyState best = null;
            float bestAmount = 0;
            foreach (var o in w.Enemies)
            {
                if (!o.IsActive || o == e || Vector2.Distance(o.Position, e.Position) > d.AuraRadius) continue;
                float amount = Mathf.Min(d.HealFraction * o.MaxHealth, o.MaxHealth - o.Health, d.HealBudgetFraction * o.MaxHealth - o.HealReceived);
                if (amount > bestAmount) { bestAmount = amount; best = o; }
            }
            if (best == null) return;
            best.Health += bestAmount;
            best.HealReceived += bestAmount;
            e.HealGiven += bestAmount;
            e.NextAttackTick = now + w.Ticks(d.HealInterval);
            w.Emit(SimEventType.EnemyHealed, best.Id, best.Position, e.Position - best.Position, bestAmount, e.Id);
        }

        static bool AnyAllyNear(World w, EnemyState e, EnemyData d)
        {
            foreach (var o in w.Enemies)
                if (o.IsActive && o != e && Vector2.Distance(o.Position, e.Position) <= d.AuraRadius) return true;
            return false;
        }

        /// <summary>Supports and non-fighting producers stay out of reach behind their allies.</summary>
        static void KeepAway(World w, EnemyState e, EnemyData d)
        {
            Vector2 target = TargetPosition(w, e);
            float dist = Vector2.Distance(e.Position, target);
            if (dist < d.KeepAwayDistance)
            {
                Vector2 away = SimMath.SafeNormal(e.Position - target, Vector2.up);
                e.Velocity = away * d.MoveSpeed;
                TurnToward(w, e, d, -away);
                e.Position = w.Arena.MoveCircle(e.Position, e.Velocity * Core.SimClock.Dt, e.Radius);
            }
            else if (dist > d.KeepAwayDistance * d.KeepAwayMaxFactor) MoveToward(w, e, d, target);
            else e.Velocity = Vector2.zero;
        }

        /// <summary>Supports stay close to their allies (inside their aura) and back off when the player comes close.</summary>
        static void StayWithAllies(World w, EnemyState e, EnemyData d)
        {
            Vector2 threat = TargetPosition(w, e);
            if (Vector2.Distance(e.Position, threat) < d.KeepAwayDistance) { KeepAway(w, e, d); return; }
            EnemyState ally = null;
            float best = float.MaxValue;
            foreach (var o in w.Enemies)
            {
                if (!o.IsActive || o == e || w.Profile.Enemy(o.Type).Role == EnemyRole.Support) continue;
                float dd = (o.Position - e.Position).sqrMagnitude;
                if (dd < best) { best = dd; ally = o; }
            }
            if (ally == null) { KeepAway(w, e, d); return; }
            if (Mathf.Sqrt(best) > d.AuraRadius * d.AuraStayFraction) MoveToward(w, e, d, ally.Position);
            else e.Velocity = Vector2.zero;
        }

        // ---------------------------------------------------------------- producers (spider, carrier, engineer)

        /// <summary>
        /// Produces up to ProduceMax children after delays; children use the active cap; each child is assigned
        /// its share of the producer's fixed reward pool at birth (§6).
        /// </summary>
        static void StepProducer(World w, EnemyState e, EnemyData d)
        {
            if (d.Damage > 0) StepMelee(w, e, d);
            else KeepAway(w, e, d);

            long now = w.Clock.Tick;
            if (!d.HasChild || e.ChildrenProduced >= d.ProduceMax || now < e.NextProduceTick) return;
            if (w.Waves.Kind == EncounterKind.Wave && WaveSystem.AliveCount(w) >= w.Waves.ActiveCap) return;
            // The boss cap holds for children too (it includes the boss itself).
            if (w.Waves.Kind == EncounterKind.Boss && WaveSystem.AliveCount(w) + 1 >= BossSystem.Data(w).ActiveCap) return;

            var childData = w.Profile.Enemy(d.Child);
            Vector2 offset = SimMath.Rotate(e.Facing, w.Profile.Spawning.ChildSpawnBackAngle + w.Profile.Spawning.ChildSpawnAngleSpread * w.Rng.Value) * (e.Radius + childData.BodyRadius + w.Profile.Spawning.ChildSpawnGap);
            Vector2 at = w.Arena.NearestFree(e.Position + offset, childData.BodyRadius);
            var child = Spawn(w, d.Child, at, warning: true, encounterGroup: e.EncounterGroup);
            child.ParentId = e.Id;
            child.ShareXp = d.ChildXpShare;
            child.ShareCoins = d.ChildCoinShare;
            e.XpAllocated += d.ChildXpShare;
            e.CoinsAllocated += d.ChildCoinShare;
            e.ChildrenProduced++;
            e.NextProduceTick = now + w.Ticks(d.ProduceDelay);
        }
    }
}
