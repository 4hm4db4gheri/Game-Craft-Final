using System.Collections.Generic;
using System.Linq;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>
    /// T21: a scripted bot plays the whole game — 15 waves and 3 bosses — with a fixed seed, and checks for
    /// soft-locks, stuck encounters, reward errors and save/load drift along the way (§6, §13, §16).
    /// The bot aims and fires at the nearest enemy with real player commands; enemies that live too long are
    /// removed so the run always progresses (it tests the flow, not the bot's skill).
    /// </summary>
    public class FullRunTests
    {
        const ulong Seed = 2026;
        const int EncounterBudgetTicks = 60 * 60 * 8; // 8 simulated minutes per wave or boss at most

        sealed class Bot
        {
            /// <summary>One bot decision: identical for identical worlds, so twins stay comparable.</summary>
            public static PlayerCommand Act(World w)
            {
                Vector2 me = w.Player.Position;
                Vector2 aim = me + Vector2.right;
                float best = float.MaxValue;
                foreach (var e in w.Enemies)
                {
                    if (!e.IsActive) continue;
                    float d = (e.Position - me).sqrMagnitude;
                    if (d < best) { best = d; aim = e.Position; }
                }
                if (w.Boss.Active && !w.Boss.Dead && (w.Boss.Position - me).sqrMagnitude < best) aim = w.Boss.Position;
                float a = w.Clock.Tick * 0.02f;
                return new PlayerCommand { Move = new Vector2(Mathf.Cos(a), Mathf.Sin(a * 1.3f)) * 0.6f, AimWorld = aim, FireHeld = true };
            }

            /// <summary>The game-flow parts GameSession normally does, plus progress guarantees.</summary>
            public static void Manage(World w)
            {
                var p = w.Player;
                p.InvulnerableUntilTick = w.Clock.Tick + 2;
                if (w.Companion.Present) w.Companion.Health = w.Companion.MaxHealth;
                foreach (var e in w.Enemies.ToList())
                    if (e.IsActive && w.Clock.Tick - e.SpawnTick > 240) Combat.DamageEnemy(w, e, 1e7f, DamageSource.Weapon, Vector2.right);
                if (w.Boss.Active && !w.Boss.Dead && w.Clock.Tick % 30 == 0) BossSystem.Damage(w, w.Boss.MaxHealth * 0.015f, DamageSource.Weapon, Vector2.right);
                foreach (var c in w.Coins.ToList()) CoinSystem.Collect(w, c);
                if (w.Tutorial == TutorialStage.Purchase) ShopSystem.Purchase(w, OfferKind.BuyWeapon, w.Profile.Economy.TutorialWeapon);
                if (w.Tutorial == TutorialStage.Grenade) { ItemSystem.EquipSelected(w); ItemSystem.Use(w); }
                while (w.Progress.Pending != null) XpSystem.Choose(w, w.Progress.Pending.Options[0]);
                switch (w.Waves.Kind)
                {
                    case EncounterKind.SafeInterval: WaveSystem.SkipSafeInterval(w); break;
                    case EncounterKind.BossPrep: BossSystem.StartEncounter(w); break;
                    case EncounterKind.PhaseComplete:
                        foreach (var c in w.Coins) CoinSystem.Collect(w, c);
                        w.Coins.Clear();
                        if (w.Phase < 3) PhaseSystem.EnterPhase(w, w.Phase + 1);
                        else w.Completed = true;
                        break;
                }
            }
        }

        static ArenaLayout Layout => WaveTests.RealArena();

        static void Tick(Simulation sim)
        {
            sim.Step(Bot.Act(sim.World));
            Bot.Manage(sim.World);
        }

        const int TwinTicks = 900;

        /// <summary>Saves through the real save file format and loads it into a new world (the "twin").</summary>
        static Simulation SaveAndLoad(Simulation sim, string label)
        {
            var w = sim.World;
            var copy = new Simulation(SaveFile.FromJson(SaveFile.Create(w, 0).ToJson()).LoadWorld(Layout));
            copy.World.Profile = w.Profile;
            Assert.AreEqual(WorldSerializer.Hash(w), WorldSerializer.Hash(copy.World), $"{label}: state changed by save/load");
            return copy;
        }

        static void CheckReward(List<string> errors, string label, int coins, int expectedCoins, int xp, int expectedXp)
        {
            if (coins != expectedCoins || xp != expectedXp)
                errors.Add($"{label}: coins {coins} (expected {expectedCoins}), XP {xp} (expected {expectedXp})");
        }

        [Test]
        public void BotPlaysTheWholeGameWithoutSoftLocksRewardErrorsOrDrift()
        {
            var sim = SimTestUtil.NewSim(Layout, seed: Seed, companion: true);
            var w = sim.World;
            WaveSystem.StartWave(w, 1);

            var report = new List<string>();
            var durations = new List<string>();
            var drift = new List<string>();
            Simulation twin = null;
            int twinLeft = 0;
            string twinLabel = null;
            int encounterStart = 0, ticks = 0, waves = 0, bosses = 0;
            int coinsAtStart = 0, xpAtStart = 0, encPhase = 1, encWave = 1;
            var calcWaves = BalanceCalculator.Waves(w.Profile);
            var calcPhases = BalanceCalculator.Phases(w.Profile);
            var rewardErrors = new List<string>();
            string current = "phase 1 wave 1";
            var driftPoints = new HashSet<string> { "1-3", "1-B", "2-2", "2-B", "3-4", "3-B" };

            while (!w.Completed)
            {
                Tick(sim);
                ticks++;
                if (twin != null)
                {
                    // The loaded copy is played with the same bot beside the original: any drift shows up here.
                    Tick(twin);
                    twin.World.Events.Clear();
                    if (--twinLeft == 0)
                    {
                        Assert.AreEqual(WorldSerializer.Hash(w), WorldSerializer.Hash(twin.World), $"{twinLabel}: loaded game drifted from the original");
                        drift.Add($"{twinLabel}: identical state after save/load and after {TwinTicks} more ticks");
                        twin = null;
                    }
                }
                foreach (var ev in w.Events)
                {
                    switch (ev.Type)
                    {
                        case SimEventType.WaveStarted:
                            current = $"phase {w.Phase} wave {(int)ev.Value}";
                            encPhase = w.Phase;
                            encWave = (int)ev.Value;
                            encounterStart = ticks;
                            coinsAtStart = w.Progress.CoinsCollected;
                            xpAtStart = (int)w.Progress.TotalXp;
                            if (twin == null && driftPoints.Contains($"{w.Phase}-{(int)ev.Value}")) { twin = SaveAndLoad(sim, current); twinLeft = TwinTicks; twinLabel = current; }
                            break;
                        case SimEventType.BossStarted:
                            current = $"phase {w.Phase} boss";
                            encPhase = w.Phase;
                            encWave = 0;
                            encounterStart = ticks;
                            coinsAtStart = w.Progress.CoinsCollected;
                            xpAtStart = (int)w.Progress.TotalXp;
                            if (twin == null && driftPoints.Contains($"{w.Phase}-B")) { twin = SaveAndLoad(sim, current); twinLeft = TwinTicks; twinLabel = current; }
                            break;
                        case SimEventType.WaveCompleted:
                            waves++;
                            var cw = calcWaves.Single(x => x.Phase == encPhase && x.Wave == encWave);
                            CheckReward(rewardErrors, current, w.Progress.CoinsCollected - coinsAtStart, cw.KillCoins + cw.BonusCoins, (int)w.Progress.TotalXp - xpAtStart, cw.Xp);
                            durations.Add($"{current}: {(ticks - encounterStart) / 60f:0.0} s");
                            break;
                        case SimEventType.BossDefeated:
                            bosses++;
                            var cp = calcPhases.Single(x => x.Phase == encPhase);
                            // The boss pool is paid at its death; ground coins are pulled in at the phase end (D26).
                            foreach (var c in w.Coins.ToList()) CoinSystem.Collect(w, c);
                            CheckReward(rewardErrors, current, w.Progress.CoinsCollected - coinsAtStart, cp.BossPoolCoins, (int)w.Progress.TotalXp - xpAtStart, cp.BossPoolXp);
                            durations.Add($"{current}: {(ticks - encounterStart) / 60f:0.0} s");
                            break;
                    }
                }
                w.Events.Clear();
                Assert.AreEqual(FailureReason.None, w.Failure, current);
                Assert.Less(ticks - encounterStart, EncounterBudgetTicks, $"soft-lock: {current} ({w.Waves.Kind}) did not finish within 8 simulated minutes");
            }

            Assert.AreEqual(15, waves, "regular waves completed");
            Assert.AreEqual(3, bosses, "bosses defeated");
            Assert.GreaterOrEqual(drift.Count, 5, "save/load drift checked across the run");

            // Rewards: every wave and boss pool paid exactly once, nothing more (§9).
            Assert.IsEmpty(rewardErrors, "reward errors:\n" + string.Join("\n", rewardErrors));
            int expectedXp = calcWaves.Sum(x => x.Xp) + calcPhases.Sum(x => x.BossPoolXp);
            int expectedCoins = calcWaves.Sum(x => x.KillCoins + x.BonusCoins) + calcPhases.Sum(x => x.BossPoolCoins);
            Assert.AreEqual(expectedXp, w.Progress.TotalXp, "total XP");
            Assert.AreEqual(expectedCoins, w.Progress.CoinsCollected, "total coins (kills, wave bonuses, boss pools)");

            report.Add($"seed {Seed}: 15 waves and 3 bosses in {ticks / 3600f:0.0} simulated minutes ({ticks} ticks)");
            report.Add($"kills {w.Progress.Kills}, XP {w.Progress.TotalXp} = expected {expectedXp}, coins {w.Progress.CoinsCollected} = expected {expectedCoins}");
            report.AddRange(durations);
            report.AddRange(drift);
            foreach (var line in report) TestContext.WriteLine(line);
            System.IO.File.WriteAllLines(System.IO.Path.Combine(Application.dataPath, "../Logs/fullrun_report.txt"), report);
        }
    }
}
