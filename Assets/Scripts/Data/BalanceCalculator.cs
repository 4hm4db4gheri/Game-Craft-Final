using System;
using System.Collections.Generic;

namespace Hollow.Data
{
    public sealed class WaveTotals
    {
        public int Phase;
        public int Wave;
        public bool IsTutorial;
        public List<EnemyCount> Composition;
        public int BaseCount;
        public int ChildCount;
        public int MaxTotal => BaseCount + ChildCount;
        public int ActiveCap;
        public int RangedCap;
        public float SpawnGap;
        public int[] Batches;
        public int Xp;
        public int KillCoins;
        public int BonusCoins;
    }

    public sealed class PhaseTotals
    {
        public int Phase;
        public int BaseRegular;
        public int MaxRegular;
        public int BossAndAdds;
        public int MaxPhase => MaxRegular + BossAndAdds;
        public int RegularXp;
        public int RegularKillCoins;
        public int WaveBonusCoins;
        public int BossPoolXp;
        public int BossPoolCoins;
        public int AddsXp;
        public int AddsCoins;
        public int BossOwnXp => BossPoolXp - AddsXp;
        public int BossOwnCoins => BossPoolCoins - AddsCoins;
        public int PhaseXp => RegularXp + BossPoolXp;
        public int PreBossCoins => RegularKillCoins + WaveBonusCoins;
        public int CoinsGenerated => PreBossCoins + BossPoolCoins;
        public int CumulativeXp;
        public int CumulativeChoices;
    }

    /// <summary>
    /// Calculated (theoretical) numbers derived from the profile: counts, budgets, XP choices, DPS, time to kill.
    /// These are design calculations, never observed results.
    /// </summary>
    public static class BalanceCalculator
    {
        public static List<WaveTotals> Waves(BalanceProfile p, TestProfile test = null)
        {
            var list = new List<WaveTotals>();
            foreach (var w in p.Waves)
            {
                bool scale = test != null && !w.IsTutorial;
                var comp = scale ? Rounding.ScaleCounts(w.Composition, test.CountMultiplier) : new List<EnemyCount>(w.Composition);
                var t = new WaveTotals
                {
                    Phase = w.Phase,
                    Wave = w.Wave,
                    IsTutorial = w.IsTutorial,
                    Composition = comp,
                    ActiveCap = scale ? Rounding.ScaleCap(w.ActiveCap, test.ActiveCapMultiplier) : w.ActiveCap,
                    RangedCap = scale ? Math.Min(Rounding.ScaleCap(w.RangedCap, test.ActiveCapMultiplier), Rounding.ScaleCap(w.ActiveCap, test.ActiveCapMultiplier)) : w.RangedCap,
                    SpawnGap = scale ? w.SpawnGap * test.SpawnGapMultiplier : w.SpawnGap,
                    BonusCoins = w.CompletionBonus,
                };
                foreach (var c in comp)
                {
                    var e = p.Enemy(c.Id);
                    t.BaseCount += c.Count;
                    t.ChildCount += c.Count * ChildrenPer(e);
                    t.Xp += c.Count * e.Xp;
                    t.KillCoins += c.Count * e.Coins;
                }
                t.Batches = w.IsTutorial ? new[] { t.BaseCount } : Rounding.SplitByShares(t.BaseCount, p.Spawning.BatchShares);
                list.Add(t);
            }
            return list;
        }

        public static List<PhaseTotals> Phases(BalanceProfile p, TestProfile test = null)
        {
            var waves = Waves(p, test);
            var result = new List<PhaseTotals>();
            int cumulativeXp = 0;
            foreach (var boss in p.Bosses)
            {
                var t = new PhaseTotals { Phase = boss.Phase };
                foreach (var w in waves)
                {
                    if (w.Phase != boss.Phase) continue;
                    t.BaseRegular += w.BaseCount;
                    t.MaxRegular += w.MaxTotal;
                    t.RegularXp += w.Xp;
                    t.RegularKillCoins += w.KillCoins;
                    t.WaveBonusCoins += w.BonusCoins;
                }
                t.BossAndAdds = 1;
                foreach (var group in BossAddGroups(boss))
                foreach (var c in group)
                {
                    var e = p.Enemy(c.Id);
                    t.BossAndAdds += c.Count * (1 + ChildrenPer(e));
                    t.AddsXp += c.Count * e.Xp;
                    t.AddsCoins += c.Count * e.Coins;
                }
                t.BossPoolXp = boss.RewardXp;
                t.BossPoolCoins = boss.RewardCoins;
                cumulativeXp += t.PhaseXp;
                t.CumulativeXp = cumulativeXp;
                t.CumulativeChoices = ChoicesAffordable(cumulativeXp, p.Xp);
                result.Add(t);
            }
            return result;
        }

        public static IEnumerable<List<EnemyCount>> BossAddGroups(BossData boss)
        {
            yield return boss.OpeningAdds;
            foreach (var th in boss.Thresholds) yield return th.Adds;
        }

        public static int ChildrenPer(EnemyData e) => e.HasChild ? e.ProduceMax : 0;

        /// <summary>XP needed for the n-th choice alone (n starts at 1): 40, 70, 100, …</summary>
        public static int ThresholdCost(int n, XpData xp) => xp.FirstThreshold + (n - 1) * xp.ThresholdIncrement;

        /// <summary>Total XP needed to have made n choices.</summary>
        public static long CumulativeCost(int n, XpData xp) => (long)n * xp.FirstThreshold + (long)xp.ThresholdIncrement * n * (n - 1) / 2;

        public static int ChoicesAffordable(long totalXp, XpData xp)
        {
            if (xp.FirstThreshold <= 0) return 0;
            int n = 0;
            while (CumulativeCost(n + 1, xp) <= totalXp) n++;
            return n;
        }

        /// <summary>Damage per second including reload downtime, before skills and armour.</summary>
        public static float SustainedDps(WeaponData w, WeaponLevel l)
        {
            if (l.Rate <= 0) return 0;
            if (w.Kind == WeaponKind.Melee) return l.Damage * l.Rate;
            float perShot = l.Damage * Math.Max(1, l.ProjectileCount);
            if (l.Magazine <= 0) return perShot * l.Rate;
            float cycle = l.Magazine / l.Rate + l.ReloadTime;
            return perShot * l.Magazine / cycle;
        }

        public static float TimeToKill(float health, float dps) => dps > 0 ? health / dps : float.PositiveInfinity;
    }
}
