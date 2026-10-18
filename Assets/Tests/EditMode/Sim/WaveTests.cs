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
    /// <summary>T10 acceptance tests (GDD §6, §7, §9).</summary>
    public class WaveTests
    {
        public static ArenaLayout RealArena() =>
            ArenaLayoutStore.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, "Resources/Arena/ArenaLayout.json")));

        static Simulation Game(BalanceProfile profile = null, ulong seed = 7, bool companion = true) =>
            SimTestUtil.NewSim(RealArena(), profile, seed, companion);

        /// <summary>Kills every enemy that has been active for a while, as an efficient player would.</summary>
        static void KillActive(World w, int minActiveTicks)
        {
            foreach (var e in w.Enemies.ToList())
                if (e.IsActive && w.Clock.Tick - e.SpawnTick > minActiveTicks)
                    Combat.DamageEnemy(w, e, 100000, DamageSource.Weapon, Vector2.right);
        }

        [Test]
        public void TutorialRunsWolvesThenPurchaseThenQuills()
        {
            var sim = Game();
            var w = sim.World;
            w.Player.InvulnerableUntilTick = long.MaxValue;
            WaveSystem.StartWave(w, 1);
            Assert.AreEqual(TutorialStage.FirstGroup, w.Tutorial);

            int maxAlive = 0;
            for (int t = 0; t < 60 * 60 && w.Tutorial == TutorialStage.FirstGroup; t++)
            {
                sim.Step(PlayerCommand.None);
                maxAlive = Mathf.Max(maxAlive, WaveSystem.AliveCount(w));
                Assert.IsTrue(w.Enemies.All(e => e.Type == EnemyId.A1_RunnerWolf), "only wolves before the purchase");
                KillActive(w, 30);
            }
            Assert.AreEqual(TutorialStage.Purchase, w.Tutorial);
            Assert.LessOrEqual(maxAlive, 3, "tutorial cap of three");
            Assert.AreEqual(8, w.Coins.Count, "eight ground coins");
            Assert.IsTrue(w.Coins.All(c => c.NeverExpires), "tutorial coins never expire");
            Assert.IsNull(w.Progress.Pending, "no XP card during the tutorial");

            sim.Run(60 * 10, PlayerCommand.None);
            Assert.AreEqual(0, w.Enemies.Count, "nothing spawns until the purchase");

            foreach (var c in w.Coins.ToList()) CoinSystem.Collect(w, c);
            Assert.AreEqual(8, w.Progress.Wallet);
            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg));
            Assert.AreEqual(TutorialStage.Grenade, w.Tutorial);
            var practice = w.Enemies.Single(e => e.Id == w.TutorialGrenadeEnemyId);
            sim.Run(60, PlayerCommand.None);
            Assert.AreEqual(1, w.Enemies.Count, "only the frozen practice target is present");
            Assert.IsTrue(ItemSystem.EquipSelected(w));
            w.Player.Aim = practice.Position;
            Assert.AreEqual(ItemUseResult.Used, ItemSystem.Use(w));
            Assert.AreEqual(TutorialStage.GrenadeThrown, w.Tutorial);
            sim.Run(60, PlayerCommand.None);
            Assert.AreEqual(TutorialStage.SecondGroup, w.Tutorial);

            for (int t = 0; t < 60 * 60 && w.Waves.Kind == EncounterKind.Wave; t++)
            {
                sim.Step(PlayerCommand.None);
                KillActive(w, 30);
            }
            Assert.AreEqual(EncounterKind.SafeInterval, w.Waves.Kind);
            Assert.AreEqual(TutorialStage.None, w.Tutorial);
            Assert.AreEqual(15, w.Progress.Wallet, "wave bonus credited directly");
        }

        [Test]
        public void CapsAreNeverExceededAndTotalsAreExact()
        {
            var sim = Game();
            var w = sim.World;
            w.Player.InvulnerableUntilTick = long.MaxValue;
            w.Companion.Present = false;
            WaveSystem.StartWave(w, 5); // 96 total incl. offspring, cap 24, ranged 7, support 2
            var data = w.Profile.Wave(1, 5);
            int planned = w.Waves.PlannedTotal;
            Assert.AreEqual(data.Composition.Sum(c => c.Count), planned);
            for (int t = 0; t < 60 * 300 && w.Waves.Kind == EncounterKind.Wave; t++)
            {
                sim.Step(PlayerCommand.None);
                int alive = w.Enemies.Count(e => e.Alive);
                Assert.LessOrEqual(alive, data.ActiveCap, "active cap");
                Assert.LessOrEqual(w.Enemies.Count(e => e.Alive && w.Profile.Enemy(e.Type).CountsAsRanged), data.RangedCap, "ranged cap");
                Assert.LessOrEqual(w.Enemies.Count(e => e.Alive && w.Profile.Enemy(e.Type).CountsAsSupport), w.Profile.Spawning.SupportCap, "support cap");
                if (t % 3 == 0) KillActive(w, 120);
            }
            Assert.AreEqual(EncounterKind.SafeInterval, w.Waves.Kind, "the wave finished");
            Assert.AreEqual(planned, w.Waves.SpawnedTotal);
        }

        [Test]
        public void SpawnsKeepSafeDistancesAndAvoidTheNearestZone()
        {
            var sim = Game();
            var w = sim.World;
            w.Player.InvulnerableUntilTick = long.MaxValue;
            float minPlayer = 4 * 2 * w.Profile.Player.BodyRadius;
            float minCompanion = 3 * 2 * w.Profile.Companion.BodyRadius;
            // Stand inside the south zone: it must never be used while we are nearest to it.
            var south = w.Layout.SpawnZones.First(z => z.Side == SpawnSide.South);
            w.Player.Position = w.Player.PrevPosition = south.Center;
            WaveSystem.StartWave(w, 2);
            w.Drain();
            for (int t = 0; t < 60 * 30; t++)
            {
                sim.Step(PlayerCommand.None);
                foreach (var ev in w.Drain().Where(e => e.Type == SimEventType.EnemySpawned))
                {
                    Assert.GreaterOrEqual(Vector2.Distance(ev.Position, w.Player.Position), minPlayer);
                    if (CompanionSystem.Active(w)) Assert.GreaterOrEqual(Vector2.Distance(ev.Position, w.Companion.Position), minCompanion - 0.3f);
                    bool inSouth = ev.Position.x >= south.Min.x && ev.Position.x <= south.Max.x && ev.Position.y >= south.Min.y && ev.Position.y <= south.Max.y;
                    Assert.IsFalse(inSouth, "zone nearest the player stays free");
                }
                KillActive(w, 60);
                w.Player.Position = w.Player.PrevPosition = south.Center;
            }
        }

        [Test]
        public void SpawnWarningPrecedesActivity()
        {
            var sim = Game();
            var w = sim.World;
            WaveSystem.StartWave(w, 2);
            sim.Run(5, PlayerCommand.None);
            var first = w.Enemies.First();
            Assert.AreEqual(EnemyAi.Spawning, first.Ai);
            sim.Run(60, PlayerCommand.None);
            Assert.AreNotEqual(EnemyAi.Spawning, first.Ai);
        }

        [Test]
        public void NewTypeGetsReadTimeBeforeMoreOfIt()
        {
            var sim = Game();
            var w = sim.World;
            w.Player.InvulnerableUntilTick = long.MaxValue;
            w.Waves.Introduced.AddRange(new[] { EnemyId.A1_RunnerWolf, EnemyId.A2_QuillShooter });
            WaveSystem.StartWave(w, 2); // introduces the charging boar
            long firstBoar = -1, secondBoar = -1;
            w.Drain();
            for (int t = 0; t < 60 * 60 && secondBoar < 0; t++)
            {
                sim.Step(PlayerCommand.None);
                foreach (var ev in w.Drain().Where(e => e.Type == SimEventType.EnemySpawned && e.Code == (int)EnemyId.A3_ChargingBoar))
                    if (firstBoar < 0) firstBoar = w.Clock.ActiveTicks; else secondBoar = w.Clock.ActiveTicks;
                KillActive(w, 20);
            }
            Assert.GreaterOrEqual(secondBoar - firstBoar, w.Ticks(w.Profile.Spawning.NewTypeReadTime));
        }

        [Test]
        public void SafeIntervalFreezesActiveTimeAndCanBeSkippedByTestHarness()
        {
            var sim = Game();
            var w = sim.World;
            w.Waves.Kind = EncounterKind.SafeInterval;
            w.Waves.Wave = 2;
            w.SafeInterval = true;
            w.Waves.SafeIntervalEndTick = w.Clock.Tick + w.Ticks(8);
            long active = w.Clock.ActiveTicks;
            sim.Run(60, PlayerCommand.None);
            Assert.AreEqual(active, w.Clock.ActiveTicks);
            WaveSystem.SkipSafeInterval(w);
            Assert.AreEqual(EncounterKind.Wave, w.Waves.Kind);
            Assert.AreEqual(3, w.Waves.Wave);
            Assert.IsFalse(w.SafeInterval);
        }

        [Test]
        public void FifteenSecondShopOpensAutomaticallyClosesForWaveAndKeepsPurchasesOnRetry()
        {
            var sim = Game();
            var w = sim.World;
            WaveSystem.StartWave(w, 2);
            w.Waves.Queue.Clear();
            w.Projectiles.Add(new ProjectileState { Owner = ProjectileOwner.Enemy });
            w.Projectiles.Add(new ProjectileState { Owner = ProjectileOwner.Player });
            w.Hazards.Add(new HazardState { Owner = HazardOwner.Enemy });
            w.Hazards.Add(new HazardState { Owner = HazardOwner.Player, DetonateTick = long.MaxValue });
            sim.Step(PlayerCommand.None);
            Assert.AreEqual(EncounterKind.SafeInterval, w.Waves.Kind);
            Assert.IsTrue(w.Shop.Open);
            Assert.IsFalse(w.Projectiles.Any(p => p.Owner == ProjectileOwner.Enemy));
            Assert.IsFalse(w.Hazards.Any(h => h.Owner == HazardOwner.Enemy));
            Assert.AreEqual(w.Ticks(15), w.Waves.SafeIntervalEndTick);
            w.Progress.Wallet = 1000;
            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.DoubleBarrel));
            sim.Step(new PlayerCommand { Move = Vector2.right, DashPressed = true, InteractPressed = true });
            Assert.IsTrue(w.Shop.Open, "E, dash and movement cannot close the timed shop");
            int remaining = (int)(w.Waves.SafeIntervalEndTick - w.Clock.Tick);
            sim.Run(remaining, PlayerCommand.None);
            Assert.AreEqual(EncounterKind.SafeInterval, w.Waves.Kind);
            sim.Step(PlayerCommand.None);
            Assert.AreEqual(EncounterKind.Wave, w.Waves.Kind);
            Assert.AreEqual(3, w.Waves.Wave);
            Assert.IsFalse(w.Shop.Open);
            Assert.IsTrue(Checkpoints.Restore(w).Player.Loadout.Owns(WeaponId.DoubleBarrel));
        }

        [Test]
        public void TestProfileMultipliersApplyButNotToTheTutorial()
        {
            var profile = TestData.Baseline();
            profile.ActiveTestProfile = "Intense";
            var sim = Game(profile);
            var w = sim.World;
            WaveSystem.StartWave(w, 1);
            Assert.AreEqual(6, w.Waves.PlannedTotal);
            Assert.AreEqual(3, w.Waves.ActiveCap);
            WaveSystem.StartWave(w, 2);
            Assert.AreEqual(40, w.Waves.PlannedTotal, "32 × 1.25");
            Assert.AreEqual(12, w.Waves.ActiveCap, "10 × 1.15 rounded");
        }

        /// <summary>T10 acceptance: scripted bot plays all 15 regular waves without a stuck wave.</summary>
        [Test]
        public void BotCompletesAllFifteenRegularWaves()
        {
            var sim = Game(seed: 1234);
            var w = sim.World;
            w.Player.InvulnerableUntilTick = long.MaxValue;
            w.Companion.Health = w.Companion.MaxHealth = 1e9f;
            WaveSystem.StartWave(w, 1);
            int wavesDone = 0;
            int ticks = 0;
            const int budget = 60 * 60 * 40; // 40 simulated minutes
            while (ticks < budget && !(w.Phase == 3 && w.Waves.Kind == EncounterKind.BossPrep))
            {
                sim.Step(PlayerCommand.None);
                ticks++;
                if (ticks % 2 == 0) KillActive(w, 45);
                foreach (var c in w.Coins.ToList()) CoinSystem.Collect(w, c);
                if (w.Tutorial == TutorialStage.Purchase) ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg);
                if (w.Tutorial == TutorialStage.Grenade)
                {
                    var practice = w.Enemies.Find(e => e.Id == w.TutorialGrenadeEnemyId);
                    if (practice != null) w.Player.Aim = practice.Position;
                    ItemSystem.EquipSelected(w);
                    ItemSystem.Use(w);
                }
                while (w.Progress.Pending != null) XpSystem.Choose(w, w.Progress.Pending.Options[0]);
                wavesDone += w.Events.Count(e => e.Type == SimEventType.WaveCompleted);
                w.Events.Clear();
                if (w.Waves.Kind == EncounterKind.SafeInterval) WaveSystem.SkipSafeInterval(w);
                if (w.Waves.Kind == EncounterKind.BossPrep && w.Phase < 3) PhaseSystem.EnterPhase(w, w.Phase + 1);
                Assert.AreEqual(FailureReason.None, w.Failure);
            }
            Assert.AreEqual(15, wavesDone, $"stuck at phase {w.Phase} wave {w.Waves.Wave} ({w.Waves.Kind}) after {ticks} ticks");
            TestContext.WriteLine($"15 waves in {ticks / 60f / 60f:0.0} simulated minutes; kills {w.Progress.Kills}; XP {w.Progress.TotalXp}");
        }
    }
}
