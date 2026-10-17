using System.IO;
using System.Linq;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using Hollow.Tests.Data;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>T17 acceptance tests (GDD §12).</summary>
    public class SaveTests
    {
        string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "hollow_save_tests_" + System.Guid.NewGuid().ToString("N"));
            SlotStore.RootOverride = tempRoot;
        }

        [TearDown]
        public void TearDown()
        {
            SlotStore.RootOverride = null;
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }

        /// <summary>Deterministic input that keeps moving, aiming and firing.</summary>
        static PlayerCommand Script(long tick)
        {
            float a = tick * 0.05f;
            return new PlayerCommand
            {
                Move = new Vector2(Mathf.Cos(a * 0.7f), Mathf.Sin(a * 0.5f)),
                AimWorld = new Vector2(Mathf.Cos(a) * 6, Mathf.Sin(a) * 6),
                FireHeld = tick % 90 < 60,
                DashPressed = tick % 170 == 0,
                ReloadPressed = tick % 230 == 0,
                SelectWeaponSlot = tick % 400 == 0 ? 2 : tick % 400 == 200 ? 1 : 0,
            };
        }

        static Simulation MidFight()
        {
            var sim = SimTestUtil.NewSim(WaveTests.RealArena(), seed: 99, companion: true);
            var w = sim.World;
            w.Player.Health = 5000;
            WeaponSystem.Give(w, WeaponId.DoubleBarrel);
            w.Tutorial = TutorialStage.None;
            WaveSystem.StartWave(w, 3);
            for (long t = 0; t < 60 * 25; t++) sim.Step(Script(w.Clock.Tick));
            Assume.That(w.Enemies.Count, Is.GreaterThan(0), "scenario must be mid-fight");
            return sim;
        }

        [Test]
        public void SaveQuitLoadRestoresTheExactStateAndIdenticalFuture()
        {
            var sim = MidFight();
            var w = sim.World;
            string hashBefore = WorldSerializer.Hash(w);

            var result = SlotStore.Write(1, SaveFile.Create(w, 123));
            Assert.IsTrue(result.Ok, result.Error);

            // "Relaunch": nothing in memory is reused; everything comes from the file.
            var loadedSave = SlotStore.Read(1);
            var loaded = new Simulation(loadedSave.LoadWorld(WaveTests.RealArena()));
            Assert.AreEqual(hashBefore, WorldSerializer.Hash(loaded.World), "state hash after load");

            for (int i = 0; i < 60 * 20; i++)
            {
                var cmd = Script(w.Clock.Tick);
                sim.Step(cmd);
                loaded.Step(cmd);
            }
            Assert.AreEqual(WorldSerializer.Hash(w), WorldSerializer.Hash(loaded.World), "identical outcome after 1200 ticks");
            Assert.AreEqual(w.Enemies.Count, loaded.World.Enemies.Count);
        }

        [Test]
        public void SaveContainsTheInterestingMidFightState()
        {
            var w = MidFight().World;
            string json = WorldSerializer.ToJson(w);
            StringAssert.Contains("\"Queue\"", json);
            StringAssert.Contains("\"Projectiles\"", json);
            StringAssert.Contains("\"S0\"", json, "RNG state");
            StringAssert.DoesNotContain("\"IsActive\"", json, "computed properties are not saved");
        }

        [Test]
        public void PendingCardIsRestoredWithoutReroll()
        {
            var sim = SimTestUtil.NewSim(WaveTests.RealArena(), seed: 5);
            var w = sim.World;
            Rewards.GrantXp(w, 45, Vector2.zero);
            XpSystem.Step(w);
            var options = w.Progress.Pending.Options.ToList();
            SlotStore.Write(2, SaveFile.Create(w, 1));
            var loaded = SlotStore.Read(2).LoadWorld(WaveTests.RealArena());
            CollectionAssert.AreEqual(options, loaded.Progress.Pending.Options);
            Assert.AreEqual(5, loaded.Progress.XpTowardNext);
        }

        [Test]
        public void SlotsAreIndependent()
        {
            var a = SimTestUtil.NewSim(WaveTests.RealArena(), seed: 1).World;
            var b = SimTestUtil.NewSim(WaveTests.RealArena(), seed: 2).World;
            SlotStore.Write(1, SaveFile.Create(a, 10));
            SlotStore.Write(2, SaveFile.Create(b, 20));
            byte[] slotB = File.ReadAllBytes(SlotStore.PathOf(2));
            a.Progress.Wallet = 777;
            SlotStore.Write(1, SaveFile.Create(a, 30));
            CollectionAssert.AreEqual(slotB, File.ReadAllBytes(SlotStore.PathOf(2)));
            Assert.AreEqual(777, SlotStore.Read(1).LoadWorld(WaveTests.RealArena()).Progress.Wallet);
            Assert.IsFalse(SlotStore.Exists(3));
            Assert.IsFalse(File.Exists(SlotStore.PathOf(1) + ".tmp"), "no temp file left behind");
        }

        [Test]
        public void SaveKeepsItsOwnBalanceSnapshot()
        {
            var w = SimTestUtil.NewSim(WaveTests.RealArena()).World;
            SlotStore.Write(1, SaveFile.Create(w, 0));
            w.Profile.Player.MoveSpeed = 9; // a later panel edit
            var save = SlotStore.Read(1);
            Assert.AreEqual(5f, save.Profile.Player.MoveSpeed);
            Assert.AreEqual(5f, save.LoadWorld(WaveTests.RealArena()).Profile.Player.MoveSpeed);
            Assert.IsNotEmpty(save.ProfileHash);
        }

        [Test]
        public void DeathRestoresTheWaveStartAndUndoesTheFailedAttempt()
        {
            var sim = SimTestUtil.NewSim(WaveTests.RealArena(), seed: 3, companion: true);
            var w = sim.World;
            w.Progress.Wallet = 500;
            w.Player.Health = 64;
            WaveSystem.StartWave(w, 2);
            sim.Run(60 * 5, PlayerCommand.None);
            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.DoubleBarrel);
            PlayerSystem.Damage(w, 10000, Vector2.left);
            Assert.AreEqual(FailureReason.PlayerDied, w.Failure);

            var restored = Checkpoints.Restore(w);
            Assert.AreEqual(FailureReason.None, restored.Failure);
            Assert.AreEqual(500, restored.Progress.Wallet, "purchase undone");
            Assert.IsFalse(restored.Player.Loadout.Owns(WeaponId.DoubleBarrel));
            Assert.AreEqual(PlayerStats.MaxHealth(restored), restored.Player.Health, "v0.19 §16: a retry fills health to the current maximum");
            Assert.AreEqual(restored.Companion.MaxHealth, restored.Companion.Health);
            Assert.AreEqual(2, restored.Waves.Wave);
            Assert.AreEqual(0, restored.Waves.SpawnedTotal, "the wave restarts");
            Assert.AreEqual(1, restored.Retries);
            Assert.IsNotNull(restored.CheckpointJson, "can fail and retry again");
        }

        [Test]
        public void CompanionDeathAlsoRestoresTheCheckpoint()
        {
            var sim = SimTestUtil.NewSim(WaveTests.RealArena(), seed: 4, companion: true);
            var w = sim.World;
            WaveSystem.StartWave(w, 2);
            sim.Run(60, PlayerCommand.None);
            CompanionSystem.Damage(w, 10000, Vector2.left);
            var restored = Checkpoints.Restore(w);
            Assert.IsFalse(restored.Companion.Dead);
            Assert.AreEqual(140f, restored.Companion.Health);
        }

        [Test]
        public void UnwritableSlotReportsFailure()
        {
            var w = SimTestUtil.NewSim(WaveTests.RealArena()).World;
            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(SlotStore.PathOf(1)); // a folder where the file should go
            var result = SlotStore.Write(1, SaveFile.Create(w, 0));
            Assert.IsFalse(result.Ok);
            Assert.IsNotEmpty(result.Error);
        }
    }
}
