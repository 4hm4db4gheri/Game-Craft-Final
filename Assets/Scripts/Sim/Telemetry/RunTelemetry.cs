using System;
using System.Collections.Generic;
using Hollow.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>One wave or boss fight as it actually went (§13 telemetry).</summary>
    public sealed class EncounterObservation
    {
        public int Phase;
        /// <summary>1–5 for waves, 0 for the boss.</summary>
        public int Wave;
        public string Kind;
        public double StartActiveSeconds;
        public double DurationActiveSeconds;
        public double RealSeconds;
        public float AverageFps;
        /// <summary>Average of the slowest 1 % of frames.</summary>
        public float LowFps;
        public float PlayerDamageTaken;
        public float CompanionDamageTaken;
        public int Kills;
        public int CoinsDropped;
        public int CoinsCollected;
        public int XpGained;
        /// <summary>cleared, failed or unfinished.</summary>
        public string Result = "unfinished";

        [JsonIgnore] internal readonly List<float> FrameTimes = new();
    }

    /// <summary>Numbers predicted by the balance calculator for the same encounter (kept apart from observations, §13).</summary>
    public sealed class EncounterExpectation
    {
        public int Phase;
        public int Wave;
        public int MaxEnemies;
        public int Xp;
        public int KillCoins;
        public int BonusCoins;
    }

    public sealed class Observed
    {
        public string Outcome = "running";
        public double ActiveSeconds;
        public List<EncounterObservation> Encounters = new();
        public List<string> Deaths = new();
        public double? FirstPurchaseActiveSeconds;
        public int CoinsGenerated;
        public int CoinsCollected;
        public int WaveBonusCoins;
        public float PlayerDamageTaken;
        public float CompanionDamageTaken;
        public List<string> SkillsChosen = new();
        public List<string> Purchases = new();
        /// <summary>Shots per "weapon Lv level".</summary>
        public SortedDictionary<string, int> ShotsByWeapon = new();
        public SortedDictionary<string, int> ItemsUsed = new();
        public int HealingCoinsSpent;
        public int PlayerHealsBought;
        public int CompanionHealsBought;
        public float AverageFps;
    }

    public sealed class TelemetryRun
    {
        public int Format = 1;
        public string StartedUtc;
        public string Mode;
        public ulong Seed;
        public string ProfileName;
        public int ProfileRevision;
        public string ProfileHash;
        public string Build;
        public Observed Observed = new();
        public List<EncounterExpectation> Calculated = new();
    }

    /// <summary>
    /// Builds the per-run telemetry log from simulation events (§13): wave and boss durations, frame rate, death
    /// reasons, damage to player and companion, first purchase time, coins generated and collected, skills,
    /// weapons and levels, item use and healing spend. Calculated expectations are stored separately.
    /// Read-only with respect to gameplay; the Game layer writes the file.
    /// </summary>
    public sealed class RunTelemetry
    {
        public TelemetryRun Run { get; } = new();
        /// <summary>Set when an encounter ended or the run ended: time to write the file.</summary>
        public bool Dirty { get; set; }

        EncounterObservation current;
        FailureReason lastFailure;
        double fpsSum;
        int fpsFrames;

        public RunTelemetry(World w, string mode, string build)
        {
            Run.StartedUtc = DateTime.UtcNow.ToString("o");
            Run.Mode = mode;
            Run.Seed = w.Seed;
            Run.ProfileName = w.Profile.Name;
            Run.ProfileRevision = w.Profile.Revision;
            Run.ProfileHash = BalanceJson.Hash(w.Profile);
            Run.Build = build;
            foreach (var t in BalanceCalculator.Waves(w.Profile))
                Run.Calculated.Add(new EncounterExpectation { Phase = t.Phase, Wave = t.Wave, MaxEnemies = t.MaxTotal, Xp = t.Xp, KillCoins = t.KillCoins, BonusCoins = t.BonusCoins });
            foreach (var t in BalanceCalculator.Phases(w.Profile))
                Run.Calculated.Add(new EncounterExpectation { Phase = t.Phase, Wave = 0, MaxEnemies = t.BossAndAdds, Xp = t.BossPoolXp, KillCoins = t.BossPoolCoins });
            lastFailure = w.Failure;
        }

        /// <summary>Call once per rendered frame with the events of that frame (before they are cleared).</summary>
        public void Observe(World w)
        {
            var o = Run.Observed;
            foreach (var ev in w.Events)
            {
                switch (ev.Type)
                {
                    case SimEventType.WaveStarted: Begin(w, ev.Code, (int)ev.Value, "wave"); break;
                    case SimEventType.BossStarted: Begin(w, w.Phase, 0, "boss"); break;
                    case SimEventType.WaveCompleted:
                        o.WaveBonusCoins += (int)ev.Value;
                        Finish(w, "cleared");
                        break;
                    case SimEventType.BossDefeated: Finish(w, "cleared"); break;
                    case SimEventType.PlayerHit:
                        o.PlayerDamageTaken += ev.Value;
                        if (current != null) current.PlayerDamageTaken += ev.Value;
                        break;
                    case SimEventType.CompanionHit:
                        o.CompanionDamageTaken += ev.Value;
                        if (current != null) current.CompanionDamageTaken += ev.Value;
                        break;
                    case SimEventType.EnemyDied: if (current != null) current.Kills++; break;
                    case SimEventType.CoinDropped:
                        o.CoinsGenerated++;
                        if (current != null) current.CoinsDropped++;
                        break;
                    case SimEventType.CoinCollected:
                        o.CoinsCollected += (int)ev.Value;
                        if (current != null) current.CoinsCollected += (int)ev.Value;
                        break;
                    case SimEventType.XpGained: if (current != null) current.XpGained += (int)ev.Value; break;
                    case SimEventType.ShotFired when ev.EntityId == PlayerState.EntityId:
                        var id = (WeaponId)ev.Code;
                        var owned = w.Player.Loadout.Get(id);
                        Count(o.ShotsByWeapon, $"{id} Lv{(owned != null ? owned.Level : 1)}");
                        break;
                    case SimEventType.ItemUsed: Count(o.ItemsUsed, ((ItemId)ev.Code).ToString()); break;
                    case SimEventType.SkillChosen: o.SkillsChosen.Add($"{(SkillId)ev.Code} rank {(int)ev.Value} at {w.Clock.ActiveSeconds:F1}s"); break;
                    case SimEventType.Purchased:
                        var kind = (OfferKind)ev.Code;
                        string what = kind == OfferKind.BuyItem ? ((ItemId)ev.EntityId).ToString()
                            : kind is OfferKind.HealPlayer or OfferKind.HealCompanion ? "" : ((WeaponId)ev.EntityId).ToString();
                        string level = kind == OfferKind.UpgradeWeapon ? $" Lv{(int)ev.Direction.x}" : "";
                        o.Purchases.Add($"{kind} {what}{level} for {(int)ev.Value} at {w.Clock.ActiveSeconds:F1}s (phase {w.Phase})".Replace("  ", " "));
                        o.FirstPurchaseActiveSeconds ??= w.Clock.ActiveSeconds;
                        if (kind is OfferKind.HealPlayer or OfferKind.HealCompanion) o.HealingCoinsSpent += (int)ev.Value;
                        if (kind == OfferKind.HealPlayer) o.PlayerHealsBought++;
                        if (kind == OfferKind.HealCompanion) o.CompanionHealsBought++;
                        break;
                }
            }

            if (w.Failure != FailureReason.None && lastFailure == FailureReason.None)
            {
                o.Deaths.Add($"{w.Failure} in phase {w.Phase} {(w.Waves.Kind == EncounterKind.Boss ? "boss" : $"wave {w.Waves.Wave}")} at {w.Clock.ActiveSeconds:F1}s");
                Finish(w, "failed");
            }
            lastFailure = w.Failure;
            o.ActiveSeconds = w.Clock.ActiveSeconds;
        }

        /// <summary>Real frame time, counted only while gameplay runs.</summary>
        public void Frame(float realDeltaSeconds, bool gameplayRunning)
        {
            if (!gameplayRunning || realDeltaSeconds <= 0f) return;
            fpsSum += realDeltaSeconds;
            fpsFrames++;
            Run.Observed.AverageFps = (float)(fpsFrames / fpsSum);
            if (current == null) return;
            current.RealSeconds += realDeltaSeconds;
            current.FrameTimes.Add(realDeltaSeconds);
        }

        public void End(string outcome)
        {
            if (current != null) Finish(null, "unfinished");
            Run.Observed.Outcome = outcome;
            Dirty = true;
        }

        public string ToJson() => JsonConvert.SerializeObject(Run, Formatting.Indented);

        void Begin(World w, int phase, int wave, string kind)
        {
            if (current != null) Finish(w, "unfinished");
            current = new EncounterObservation { Phase = phase, Wave = wave, Kind = kind, StartActiveSeconds = w.Clock.ActiveSeconds };
            Run.Observed.Encounters.Add(current);
        }

        void Finish(World w, string result)
        {
            if (current == null) return;
            if (w != null) current.DurationActiveSeconds = w.Clock.ActiveSeconds - current.StartActiveSeconds;
            current.Result = result;
            Dirty = true;
            var times = current.FrameTimes;
            if (times.Count > 0)
            {
                double sum = 0;
                foreach (var t in times) sum += t;
                current.AverageFps = (float)(times.Count / sum);
                times.Sort();
                int n = Mathf.Max(1, times.Count / 100);
                double slow = 0;
                for (int i = times.Count - n; i < times.Count; i++) slow += times[i];
                current.LowFps = (float)(n / slow);
            }
            current = null;
        }

        static void Count(SortedDictionary<string, int> d, string key) => d[key] = d.TryGetValue(key, out int v) ? v + 1 : 1;
    }
}
