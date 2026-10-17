using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    public enum BossAi { Rest, ChargeWindup, Charging, ChargeRecover, StrikeWindup, StrikeRecover, BurstWarning, Bursting, BurstRecover, BlastWarning, BlastRecover }

    public sealed class BossState
    {
        public const int EntityId = -2;
        public const int EncounterGroup = 1;

        public bool Active;
        public bool Dead;
        public BossId Id;
        public Vector2 Position;
        public Vector2 PrevPosition;
        public Vector2 Facing = Vector2.down;
        public float Radius;
        public float Health;
        public float MaxHealth;

        public BossAi Ai;
        public long AiUntilTick;
        public int PatternIndex;
        /// <summary>When the current/last attack began; the next starts AttackInterval later at the earliest (v0.19 §12).</summary>
        public long AttackStartTick;
        public float ChargeLength;
        public Vector2 AttackDir;
        public Vector2 AttackPoint;
        public float ChargeTravelled;
        public bool ChargeHit;
        public long NextShotTick;
        public long BurstStartTick;

        public long StunnedUntilTick;
        public long EmpResistantUntilTick;
        public float HealedTotal;

        /// <summary>Threshold groups already queued: never again, even after healing or loading (§7).</summary>
        public List<int> FiredThresholds = new();
        public List<EnemyId> AddQueue = new();
        public long NextAddActiveTick;

        /// <summary>Encounter reward pool already paid out through adds (§9).</summary>
        public int PoolXpPaid;
        public int PoolCoinsPaid;
    }

    /// <summary>
    /// Boss encounters (GDD §7): opening adds, two threshold groups queued once, cap including the boss,
    /// readable telegraphed patterns that mostly target the player, shorter rests (never removed warnings)
    /// at low health, healing capped at a share of max health, EMP interrupt + resistance, and a fixed
    /// reward pool so a quick kill never earns less than a slow one.
    /// </summary>
    public static class BossSystem
    {
        public static BossData Data(World w) => w.Profile.Boss(w.Phase);

        public static void StartEncounter(World w)
        {
            var d = Data(w);
            WaveSystem.CaptureHealth(w); // adds use the health captured now (§13)
            var b = w.Boss;
            Vector2 spawn = w.Layout.SpawnZones.Find(z => z.Side == SpawnSide.North)?.Center ?? Vector2.up * 8;
            b.Active = true;
            b.Dead = false;
            b.Id = d.Id;
            b.Position = b.PrevPosition = w.Arena.NearestFree(spawn, d.BodyRadius);
            b.Radius = d.BodyRadius;
            b.Health = b.MaxHealth = d.Health;
            b.Facing = Vector2.down;
            b.Ai = BossAi.Rest;
            b.AiUntilTick = w.Clock.Tick + w.Ticks(d.FirstAttackDelay);
            b.PatternIndex = 0;
            b.FiredThresholds.Clear();
            b.AddQueue.Clear();
            foreach (var c in d.OpeningAdds) for (int i = 0; i < c.Count; i++) b.AddQueue.Add(c.Id);
            b.NextAddActiveTick = w.Clock.ActiveTicks;
            b.PoolXpPaid = b.PoolCoinsPaid = 0;
            b.HealedTotal = 0;
            b.StunnedUntilTick = b.EmpResistantUntilTick = 0;

            w.Waves.Kind = EncounterKind.Boss;
            w.SafeInterval = false;
            w.Emit(SimEventType.BossStarted, BossState.EntityId, b.Position, default, 0, (int)d.Id);
            // The boss-start checkpoint includes the preparation purchases (§7).
            Checkpoints.Capture(w);
        }

        public static void Step(World w)
        {
            var b = w.Boss;
            if (!b.Active || b.Dead) return;
            var d = Data(w);
            b.PrevPosition = b.Position;
            QueueThresholds(w, b, d);
            SpawnAdds(w, b, d);
            if (w.Player.Dead) return;
            if (w.Clock.Tick < b.StunnedUntilTick) return;
            StepAi(w, b, d);
        }

        // ---------------------------------------------------------------- adds

        static void QueueThresholds(World w, BossState b, BossData d)
        {
            float fraction = b.Health / b.MaxHealth;
            for (int i = 0; i < d.Thresholds.Count; i++)
            {
                if (b.FiredThresholds.Contains(i) || fraction > d.Thresholds[i].HealthFraction) continue;
                b.FiredThresholds.Add(i);
                foreach (var c in d.Thresholds[i].Adds) for (int k = 0; k < c.Count; k++) b.AddQueue.Add(c.Id);
            }
        }

        static void SpawnAdds(World w, BossState b, BossData d)
        {
            if (b.AddQueue.Count == 0 || w.Clock.ActiveTicks < b.NextAddActiveTick) return;
            if (WaveSystem.AliveCount(w) + 1 >= d.ActiveCap) return; // the cap includes the boss
            int support = 0;
            foreach (var e in w.Enemies) if (e.Alive && w.Profile.Enemy(e.Type).CountsAsSupport) support++;
            int index = b.AddQueue.FindIndex(id => !w.Profile.Enemy(id).CountsAsSupport || support < w.Profile.Spawning.SupportCap);
            if (index < 0) return;
            var type = b.AddQueue[index];
            if (!WaveSystem.FindSpawnPoint(w, w.Profile.Enemy(type).BodyRadius, out var pos))
            {
                b.NextAddActiveTick = w.Clock.ActiveTicks + w.Ticks(w.Profile.Spawning.NoSafePointRetryDelay);
                return;
            }
            EnemySystem.Spawn(w, type, pos, true, BossState.EncounterGroup);
            b.AddQueue.RemoveAt(index);
            b.NextAddActiveTick = w.Clock.ActiveTicks + System.Math.Max(1, w.Ticks(d.AddSpawnGap));
        }

        // ---------------------------------------------------------------- patterns

        static bool LowHealth(BossState b, BossData d) => b.Health / b.MaxHealth <= d.LowHealthFraction;

        static void StepAi(World w, BossState b, BossData d)
        {
            long now = w.Clock.Tick;
            Vector2 player = w.Player.Position;
            switch (b.Ai)
            {
                case BossAi.Rest:
                    Move(w, b, d, player);
                    if (now >= b.AiUntilTick) ChooseAttack(w, b, d);
                    break;

                case BossAi.ChargeWindup:
                    if (now >= b.AiUntilTick) { b.Ai = BossAi.Charging; b.ChargeTravelled = 0; b.ChargeHit = false; Attack(w, b); }
                    break;
                case BossAi.Charging:
                {
                    Vector2 from = b.Position;
                    float step = d.ChargeSpeed * Core.SimClock.Dt;
                    b.Position = w.Arena.MoveCircle(from, b.AttackDir * step, b.Radius);
                    float moved = Vector2.Distance(from, b.Position);
                    b.ChargeTravelled += moved;
                    if (!b.ChargeHit) b.ChargeHit = HitAlongPath(w, b, from, d.Damage);
                    if (b.ChargeTravelled >= b.ChargeLength || moved < step * 0.5f)
                    {
                        b.Ai = BossAi.ChargeRecover; // the punish window
                        b.AiUntilTick = now + w.Ticks(d.ChargeRecovery);
                    }
                    break;
                }
                case BossAi.ChargeRecover:
                case BossAi.StrikeRecover:
                case BossAi.BurstRecover:
                case BossAi.BlastRecover:
                    if (now >= b.AiUntilTick) Rest(w, b, d);
                    break;

                case BossAi.StrikeWindup:
                    if (now >= b.AiUntilTick)
                    {
                        Attack(w, b);
                        w.Emit(SimEventType.Explosion, BossState.EntityId, b.Position, default, d.StrikeRadius, (int)HazardOwner.Enemy);
                        AreaDamage(w, b.Position, d.StrikeRadius, d.Damage);
                        b.Ai = BossAi.StrikeRecover;
                        b.AiUntilTick = now + w.Ticks(d.StrikeRecovery);
                    }
                    break;

                case BossAi.BurstWarning:
                    if (now >= b.AiUntilTick)
                    {
                        b.Ai = BossAi.Bursting;
                        b.BurstStartTick = now;
                        b.NextShotTick = now;
                        b.AiUntilTick = now + w.Ticks(d.BurstDuration);
                        Attack(w, b);
                    }
                    break;
                case BossAi.Bursting:
                    if (now >= b.NextShotTick && now < b.AiUntilTick)
                    {
                        // Sweeps across the warned cone, never outside it.
                        float t = (now - b.BurstStartTick) / (float)System.Math.Max(1, b.AiUntilTick - b.BurstStartTick);
                        Vector2 dir = SimMath.Rotate(b.AttackDir, Mathf.Lerp(-d.BurstSpread * 0.5f, d.BurstSpread * 0.5f, t));
                        FireShot(w, b, d, dir);
                        b.NextShotTick = now + System.Math.Max(1, w.Ticks(d.BurstShotInterval));
                    }
                    if (now >= b.AiUntilTick) { b.Ai = BossAi.BurstRecover; b.AiUntilTick = now + w.Ticks(d.BurstRecovery); }
                    break;

                case BossAi.BlastWarning:
                    if (now >= b.AiUntilTick) { b.Ai = BossAi.BlastRecover; b.AiUntilTick = now + w.Ticks(d.BlastRecovery); }
                    break;
            }
        }

        static void ChooseAttack(World w, BossState b, BossData d)
        {
            long now = w.Clock.Tick;
            Vector2 player = w.Player.Position;
            b.AttackPoint = player;
            b.AttackDir = SimMath.SafeNormal(player - b.Position, b.Facing);
            b.Facing = b.AttackDir;
            b.AttackStartTick = now;
            int pattern = b.PatternIndex++ % 2;
            switch (d.Id)
            {
                case BossId.LargeBeast:
                    bool close = Vector2.Distance(b.Position, player) <= d.StrikeRadius * d.StrikeTriggerFactor;
                    if (pattern == 0 || !close)
                    {
                        b.Ai = BossAi.ChargeWindup;
                        b.AiUntilTick = now + w.Ticks(d.ChargeTelegraph);
                        b.ChargeLength = Mathf.Min(Vector2.Distance(b.Position, player) + d.ChargeOvershoot, d.ChargeMaxDistance);
                        Telegraph(w, b, TelegraphShape.Lane, b.ChargeLength, d.ChargeTelegraph);
                    }
                    else
                    {
                        b.Ai = BossAi.StrikeWindup;
                        b.AiUntilTick = now + w.Ticks(d.StrikeWindup);
                        Telegraph(w, b, TelegraphShape.Circle, d.StrikeRadius, d.StrikeWindup);
                    }
                    break;

                default: // industrial robot and former companion: burst, then marked blast / grenade
                    if (pattern == 0)
                    {
                        b.Ai = BossAi.BurstWarning;
                        b.AiUntilTick = now + w.Ticks(d.BurstWarning);
                        Telegraph(w, b, TelegraphShape.Line, d.ProjectileRange, d.BurstWarning);
                        // The former companion combines patterns at low health; the marked zone keeps an escape.
                        if (d.Id == BossId.FormerCompanion && LowHealth(b, d)) Blast(w, b, d);
                    }
                    else
                    {
                        Blast(w, b, d);
                        b.Ai = BossAi.BlastWarning;
                        b.AiUntilTick = now + w.Ticks(d.BlastWarning);
                    }
                    break;
            }
        }

        static void Blast(World w, BossState b, BossData d)
        {
            Vector2 target = w.Player.Position;
            if (Vector2.Distance(b.Position, target) > d.BlastThrowRange)
                target = b.Position + SimMath.SafeNormal(target - b.Position, b.Facing) * d.BlastThrowRange;
            float flight = d.Id == BossId.FormerCompanion ? d.BlastWarning * d.BlastFlightFraction : 0f;
            HazardSystem.Create(w, HazardOwner.Enemy, b.Position, target, d.BlastRadius, d.Damage, d.BlastWarning, BossState.EntityId, default, flight);
            w.Emit(SimEventType.EnemyTelegraph, BossState.EntityId, target, Vector2.right * d.BlastRadius, d.BlastWarning, (int)TelegraphShape.Circle);
        }

        static void Rest(World w, BossState b, BossData d)
        {
            b.Ai = BossAi.Rest;
            float interval = LowHealth(b, d) ? d.LowHealthAttackInterval : d.AttackInterval;
            b.AiUntilTick = System.Math.Max(w.Clock.Tick, b.AttackStartTick + w.Ticks(interval));
        }

        static void Move(World w, BossState b, BossData d, Vector2 target)
        {
            if (Vector2.Distance(b.Position, target) < b.Radius + d.ApproachStopDistance) return;
            Vector2 dir = w.PlayerField.Direction(b.Position, target);
            b.Position = w.Arena.MoveCircle(b.Position, dir * d.MoveSpeed * Core.SimClock.Dt, b.Radius);
            if (dir.sqrMagnitude > 1e-6f) b.Facing = dir;
        }

        static void Telegraph(World w, BossState b, TelegraphShape shape, float length, float seconds) =>
            w.Emit(SimEventType.EnemyTelegraph, BossState.EntityId, b.Position, b.AttackDir * length, seconds, (int)shape);

        static void Attack(World w, BossState b) =>
            w.Emit(SimEventType.EnemyAttack, BossState.EntityId, b.Position, b.AttackDir, 1, -1);

        static void FireShot(World w, BossState b, BossData d, Vector2 dir)
        {
            w.Projectiles.Add(new ProjectileState
            {
                Id = w.AllocateId(), Owner = ProjectileOwner.Enemy, SourceId = BossState.EntityId,
                Position = b.Position, PrevPosition = b.Position, Origin = b.Position,
                Velocity = dir * d.ProjectileSpeed, Damage = d.BurstShotDamage, Range = d.ProjectileRange,
                Radius = w.Profile.Combat.EnemyProjectileRadius, TargetsLeft = 1,
            });
        }

        static bool HitAlongPath(World w, BossState b, Vector2 from, float damage)
        {
            bool hit = false;
            float rp = b.Radius + w.Profile.Player.BodyRadius;
            if (!w.Player.Dead && SimMath.SegmentPointDistanceSq(from, b.Position, w.Player.Position) <= rp * rp)
            {
                PlayerSystem.Damage(w, damage, b.AttackDir);
                hit = true;
            }
            if (CompanionSystem.Active(w))
            {
                float rc = b.Radius + w.Profile.Companion.BodyRadius;
                if (SimMath.SegmentPointDistanceSq(from, b.Position, w.Companion.Position) <= rc * rc)
                {
                    CompanionSystem.Damage(w, damage, b.AttackDir);
                    hit = true;
                }
            }
            return hit;
        }

        static void AreaDamage(World w, Vector2 at, float radius, float damage)
        {
            if (!w.Player.Dead && Vector2.Distance(w.Player.Position, at) <= radius + w.Profile.Player.BodyRadius)
                PlayerSystem.Damage(w, damage, SimMath.SafeNormal(w.Player.Position - at, Vector2.up));
            if (CompanionSystem.Active(w) && Vector2.Distance(w.Companion.Position, at) <= radius + w.Profile.Companion.BodyRadius)
                CompanionSystem.Damage(w, damage, SimMath.SafeNormal(w.Companion.Position - at, Vector2.up));
        }

        // ---------------------------------------------------------------- damage, healing, EMP, death

        public static float Damage(World w, float amount, DamageSource source, Vector2 dir)
        {
            var b = w.Boss;
            if (!b.Active || b.Dead || amount <= 0) return 0;
            b.Health -= amount;
            w.Emit(SimEventType.EnemyHit, BossState.EntityId, b.Position, dir, amount, (int)source);
            if (b.Health <= 0) Defeat(w);
            return amount;
        }

        /// <summary>Healing of the boss (repair robots, medics) never exceeds HealCapFraction of max health in total.</summary>
        public static float Heal(World w, float amount)
        {
            var b = w.Boss;
            var d = Data(w);
            float cap = d.HealCapFraction * b.MaxHealth;
            float allowed = Mathf.Min(amount, cap - b.HealedTotal, b.MaxHealth - b.Health);
            if (allowed <= 0) return 0;
            b.Health += allowed;
            b.HealedTotal += allowed;
            return allowed;
        }

        public static void Emp(World w, float stun, float resistance)
        {
            var b = w.Boss;
            if (!b.Active || b.Dead || !Data(w).EmpAffects || w.Clock.Tick < b.EmpResistantUntilTick) return;
            b.StunnedUntilTick = w.Clock.Tick + w.Ticks(stun);
            b.EmpResistantUntilTick = b.StunnedUntilTick + w.Ticks(resistance);
            b.Ai = BossAi.Rest;
            b.AiUntilTick = b.StunnedUntilTick;
        }

        static void Defeat(World w)
        {
            var b = w.Boss;
            var d = Data(w);
            b.Health = 0;
            b.Dead = true;
            b.Active = false;

            // Pay whatever the fixed pool has not paid yet: the boss share plus living or unspawned adds (§9).
            int xp = Mathf.Max(0, d.RewardXp - b.PoolXpPaid);
            int coins = Mathf.Max(0, d.RewardCoins - b.PoolCoinsPaid);
            b.PoolXpPaid += xp;
            b.PoolCoinsPaid += coins;
            Rewards.GrantXp(w, xp, b.Position);
            Rewards.DropCoins(w, b.Position, coins);

            // Disable adds, cancel queues, clear hazards (§7). Their share was already paid above.
            foreach (var e in w.Enemies)
            {
                if (!e.Alive) continue;
                e.Alive = false;
                w.Emit(SimEventType.EnemyDied, e.Id, e.Position, Vector2.zero, 0, (int)e.Type);
            }
            b.AddQueue.Clear();
            foreach (var h in w.Hazards) h.Cancelled = true;
            foreach (var p in w.Projectiles) if (p.Owner == ProjectileOwner.Enemy) p.Alive = false;
            if (w.Profile.Economy.BossDeathCollectsGroundCoins)
                foreach (var c in w.Coins) if (c.Alive) c.Magnetized = true;

            w.Waves.Kind = EncounterKind.PhaseComplete;
            w.SafeInterval = true;
            w.Emit(SimEventType.BossDefeated, BossState.EntityId, b.Position, default, 0, (int)d.Id);
        }
    }
}
