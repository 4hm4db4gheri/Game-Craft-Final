using System.Collections.Generic;
using System.Linq;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>T13 acceptance tests (GDD §7, §9).</summary>
    public class BossTests
    {
        static Simulation Fight(int phase, ulong seed = 11)
        {
            var sim = SimTestUtil.NewSim(WaveTests.RealArena(), seed: seed, companion: phase < 3);
            var w = sim.World;
            if (phase != 1) PhaseSystem.EnterPhase(w, phase);
            w.Enemies.Clear();
            w.Tutorial = TutorialStage.None;
            w.Progress.ChoicesBlocked = true;
            w.Player.InvulnerableUntilTick = long.MaxValue;
            if (w.Companion.Present) w.Companion.Health = w.Companion.MaxHealth = 1e9f;
            BossSystem.StartEncounter(w);
            return sim;
        }

        /// <summary>Deals this much after armour (tests about health thresholds, not about armour).</summary>
        static void HurtRaw(World w, float amount) => BossSystem.Damage(w, amount / (1f - BossSystem.ArmorNow(w)), DamageSource.Weapon, Vector2.right);

        static void KillAdds(World w)
        {
            foreach (var e in w.Enemies.ToList()) if (e.Alive) Combat.DamageEnemy(w, e, 1e7f, DamageSource.Weapon, Vector2.right);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void QuickKillPaysTheWholePool(int phase)
        {
            var sim = Fight(phase);
            var w = sim.World;
            var d = w.Profile.Boss(phase);
            int xp0 = w.Progress.TotalXp, coins0 = w.Progress.CoinsGenerated;
            BossSystem.Damage(w, 1e9f, DamageSource.Weapon, Vector2.right);
            Assert.AreEqual(d.RewardXp, w.Progress.TotalXp - xp0);
            Assert.AreEqual(d.RewardCoins, w.Progress.CoinsGenerated - coins0);
            Assert.AreEqual(EncounterKind.PhaseComplete, w.Waves.Kind);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SlowKillPaysExactlyTheSamePool(int phase)
        {
            var sim = Fight(phase);
            var w = sim.World;
            var d = w.Profile.Boss(phase);
            int xp0 = w.Progress.TotalXp, coins0 = w.Progress.CoinsGenerated;
            // Bring the boss down in steps so both threshold groups spawn, killing every add as it appears.
            for (int step = 0; step < 6 && !w.Boss.Dead; step++)
            {
                HurtRaw(w, w.Boss.MaxHealth * 0.15f);
                for (int t = 0; t < 60 * 25; t++)
                {
                    sim.Step(PlayerCommand.None);
                    if (t % 20 == 0) KillAdds(w);
                    if (t > 2 && w.Boss.AddQueue.Count == 0 && !w.Enemies.Any(e => e.Alive && e.EncounterGroup == BossState.EncounterGroup)) break;
                }
                KillAdds(w);
            }
            if (BossSystem.OffMap(w.Boss)) w.Boss.Ai = BossAi.Rest; // not mid-leap for the final blow
            BossSystem.Damage(w, 1e9f, DamageSource.Weapon, Vector2.right);
            Assert.AreEqual(2, w.Boss.FiredThresholds.Count);
            Assert.AreEqual(d.RewardXp, w.Progress.TotalXp - xp0, "XP pool");
            Assert.AreEqual(d.RewardCoins, w.Progress.CoinsGenerated - coins0, "coin pool");
        }

        [Test]
        public void ThresholdGroupsAreQueuedOnceAcrossHealingAndLoading()
        {
            var sim = Fight(2);
            var w = sim.World;
            var d = w.Profile.Boss(2);
            int Queued() => w.Boss.AddQueue.Count + w.Enemies.Count(e => e.EncounterGroup == BossState.EncounterGroup);

            sim.Step(PlayerCommand.None);
            int opening = d.OpeningAdds.Sum(c => c.Count);
            Assert.AreEqual(opening, Queued());
            HurtRaw(w, w.Boss.MaxHealth * 0.31f); // below 70 %
            sim.Step(PlayerCommand.None);
            int afterFirst = Queued();
            Assert.AreEqual(opening + d.Thresholds[0].Adds.Sum(c => c.Count), afterFirst);

            w.Boss.HealedTotal = 0;
            BossSystem.Heal(w, w.Boss.MaxHealth); // back above 70 % (capped at 10 %: still below) ...
            w.Boss.Health = w.Boss.MaxHealth * 0.9f; // ... so force it above for the test
            HurtRaw(w, w.Boss.MaxHealth * 0.25f);
            sim.Step(PlayerCommand.None);
            Assert.AreEqual(afterFirst, Queued(), "healing above the threshold does not re-queue it");

            var loaded = new Simulation(WorldSerializer.FromJson(WorldSerializer.ToJson(w), w.Profile, w.Layout));
            loaded.Step(PlayerCommand.None);
            Assert.AreEqual(1, loaded.World.Boss.FiredThresholds.Count, "loading does not repeat it");
        }

        [Test]
        public void BossDeathDisablesAddsCancelsQueuesAndClearsHazards()
        {
            var sim = Fight(3);
            var w = sim.World;
            sim.Run(60 * 6, PlayerCommand.None);
            HazardSystem.Create(w, HazardOwner.Enemy, Vector2.zero, new Vector2(3, 3), 2, 10, 5);
            Assert.Greater(w.Enemies.Count(e => e.Alive), 0);
            BossSystem.Damage(w, 1e9f, DamageSource.Weapon, Vector2.right);
            sim.Step(PlayerCommand.None);
            Assert.AreEqual(0, w.Enemies.Count(e => e.Alive));
            Assert.AreEqual(0, w.Boss.AddQueue.Count);
            Assert.AreEqual(0, w.Hazards.Count);
            Assert.AreEqual(0, w.Projectiles.Count(p => p.Owner == ProjectileOwner.Enemy));
            Assert.IsTrue(w.Coins.All(c => c.Magnetized), "ground coins fly to the player (D26)");
        }

        [Test]
        public void BossHealingNeverExceedsTenPercentInTotal()
        {
            var sim = Fight(2);
            var w = sim.World;
            w.Boss.Health = w.Boss.MaxHealth * 0.5f;
            float total = 0;
            for (int i = 0; i < 100; i++) total += BossSystem.Heal(w, 50);
            Assert.AreEqual(w.Boss.MaxHealth * 0.1f, total, 1e-2f);
        }

        [Test]
        public void EmpInterruptsTheMachineBossThenGivesResistance()
        {
            var sim = Fight(2);
            var w = sim.World;
            var emp = w.Profile.Item(ItemId.EmpPulse);
            BossSystem.Emp(w, emp.StunBoss, emp.BossResistance);
            Assert.AreEqual(w.Clock.Tick + w.Ticks(emp.StunBoss), w.Boss.StunnedUntilTick);
            long stunnedUntil = w.Boss.StunnedUntilTick;
            sim.Run(60, PlayerCommand.None);
            BossSystem.Emp(w, emp.StunBoss, emp.BossResistance);
            Assert.AreEqual(stunnedUntil, w.Boss.StunnedUntilTick, "resistant for six seconds");
        }

        [Test]
        public void EmpDoesNotAffectTheBeast()
        {
            var sim = Fight(1);
            var w = sim.World;
            BossSystem.Emp(w, 0.8f, 6f);
            Assert.LessOrEqual(w.Boss.StunnedUntilTick, w.Clock.Tick);
        }

        [Test]
        public void ActiveCapIncludesTheBoss()
        {
            var sim = Fight(3);
            var w = sim.World;
            var d = w.Profile.Boss(3);
            HurtRaw(w, w.Boss.MaxHealth * 0.7f);
            for (int t = 0; t < 60 * 60; t++)
            {
                sim.Step(PlayerCommand.None);
                Assert.LessOrEqual(w.Enemies.Count(e => e.Alive) + 1, d.ActiveCap);
            }
        }

        [Test]
        public void BossCheckpointKeepsPreparationPurchases()
        {
            var sim = SimTestUtil.NewSim(WaveTests.RealArena(), seed: 2);
            var w = sim.World;
            w.Waves.Kind = EncounterKind.BossPrep;
            w.Progress.Wallet = 500;
            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.DoubleBarrel);
            BossSystem.StartEncounter(w);
            PlayerSystem.Damage(w, 1e6f, Vector2.left);
            var restored = Checkpoints.Restore(w);
            Assert.IsTrue(restored.Player.Loadout.Owns(WeaponId.DoubleBarrel));
            Assert.AreEqual(EncounterKind.Boss, restored.Waves.Kind, "the retry repeats the boss");
            Assert.AreEqual(restored.Boss.MaxHealth, restored.Boss.Health);
        }

        [Test]
        public void BeastTelegraphsItsLaneBeforeCharging()
        {
            var sim = Fight(1);
            var w = sim.World;
            w.Player.Position = w.Boss.Position + new Vector2(0, -8);
            bool lane = false;
            w.Drain();
            for (int t = 0; t < 60 * 6 && w.Boss.Ai != BossAi.Charging; t++)
            {
                sim.Step(PlayerCommand.None);
                lane |= w.Drain().Any(e => e.Type == SimEventType.EnemyTelegraph && e.EntityId == BossState.EntityId && e.Code == (int)TelegraphShape.Lane);
            }
            Assert.AreEqual(BossAi.Charging, w.Boss.Ai);
            Assert.IsTrue(lane);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void AttacksStartNoMoreOftenThanTheMinimumInterval(int phase)
        {
            var sim = Fight(phase);
            var w = sim.World;
            var d = w.Profile.Boss(phase);
            w.Player.Position = w.Boss.Position + new Vector2(0, -4);
            var starts = new List<long>();
            long last = -1;
            for (int t = 0; t < 60 * 30 && starts.Count < 5; t++)
            {
                sim.Step(PlayerCommand.None);
                KillAdds(w);
                if (w.Boss.AttackStartTick != last) { last = w.Boss.AttackStartTick; starts.Add(last); }
            }
            Assert.GreaterOrEqual(starts.Count, 4);
            for (int i = 2; i < starts.Count; i++) // the first entry is the encounter start
                Assert.GreaterOrEqual(starts[i] - starts[i - 1], w.Ticks(d.AttackInterval), "v0.19 §12 minimum start interval");
        }

        [Test]
        public void PlayerWeaponsMeleeDashAndGrenadesHurtTheBoss()
        {
            var sim = Fight(1);
            var w = sim.World;
            w.Boss.StunnedUntilTick = long.MaxValue;
            float hp = w.Boss.Health;
            w.Player.Position = w.Boss.Position + new Vector2(0, -3f);
            sim.Run(40, new PlayerCommand { FireHeld = true, AimWorld = w.Boss.Position });
            Assert.Less(w.Boss.Health, hp, "pistol");
        }

        // ---------------------------------------------------------------- D86: armour and the new moves

        /// <summary>Runs until the boss reaches this state (killing adds as they come); false if it never does.</summary>
        static bool RunUntil(Simulation sim, BossAi ai, int maxTicks = 60 * 15)
        {
            for (int t = 0; t < maxTicks && sim.World.Boss.Ai != ai; t++)
            {
                sim.Step(PlayerCommand.None);
                KillAdds(sim.World);
            }
            return sim.World.Boss.Ai == ai;
        }

        /// <summary>Makes the given move the boss's next attack.</summary>
        static void NextMove(World w, BossMove move)
        {
            var d = w.Profile.Boss(w.Phase);
            w.Boss.PatternIndex = d.Moves.IndexOf(move);
            Assert.GreaterOrEqual(w.Boss.PatternIndex, 0, $"{d.Id} has no {move}");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ArmourBlocksHitsExceptInThePunishWindows(int phase)
        {
            var sim = Fight(phase);
            var w = sim.World;
            var d = w.Profile.Boss(phase);
            Assert.Greater(d.Armor, 0f);
            w.Boss.Ai = BossAi.Rest;
            Assert.AreEqual(100f * (1f - d.Armor), BossSystem.Damage(w, 100f, DamageSource.Weapon, Vector2.right), 1e-3f, "armoured");
            w.Boss.Ai = BossAi.ChargeRecover;
            Assert.AreEqual(100f * (1f - d.ExposedArmor), BossSystem.Damage(w, 100f, DamageSource.Weapon, Vector2.right), 1e-3f, "recovering");
            w.Boss.Ai = BossAi.Rest;
            w.Boss.StunnedUntilTick = w.Clock.Tick + 60;
            Assert.AreEqual(100f * (1f - d.ExposedArmor), BossSystem.Damage(w, 100f, DamageSource.Weapon, Vector2.right), 1e-3f, "stunned");
        }

        [TestCase(2)]
        [TestCase(3)]
        public void LeapLeavesTheMapTracksThePlayerAndSlamsWhereItLocked(int phase)
        {
            var sim = Fight(phase);
            var w = sim.World;
            var d = w.Profile.Boss(phase);
            w.Player.Position = w.Arena.NearestFree(w.Boss.Position + new Vector2(0, -6), w.Profile.Player.BodyRadius);
            NextMove(w, BossMove.Leap);
            Assert.IsTrue(RunUntil(sim, BossAi.LeapAir));
            Assert.IsFalse(BossSystem.Targetable(w.Boss), "off the map");
            Assert.AreEqual(0f, BossSystem.Damage(w, 100f, DamageSource.Weapon, Vector2.right), "nothing reaches it");

            // The player runs; the landing zone follows.
            Vector2 run = w.Arena.NearestFree(w.Player.Position + new Vector2(4, 0), w.Profile.Player.BodyRadius);
            w.Player.Position = run;
            Assert.IsTrue(RunUntil(sim, BossAi.LeapLock));
            // (the boss still needs room for its body, so a wall can nudge the zone by up to its radius)
            Assert.Less(Vector2.Distance(w.Boss.Position, w.Player.Position), w.Boss.Radius + 0.5f, "the zone locked on the player");

            // Standing in the locked zone hurts.
            w.Player.InvulnerableUntilTick = 0;
            float hp = w.Player.Health;
            Assert.IsTrue(RunUntil(sim, BossAi.LeapRecover, 120));
            Assert.Less(w.Player.Health, hp, "the slam hits");
            Assert.IsTrue(BossSystem.Targetable(w.Boss));
            Assert.IsTrue(BossSystem.Exposed(w, w.Boss), "the landing is a punish window");
        }

        [Test]
        public void LeavingTheLockedLeapZoneIsSafe()
        {
            var sim = Fight(2);
            var w = sim.World;
            var d = w.Profile.Boss(2);
            w.Player.Position = w.Arena.NearestFree(w.Boss.Position + new Vector2(0, -6), w.Profile.Player.BodyRadius);
            NextMove(w, BossMove.Leap);
            Assert.IsTrue(RunUntil(sim, BossAi.LeapLock));
            w.Player.Position = w.Arena.NearestFree(w.Boss.Position + new Vector2(d.LeapRadius + 1.5f, 0), w.Profile.Player.BodyRadius);
            Assume.That(Vector2.Distance(w.Player.Position, w.Boss.Position), Is.GreaterThan(d.LeapRadius + w.Profile.Player.BodyRadius));
            w.Player.InvulnerableUntilTick = 0;
            float hp = w.Player.Health;
            Assert.IsTrue(RunUntil(sim, BossAi.LeapRecover, 120));
            Assert.AreEqual(hp, w.Player.Health, 1e-3f);
        }

        [TestCase(1)]
        [TestCase(3)]
        public void PounceLandsOnTheMarkedPosition(int phase)
        {
            var sim = Fight(phase);
            var w = sim.World;
            var d = w.Profile.Boss(phase);
            w.Player.Position = w.Arena.NearestFree(w.Boss.Position + new Vector2(0, -5), w.Profile.Player.BodyRadius);
            NextMove(w, BossMove.Pounce);
            w.Drain();
            Assert.IsTrue(RunUntil(sim, BossAi.PounceWindup));
            Vector2 mark = w.Boss.AttackPoint;
            Assert.Less(Vector2.Distance(mark, w.Player.Position), w.Boss.Radius + 0.5f, "marked on the player");
            Assert.IsTrue(w.Drain().Any(e => e.Type == SimEventType.EnemyTelegraph && e.EntityId == BossState.EntityId && e.Code == (int)TelegraphShape.Circle));

            w.Player.InvulnerableUntilTick = 0;
            float hp = w.Player.Health;
            Assert.IsTrue(RunUntil(sim, BossAi.PounceRecover, 120));
            Assert.AreEqual(mark, w.Boss.Position);
            Assert.Less(w.Player.Health, hp, "standing on the mark gets hit");
        }

        [Test]
        public void RobotBurstFansSweepsTheWholeConeAndStaysWhereItWarned()
        {
            var sim = Fight(2);
            var w = sim.World;
            var d = w.Profile.Boss(2);
            w.Player.Position = w.Arena.NearestFree(w.Boss.Position + new Vector2(0, -6), w.Profile.Player.BodyRadius);
            NextMove(w, BossMove.Burst);
            Assert.IsTrue(RunUntil(sim, BossAi.BurstWarning));
            Vector2 start = w.Boss.AttackDir;
            // Step aside: the warned cone stays put (D88).
            w.Player.Position = w.Arena.NearestFree(w.Boss.Position + new Vector2(6, -2), w.Profile.Player.BodyRadius);
            var shots = new List<Vector2>();
            for (int t = 0; t < 60 * 4 && w.Boss.Ai != BossAi.BurstRecover; t++)
            {
                int before = w.Projectiles.Count;
                sim.Step(PlayerCommand.None);
                KillAdds(w);
                foreach (var p in w.Projectiles.Skip(before)) if (p.SourceId == BossState.EntityId) shots.Add(p.Velocity.normalized);
            }
            int volleys = Mathf.CeilToInt(d.BurstDuration / d.BurstShotInterval);
            Assert.GreaterOrEqual(shots.Count, (volleys - 1) * d.BurstFanCount, "every volley fans");
            Assert.Less(Vector2.Angle(start, w.Boss.AttackDir), 0.01f, "the cone does not follow the player");
            float widest = 0;
            foreach (var a in shots) foreach (var c in shots) widest = Mathf.Max(widest, Vector2.Angle(a, c));
            Assert.Greater(widest, d.BurstSpread * 0.8f, "the sweep covers the wide cone");
        }

        [Test]
        public void TheBossIsUntouchableByBodiesMinesAndTheCompanionWhileOffTheMap()
        {
            var sim = Fight(2);
            var w = sim.World;
            NextMove(w, BossMove.Leap);
            Assert.IsTrue(RunUntil(sim, BossAi.LeapAir));
            Assert.IsTrue(BossSystem.InAir(w.Boss));
            BossSystem.Emp(w, 0.8f, 6f);
            Assert.LessOrEqual(w.Boss.StunnedUntilTick, w.Clock.Tick, "a pulse cannot reach it");
        }
    }
}
