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
                BossSystem.Damage(w, w.Boss.MaxHealth * 0.15f, DamageSource.Weapon, Vector2.right);
                for (int t = 0; t < 60 * 25; t++)
                {
                    sim.Step(PlayerCommand.None);
                    if (t % 20 == 0) KillAdds(w);
                    if (t > 2 && w.Boss.AddQueue.Count == 0 && !w.Enemies.Any(e => e.Alive)) break;
                }
                KillAdds(w);
            }
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
            BossSystem.Damage(w, w.Boss.MaxHealth * 0.31f, DamageSource.Weapon, Vector2.right); // below 70 %
            sim.Step(PlayerCommand.None);
            int afterFirst = Queued();
            Assert.AreEqual(opening + d.Thresholds[0].Adds.Sum(c => c.Count), afterFirst);

            w.Boss.HealedTotal = 0;
            BossSystem.Heal(w, w.Boss.MaxHealth); // back above 70 % (capped at 10 %: still below) ...
            w.Boss.Health = w.Boss.MaxHealth * 0.9f; // ... so force it above for the test
            BossSystem.Damage(w, w.Boss.MaxHealth * 0.25f, DamageSource.Weapon, Vector2.right);
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
        public void PushPulseNeverDisplacesTheBoss()
        {
            var sim = Fight(1);
            var w = sim.World;
            w.Phase = 1;
            w.Inventory.Add(ItemId.PushPulse, 1);
            w.Player.Position = w.Boss.Position + new Vector2(0, -2f);
            Vector2 before = w.Boss.Position;
            w.Boss.StunnedUntilTick = long.MaxValue;
            ItemSystem.Equip(w, ItemId.PushPulse);
            sim.Step(new PlayerCommand { FirePressed = true, FireHeld = true, AimWorld = before });
            sim.Run(20, PlayerCommand.None);
            Assert.AreEqual(before, w.Boss.Position);
        }

        [Test]
        public void ActiveCapIncludesTheBoss()
        {
            var sim = Fight(3);
            var w = sim.World;
            var d = w.Profile.Boss(3);
            BossSystem.Damage(w, w.Boss.MaxHealth * 0.7f, DamageSource.Weapon, Vector2.right);
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
    }
}
