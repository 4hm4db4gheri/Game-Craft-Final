using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>T18: run telemetry, the §13 rules for applying panel edits, and the hard-coded constant guard.</summary>
    public class TelemetryAndApplyTests
    {
        static Simulation Game() => SimTestUtil.NewSim(WaveTests.RealArena(), seed: 7, companion: true);

        static void KillActive(World w, int minActiveTicks)
        {
            foreach (var e in w.Enemies.ToList())
                if (e.IsActive && w.Clock.Tick - e.SpawnTick > minActiveTicks)
                    Combat.DamageEnemy(w, e, 100000, DamageSource.Weapon, Vector2.right);
        }

        /// <summary>The SMG purchase gives a practice grenade; the second group waits until it hits the practice wolf.</summary>
        static void ThrowPracticeGrenade(World w)
        {
            var target = w.Enemies.Find(e => e.Id == w.TutorialGrenadeEnemyId);
            if (target != null) w.Player.Aim = target.Position;
            ItemSystem.EquipSelected(w);
            ItemSystem.Use(w);
        }

        static void StepObserved(Simulation sim, RunTelemetry t)
        {
            sim.Step(PlayerCommand.None);
            t.Observe(sim.World);
            t.Frame(1f / 60f, true);
            sim.World.Events.Clear();
        }

        [Test]
        public void TelemetryRecordsTheTutorialWave()
        {
            var sim = Game();
            var w = sim.World;
            w.Player.InvulnerableUntilTick = long.MaxValue;
            var t = new RunTelemetry(w, "test", "tests");
            WaveSystem.StartWave(w, 1);
            t.Observe(w);
            w.Events.Clear();

            for (int i = 0; i < 60 * 60 && w.Tutorial == TutorialStage.FirstGroup; i++) { StepObserved(sim, t); KillActive(w, 30); }
            foreach (var c in w.Coins.ToList()) CoinSystem.Collect(w, c);
            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg));
            t.Observe(w);
            w.Events.Clear();
            for (int i = 0; i < 60 * 60 && w.Waves.Kind == EncounterKind.Wave; i++)
            {
                if (w.Tutorial == TutorialStage.Grenade) ThrowPracticeGrenade(w);
                StepObserved(sim, t);
                KillActive(w, 30);
            }

            var o = t.Run.Observed;
            Assert.AreEqual(1, o.Encounters.Count);
            var e = o.Encounters[0];
            Assert.AreEqual((1, 1, "wave", "cleared"), (e.Phase, e.Wave, e.Kind, e.Result));
            Assert.Greater(e.DurationActiveSeconds, 0);
            Assert.Greater(e.AverageFps, 0);
            Assert.AreEqual(7, e.Kills, "four wolves, the practice wolf, then two quill shooters");
            Assert.AreEqual(w.Progress.CoinsCollected, o.CoinsCollected + o.WaveBonusCoins, "ground coins and wave bonus are kept apart");
            Assert.GreaterOrEqual(o.CoinsGenerated, 8);
            Assert.AreEqual(w.Profile.Wave(1, 1).CompletionBonus, o.WaveBonusCoins);
            Assert.AreEqual(1, o.Purchases.Count);
            StringAssert.Contains("BuyWeapon LightSmg", o.Purchases[0]);
            Assert.IsNotNull(o.FirstPurchaseActiveSeconds);
            Assert.AreEqual(15 + 3, t.Run.Calculated.Count, "expectations kept apart: 15 waves and 3 boss fights");
            Assert.AreEqual(7UL, t.Run.Seed);

            string json = t.ToJson();
            StringAssert.Contains("\"Observed\"", json);
            StringAssert.Contains("\"Calculated\"", json);
        }

        [Test]
        public void TelemetryRecordsDeathsSkillsItemsAndHealing()
        {
            var sim = Game();
            var w = sim.World;
            w.Tutorial = TutorialStage.None;
            var t = new RunTelemetry(w, "test", "tests");
            WaveSystem.StartWave(w, 2);

            Rewards.GrantXp(w, 1000, w.Player.Position);
            XpSystem.Step(w);
            Assert.IsTrue(XpSystem.Choose(w, w.Progress.Pending.Options[0]));
            w.Progress.Wallet = 1000;
            w.Player.Health = 1;
            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.HealPlayer));
            w.Inventory.Add(ItemId.Grenade, 1);
            ItemSystem.Equip(w, ItemId.Grenade);
            Assert.AreEqual(ItemUseResult.Used, ItemSystem.Use(w));
            t.Observe(w);
            w.Events.Clear();

            w.Failure = FailureReason.CompanionDied;
            t.Observe(w);
            t.End("left");

            var o = t.Run.Observed;
            Assert.AreEqual(1, o.SkillsChosen.Count);
            Assert.AreEqual(w.Profile.Economy.PlayerHealPrice, o.HealingCoinsSpent);
            Assert.AreEqual(1, o.PlayerHealsBought);
            Assert.AreEqual(1, o.ItemsUsed["Grenade"]);
            Assert.AreEqual(1, o.Deaths.Count);
            StringAssert.StartsWith("CompanionDied in phase 1 wave 2", o.Deaths[0]);
            Assert.AreEqual("failed", o.Encounters[0].Result);
            Assert.AreEqual("left", o.Outcome);
        }

        // ------------------------------------------------------------------ §13 apply rules

        [Test]
        public void EnemyHealthEditsApplyFromTheNextWaveStart()
        {
            var sim = Game();
            var w = sim.World;
            w.Tutorial = TutorialStage.None;
            WaveSystem.StartWave(w, 2);
            float before = w.Profile.Enemy(EnemyId.A1_RunnerWolf).Health;

            var edited = BalanceJson.Clone(w.Profile);
            edited.Enemy(EnemyId.A1_RunnerWolf).Health = before * 3f;
            w.Profile = edited; // what an Apply in the balance panel does

            Assert.AreEqual(before, EnemySystem.Spawn(w, EnemyId.A1_RunnerWolf, Vector2.zero, false).MaxHealth, "mid-wave spawns keep the wave's health");
            WaveSystem.StartWave(w, 3);
            Assert.AreEqual(before * 3f, EnemySystem.Spawn(w, EnemyId.A1_RunnerWolf, Vector2.zero, false).MaxHealth, "the next wave uses the edit");
        }

        [Test]
        public void WaveQueueEditsApplyFromTheNextWaveStart()
        {
            var sim = Game();
            var w = sim.World;
            w.Tutorial = TutorialStage.None;
            WaveSystem.StartWave(w, 2);
            int planned = w.Waves.PlannedTotal;

            var edited = BalanceJson.Clone(w.Profile);
            var comp = edited.Wave(1, 2).Composition;
            for (int i = 0; i < comp.Count; i++) comp[i] = new EnemyCount { Id = comp[i].Id, Count = comp[i].Count * 2 };
            w.Profile = edited;
            Assert.AreEqual(planned, w.Waves.PlannedTotal, "the running wave keeps its queue");

            WaveSystem.StartWave(w, 2);
            Assert.AreEqual(planned * 2, w.Waves.PlannedTotal, "a restart builds the queue from the edit");
        }

        [Test]
        public void PricesChangeOnlyAtTheNextShopOpening()
        {
            var w = Game().World;
            ShopSystem.SetOpen(w, true);
            int price = ShopSystem.Offers(w).First(o => o.Kind == OfferKind.HealPlayer).Price;

            var edited = BalanceJson.Clone(w.Profile);
            edited.Economy.PlayerHealPrice = price + 50;
            w.Profile = edited;
            Assert.AreEqual(price, ShopSystem.Offers(w).First(o => o.Kind == OfferKind.HealPlayer).Price, "open shop keeps its prices");

            ShopSystem.SetOpen(w, false);
            ShopSystem.SetOpen(w, true);
            Assert.AreEqual(price + 50, ShopSystem.Offers(w).First(o => o.Kind == OfferKind.HealPlayer).Price);
        }

        [Test]
        public void CompletedPurchasesAreNeverRewritten()
        {
            var w = Game().World;
            w.Tutorial = TutorialStage.None;
            w.Progress.Wallet = 500;
            ShopSystem.SetOpen(w, true);
            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg));
            int wallet = w.Progress.Wallet;

            var edited = BalanceJson.Clone(w.Profile);
            foreach (var l in edited.Weapon(WeaponId.LightSmg).Levels) l.Cost += 100;
            w.Profile = edited;
            ShopSystem.SetOpen(w, false);
            ShopSystem.SetOpen(w, true);

            Assert.AreEqual(wallet, w.Progress.Wallet, "nothing is charged or refunded afterwards");
            Assert.IsTrue(w.Player.Loadout.Owns(WeaponId.LightSmg));
            Assert.AreEqual(1, w.Player.Loadout.Get(WeaponId.LightSmg).Level);
        }

        // ------------------------------------------------------------------ hard-coded constants guard

        /// <summary>
        /// Every number literal left in the simulation, with why it is not gameplay data (docs/CONSTANTS_SCAN.md).
        /// Anything not listed here fails the test, so a new gameplay constant cannot slip into code.
        /// </summary>
        static readonly HashSet<string> AllowedLiterals = new()
        {
            // numeric safety epsilons and division guards
            "Arena/ArenaGrid.cs|6f", "Arena/ArenaGrid.cs|10f", "Arena/ArenaGrid.cs|7f", "Arena/ArenaGrid.cs|0.05f",
            "Bosses/BossSystem.cs|6f", "Combat/Combat.cs|8f", "Combat/ProjectileSystem.cs|6f", "Combat/ProjectileSystem.cs|0.01f",
            "Companion/CompanionSystem.cs|0.01f", "Enemies/EnemyBehaviours.cs|0.1f", "Enemies/EnemySystem.cs|6f", "Enemies/EnemySystem.cs|8f",
            "Enemies/EnemySystem.cs|5f", "Items/ItemSystem.cs|0.05f", "Player/PlayerSystem.cs|4f", "Player/PlayerSystem.cs|6f",
            "SimMath.cs|10f", "SimMath.cs|8f", "Weapons/MeleeSystem.cs|9", "Weapons/MeleeSystem.cs|0.01f", "Weapons/WeaponSystem.cs|9",
            "Weapons/WeaponSystem.cs|0.01f",
            // algorithm constants: solver iterations, quadratic formula, rate = 1 / interval, hashing, id formatting
            "Arena/ArenaGrid.cs|4", "SimMath.cs|4", "Weapons/MeleeSystem.cs|1.0", "Weapons/WeaponSystem.cs|1.0", "Combat/SpatialHash.cs|32",
            "Save/WorldSerializer.cs|32", "Save/WorldSerializer.cs|16", "Waves/WaveSystem.cs|8",
            // pre-sized list capacities (performance only)
            "Combat/HazardSystem.cs|32", "Combat/ProjectileSystem.cs|32", "Combat/ProjectileSystem.cs|8", "Combat/SpatialHash.cs|8",
            "Enemies/EnemySystem.cs|32", "Items/ItemSystem.cs|32", "Progression/XpSystem.cs|8", "Shop/ShopSystem.cs|32", "Weapons/MeleeSystem.cs|32",
            // fallback when an arena has no north spawn zone (never with the shipped layout)
            "Bosses/BossSystem.cs|8",
            // presentation helper living in the simulation: the expiry blink rate of a coin
            "Economy/CoinSystem.cs|6f", "Economy/CoinSystem.cs|0.35f",
            // GDD structure: three save slots (§12)
            "Save/SlotStore.cs|3",
            // definition of the telemetry statistic "slowest 1 % of frames"
            "Telemetry/RunTelemetry.cs|100",
        };

        [Test]
        public void NoUnlistedNumberLiteralsInTheSimulation()
        {
            var literal = new Regex(@"(?<![\w.])(\d+\.\d+f?|\d+f|\d+)(?![\w.])");
            var trivial = new HashSet<string> { "0", "1", "2", "0f", "1f", "2f", "0.5f", "0.0f", "1.0f" };
            string root = Path.Combine(Application.dataPath, "Scripts/Sim");
            var unknown = new List<string>();
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string rel = file.Substring(root.Length + 1).Replace('\\', '/');
                int n = 0;
                foreach (var raw in File.ReadLines(file))
                {
                    n++;
                    string line = raw.Trim();
                    if (line.StartsWith("//") || line.StartsWith("[")) continue;
                    int comment = line.IndexOf("//", System.StringComparison.Ordinal);
                    if (comment >= 0) line = line.Substring(0, comment);
                    line = Regex.Replace(line, "\"[^\"]*\"", "");
                    foreach (Match m in literal.Matches(line))
                        if (!trivial.Contains(m.Value) && !AllowedLiterals.Contains($"{rel}|{m.Value}"))
                            unknown.Add($"{rel}:{n} {m.Value}");
                }
            }
            Assert.IsEmpty(unknown, "Number literals in gameplay code; move them to BalanceProfile or justify them here:\n" + string.Join("\n", unknown));
        }
    }
}
