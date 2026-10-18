using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    public enum BossAi
    {
        Rest, ChargeWindup, Charging, ChargeRecover, StrikeWindup, StrikeRecover, BurstWarning, Bursting, BurstRecover, BlastWarning, BlastRecover,
        PounceWindup, Pouncing, PounceRecover, LeapRise, LeapAir, LeapLock, LeapRecover,
    }

    public sealed class BossState
    {
        public const int EntityId = -2;
        public const int EncounterGroup = 1;
        /// <summary>D87: endless boss reinforcements; outside the reward pool.</summary>
        public const int ReinforcementGroup = 2;

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
        /// <summary>Pounce and leap: where the jump left from, when the current part of it began, pounces still to follow.</summary>
        public Vector2 LeapFrom;
        public long MoveStartTick;
        public int ChainLeft;
        /// <summary>D89: blasts of the current volley still to throw, how many were thrown, and when the next one goes.</summary>
        public int BlastsLeft;
        public int BlastIndex;
        public long NextBlastTick;

        public long StunnedUntilTick;
        public long EmpResistantUntilTick;
        public float HealedTotal;

        /// <summary>Threshold groups already queued: never again, even after healing or loading (§7).</summary>
        public List<int> FiredThresholds = new();
        public List<EnemyId> AddQueue = new();
        public long NextAddActiveTick;
        /// <summary>D87: reinforcements waiting to spawn, and when the next group is called.</summary>
        public List<EnemyId> ReinforceQueue = new();
        public long NextReinforceActiveTick;

        /// <summary>Encounter reward pool already paid out through adds (§9).</summary>
        public int PoolXpPaid;
        public int PoolCoinsPaid;
    }

    /// <summary>
    /// Boss encounters (GDD §7): opening adds, two threshold groups queued once, cap including the boss,
    /// readable telegraphed patterns that mostly target the player, shorter rests (never removed warnings)
    /// at low health, healing capped at a share of max health, EMP interrupt + resistance, and a fixed
    /// reward pool so a quick kill never earns less than a slow one.
    /// D86: armour blocks a share of every hit except in the exposed windows (recovering or stunned); the attack cycle
    /// comes from the data and adds the pounce (a hop onto the player's marked position) and the leap (off the map,
    /// the landing zone follows the player, locks, then a slam); the burst can fan, sweep there and back and track.
    /// D89: every heavy move breaks the obstacles it reaches; burst bullets can home; blasts can come as a volley
    /// placed around the player's heading.
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
            b.ReinforceQueue.Clear();
            b.NextReinforceActiveTick = w.Clock.ActiveTicks + w.Ticks(d.ReinforceInterval);
            b.PoolXpPaid = b.PoolCoinsPaid = 0;
            b.HealedTotal = 0;
            b.StunnedUntilTick = b.EmpResistantUntilTick = 0;
            b.ChainLeft = 0;
            b.BlastsLeft = b.BlastIndex = 0;

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
            if (b.BlastsLeft > 0 && w.Clock.Tick >= b.NextBlastTick) ThrowNextBlast(w, b, d);
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
            // D87: the opening and threshold adds come first; then a reinforcement group is called every interval.
            if (b.AddQueue.Count == 0 && b.ReinforceQueue.Count == 0 && d.ReinforceInterval > 0f && d.Reinforcements.Count > 0 &&
                w.Clock.ActiveTicks >= b.NextReinforceActiveTick)
            {
                foreach (var c in d.Reinforcements) for (int i = 0; i < c.Count; i++) b.ReinforceQueue.Add(c.Id);
                b.NextReinforceActiveTick = w.Clock.ActiveTicks + w.Ticks(d.ReinforceInterval);
            }
            bool reinforcing = b.AddQueue.Count == 0;
            var queue = reinforcing ? b.ReinforceQueue : b.AddQueue;
            if (queue.Count == 0 || w.Clock.ActiveTicks < b.NextAddActiveTick) return;
            if (WaveSystem.AliveCount(w) + 1 >= d.ActiveCap) return; // the cap includes the boss
            int support = 0;
            foreach (var e in w.Enemies) if (e.Alive && w.Profile.Enemy(e.Type).CountsAsSupport) support++;
            int index = queue.FindIndex(id => !w.Profile.Enemy(id).CountsAsSupport || support < w.Profile.Spawning.SupportCap);
            if (index < 0) return;
            var type = queue[index];
            if (!WaveSystem.FindSpawnPoint(w, w.Profile.Enemy(type).BodyRadius, out var pos))
            {
                b.NextAddActiveTick = w.Clock.ActiveTicks + w.Ticks(w.Profile.Spawning.NoSafePointRetryDelay);
                return;
            }
            EnemySystem.Spawn(w, type, pos, true, reinforcing ? BossState.ReinforcementGroup : BossState.EncounterGroup);
            queue.RemoveAt(index);
            b.NextAddActiveTick = w.Clock.ActiveTicks + System.Math.Max(1, w.Ticks(d.AddSpawnGap));
        }

        // ---------------------------------------------------------------- patterns

        static bool LowHealth(BossState b, BossData d) => b.Health / b.MaxHealth <= d.LowHealthFraction;

        /// <summary>Mid-jump (pounce or leap): bodies do not collide with it.</summary>
        public static bool InAir(BossState b) => b.Ai is BossAi.Pouncing or BossAi.LeapRise or BossAi.LeapAir or BossAi.LeapLock;

        /// <summary>Off the map during a leap: no shot, blade, blast, mine or pulse can reach it.</summary>
        public static bool OffMap(BossState b) => b.Ai is BossAi.LeapRise or BossAi.LeapAir or BossAi.LeapLock;

        /// <summary>The boss can be hit (and targeted) right now.</summary>
        public static bool Targetable(BossState b) => b.Active && !b.Dead && !OffMap(b);

        /// <summary>The punish windows: recovering after an attack, or stunned (D86).</summary>
        public static bool Exposed(World w, BossState b) => w.Clock.Tick < b.StunnedUntilTick ||
            b.Ai is BossAi.ChargeRecover or BossAi.StrikeRecover or BossAi.BurstRecover or BossAi.BlastRecover or BossAi.PounceRecover or BossAi.LeapRecover;

        /// <summary>Share of a hit the boss's armour blocks right now.</summary>
        public static float ArmorNow(World w) => Exposed(w, w.Boss) ? Data(w).ExposedArmor : Data(w).Armor;

        /// <summary>0..1 through the current timed part of a pounce or leap (the view draws the jump from it).</summary>
        public static float MoveProgress(World w) => Flight(w.Clock.Tick, w.Boss);

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
                    // D87: obstacles just ahead give way instead of stopping the charge; walls still stop it.
                    if (d.HeavyMovesBreakObstacles) Obstacles.BreakAround(w, from + b.AttackDir * step * 2, b.Radius);
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
                case BossAi.LeapRecover:
                    if (now >= b.AiUntilTick) Rest(w, b, d);
                    break;
                case BossAi.PounceRecover:
                    if (now < b.AiUntilTick) break;
                    if (b.ChainLeft > 0) { b.ChainLeft--; StartPounce(w, b, d); }
                    else Rest(w, b, d);
                    break;

                case BossAi.PounceWindup:
                    if (now >= b.AiUntilTick)
                    {
                        b.Ai = BossAi.Pouncing;
                        b.LeapFrom = b.Position;
                        b.MoveStartTick = now;
                        b.AiUntilTick = now + System.Math.Max(1, w.Ticks(d.PounceFlightTime));
                        Attack(w, b);
                    }
                    break;
                case BossAi.Pouncing:
                    b.Position = Vector2.Lerp(b.LeapFrom, b.AttackPoint, Flight(now, b));
                    if (now >= b.AiUntilTick) Land(w, b, d, d.PounceRadius, d.PounceDamage, BossAi.PounceRecover, d.PounceRecovery);
                    break;

                case BossAi.LeapRise:
                    if (now >= b.AiUntilTick)
                    {
                        b.Ai = BossAi.LeapAir;
                        b.MoveStartTick = now;
                        b.AiUntilTick = now + w.Ticks(d.LeapTrackTime);
                    }
                    break;
                case BossAi.LeapAir:
                {
                    // Off the map, so walls and bodies do not matter: the landing zone simply follows the player.
                    Vector2 to = player - b.Position;
                    float step = d.LeapTrackSpeed * Core.SimClock.Dt;
                    b.Position = to.magnitude <= step ? player : b.Position + to.normalized * step;
                    if (now >= b.AiUntilTick)
                    {
                        b.Position = b.AttackPoint = w.Arena.NearestFree(b.Position, b.Radius);
                        b.Ai = BossAi.LeapLock;
                        b.MoveStartTick = now;
                        b.AiUntilTick = now + System.Math.Max(1, w.Ticks(d.LeapLockTime));
                        w.Emit(SimEventType.EnemyTelegraph, BossState.EntityId, b.Position, Vector2.right * d.LeapRadius, d.LeapLockTime, (int)TelegraphShape.Circle);
                    }
                    break;
                }
                case BossAi.LeapLock:
                    if (now >= b.AiUntilTick) Land(w, b, d, d.LeapRadius, d.LeapDamage, BossAi.LeapRecover, d.LeapRecovery);
                    break;

                case BossAi.StrikeWindup:
                    if (now >= b.AiUntilTick)
                    {
                        Attack(w, b);
                        w.Emit(SimEventType.Explosion, BossState.EntityId, b.Position, default, d.StrikeRadius, (int)HazardOwner.Enemy);
                        AreaDamage(w, b.Position, d.StrikeRadius, d.Damage);
                        if (d.HeavyMovesBreakObstacles) Obstacles.BreakAround(w, b.Position, d.StrikeRadius);
                        b.Ai = BossAi.StrikeRecover;
                        b.AiUntilTick = now + w.Ticks(d.StrikeRecovery);
                    }
                    break;

                case BossAi.BurstWarning:
                    Track(w, b, d);
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
                    Track(w, b, d);
                    if (now >= b.NextShotTick && now < b.AiUntilTick)
                    {
                        // Sweeps across the warned cone (there and back for several sweeps), never outside it.
                        float t = (now - b.BurstStartTick) / (float)System.Math.Max(1, b.AiUntilTick - b.BurstStartTick);
                        float s = t * System.Math.Max(1, d.BurstSweeps);
                        int pass = (int)s;
                        float f = s - pass;
                        if ((pass & 1) == 1) f = 1f - f;
                        Vector2 center = SimMath.Rotate(b.AttackDir, Mathf.Lerp(-d.BurstSpread * 0.5f, d.BurstSpread * 0.5f, f));
                        int fan = System.Math.Max(1, d.BurstFanCount);
                        for (int k = 0; k < fan; k++) FireShot(w, b, d, SimMath.Rotate(center, (k - (fan - 1) * 0.5f) * d.BurstFanSpacing));
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
            var move = d.Moves.Count > 0 ? d.Moves[b.PatternIndex++ % d.Moves.Count] : BossMove.Charge;
            if (move == BossMove.Strike && Vector2.Distance(b.Position, player) > d.StrikeRadius * d.StrikeTriggerFactor)
                move = d.Moves.Contains(BossMove.Pounce) ? BossMove.Pounce : BossMove.Charge; // too far: close in instead
            switch (move)
            {
                case BossMove.Charge:
                    b.Ai = BossAi.ChargeWindup;
                    b.AiUntilTick = now + w.Ticks(d.ChargeTelegraph);
                    b.ChargeLength = Mathf.Min(Vector2.Distance(b.Position, player) + d.ChargeOvershoot, d.ChargeMaxDistance);
                    Telegraph(w, b, TelegraphShape.Lane, b.ChargeLength, d.ChargeTelegraph);
                    break;

                case BossMove.Strike:
                    b.Ai = BossAi.StrikeWindup;
                    b.AiUntilTick = now + w.Ticks(d.StrikeWindup);
                    Telegraph(w, b, TelegraphShape.Circle, d.StrikeRadius, d.StrikeWindup);
                    break;

                case BossMove.Burst:
                    b.Ai = BossAi.BurstWarning;
                    b.AiUntilTick = now + w.Ticks(d.BurstWarning);
                    Telegraph(w, b, TelegraphShape.Line, d.ProjectileRange, d.BurstWarning);
                    // The former companion combines patterns at low health; the marked zone keeps an escape.
                    if (d.Id == BossId.FormerCompanion && LowHealth(b, d)) Blast(w, b, d);
                    break;

                case BossMove.Blast:
                    Blast(w, b, d);
                    b.Ai = BossAi.BlastWarning;
                    b.AiUntilTick = now + w.Ticks(d.BlastWarning + d.BlastGap * System.Math.Max(0, d.BlastCount - 1));
                    break;

                case BossMove.Pounce:
                    b.ChainLeft = System.Math.Max(0, (LowHealth(b, d) ? d.PounceChainLowHealth : d.PounceChain) - 1);
                    StartPounce(w, b, d);
                    break;

                case BossMove.Leap:
                    b.Ai = BossAi.LeapRise;
                    b.LeapFrom = b.Position;
                    b.MoveStartTick = now;
                    b.AiUntilTick = now + System.Math.Max(1, w.Ticks(d.LeapRiseTime));
                    Attack(w, b);
                    w.Emit(SimEventType.EnemyTelegraph, BossState.EntityId, b.Position, Vector2.right * d.LeapRadius,
                        d.LeapRiseTime + d.LeapTrackTime + d.LeapLockTime, (int)TelegraphShape.Circle);
                    break;
            }
        }

        /// <summary>Marks the landing zone on the player's position (within reach) for the telegraph and the flight.</summary>
        static void StartPounce(World w, BossState b, BossData d)
        {
            long now = w.Clock.Tick;
            Vector2 to = w.Player.Position - b.Position;
            if (to.magnitude > d.PounceRange) to = to.normalized * d.PounceRange;
            b.AttackPoint = w.Arena.NearestFree(b.Position + to, b.Radius);
            b.AttackDir = SimMath.SafeNormal(b.AttackPoint - b.Position, b.Facing);
            b.Facing = b.AttackDir;
            b.Ai = BossAi.PounceWindup;
            b.AiUntilTick = now + System.Math.Max(1, w.Ticks(d.PounceTelegraph));
            w.Emit(SimEventType.EnemyTelegraph, BossState.EntityId, b.AttackPoint, Vector2.right * d.PounceRadius,
                d.PounceTelegraph + d.PounceFlightTime, (int)TelegraphShape.Circle);
        }

        static void Land(World w, BossState b, BossData d, float radius, float damage, BossAi recover, float recovery)
        {
            b.Position = b.AttackPoint;
            Attack(w, b);
            w.Emit(SimEventType.Explosion, BossState.EntityId, b.Position, default, radius, (int)HazardOwner.Enemy);
            AreaDamage(w, b.Position, radius, damage);
            if (d.HeavyMovesBreakObstacles) Obstacles.BreakAround(w, b.Position, radius);
            b.Ai = recover;
            b.AiUntilTick = w.Clock.Tick + w.Ticks(recovery);
        }

        /// <summary>0..1 through the current timed part of a pounce or leap.</summary>
        static float Flight(long now, BossState b) =>
            Mathf.Clamp01((now - b.MoveStartTick) / (float)System.Math.Max(1, b.AiUntilTick - b.MoveStartTick));

        /// <summary>The warned burst cone turns toward the player, no faster than the data allows.</summary>
        static void Track(World w, BossState b, BossData d)
        {
            if (d.BurstTrackRate <= 0f) return;
            Vector2 want = SimMath.SafeNormal(w.Player.Position - b.Position, b.AttackDir);
            float max = d.BurstTrackRate * Core.SimClock.Dt;
            b.AttackDir = SimMath.Rotate(b.AttackDir, Mathf.Clamp(Vector2.SignedAngle(b.AttackDir, want), -max, max));
            b.Facing = b.AttackDir;
        }

        static void Blast(World w, BossState b, BossData d)
        {
            if (d.BlastCount <= 1)
            {
                Vector2 target = w.Player.Position;
                if (Vector2.Distance(b.Position, target) > d.BlastThrowRange)
                    target = b.Position + SimMath.SafeNormal(target - b.Position, b.Facing) * d.BlastThrowRange;
                ThrowBlast(w, b, d, target);
                return;
            }
            b.BlastsLeft = d.BlastCount;
            b.BlastIndex = 0;
            ThrowNextBlast(w, b, d);
        }

        /// <summary>
        /// D89 volley: each blast is placed when it is thrown, from where the player is and where they are heading - the
        /// first ahead of them, the next ones on alternate sides - always leaving a gap to the player's body, so a player
        /// who reads the zones can stay out of them, but running on in a straight line does not.
        /// </summary>
        static void ThrowNextBlast(World w, BossState b, BossData d)
        {
            var p = w.Player;
            Vector2 heading = p.Velocity.sqrMagnitude > 0.5f ? p.Velocity.normalized : SimMath.SafeNormal(p.Position - b.Position, b.Facing);
            float gap = d.BlastRadius + w.Profile.Player.BodyRadius + d.BlastPlayerGap;
            int k = b.BlastIndex;
            float distance = k == 0 ? Mathf.Max(gap, p.Velocity.magnitude * d.BlastLead) : gap;
            float angle = k == 0 ? 0f : d.BlastSideAngle * ((k + 1) / 2) * ((k & 1) == 1 ? 1f : -1f);
            ThrowBlast(w, b, d, p.Position + SimMath.Rotate(heading, angle) * distance);
            b.BlastIndex++;
            b.BlastsLeft--;
            b.NextBlastTick = w.Clock.Tick + System.Math.Max(1, w.Ticks(d.BlastGap));
        }

        static void ThrowBlast(World w, BossState b, BossData d, Vector2 target)
        {
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
                Radius = w.Profile.Combat.EnemyProjectileRadius, TargetsLeft = 1, Homing = d.BurstHoming,
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
            if (!Targetable(b) || amount <= 0) return 0;
            float dealt = amount * (1f - ArmorNow(w));
            b.Health -= dealt;
            w.Emit(SimEventType.EnemyHit, BossState.EntityId, b.Position, dir, dealt, (int)source);
            if (b.Health <= 0) Defeat(w);
            return dealt;
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
            if (!Targetable(b) || !Data(w).EmpAffects || w.Clock.Tick < b.EmpResistantUntilTick) return;
            if (b.Ai == BossAi.Pouncing) b.Position = w.Arena.NearestFree(b.Position, b.Radius); // dropped mid-hop
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
            b.ReinforceQueue.Clear();
            b.BlastsLeft = 0;
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
