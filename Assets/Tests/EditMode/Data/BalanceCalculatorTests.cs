using System.Linq;
using Hollow.Data;
using NUnit.Framework;

namespace Hollow.Tests.Data
{
    /// <summary>T01 acceptance: the calculator reproduces every total stated in the GDD.</summary>
    public class BalanceCalculatorTests
    {
        [TestCase(1, 226, 238, 31, 269)]
        [TestCase(2, 322, 340, 39, 379)]
        [TestCase(3, 446, 454, 43, 497)]
        public void PhaseEnemyTotalsMatchGdd(int phase, int baseRegular, int maxRegular, int bossAndAdds, int maxPhase)
        {
            var t = BalanceCalculator.Phases(TestData.Baseline()).Single(x => x.Phase == phase);
            Assert.AreEqual(baseRegular, t.BaseRegular, "base regular");
            Assert.AreEqual(maxRegular, t.MaxRegular, "max regular");
            Assert.AreEqual(bossAndAdds, t.BossAndAdds, "boss and adds");
            Assert.AreEqual(maxPhase, t.MaxPhase, "max phase");
        }

        [Test]
        public void GrandTotalsMatchGdd()
        {
            var phases = BalanceCalculator.Phases(TestData.Baseline());
            Assert.AreEqual(994, phases.Sum(p => p.BaseRegular));
            Assert.AreEqual(1032, phases.Sum(p => p.MaxRegular));
            Assert.AreEqual(113, phases.Sum(p => p.BossAndAdds));
            Assert.AreEqual(1145, phases.Sum(p => p.MaxPhase));
        }

        [TestCase(1, 1116, 804, 984, 7)]
        [TestCase(2, 1990, 1534, 1858, 13)]
        [TestCase(3, 3246, 2614, 3100, 19)]
        public void XpAndCoinBudgetsMatchGdd(int phase, int xp, int preBossCoins, int coinsGenerated, int cumulativeChoices)
        {
            var t = BalanceCalculator.Phases(TestData.Baseline()).Single(x => x.Phase == phase);
            Assert.AreEqual(xp, t.PhaseXp, "phase XP");
            Assert.AreEqual(preBossCoins, t.PreBossCoins, "pre-boss coin budget");
            Assert.AreEqual(coinsGenerated, t.CoinsGenerated, "coins generated");
            Assert.AreEqual(cumulativeChoices, t.CumulativeChoices, "cumulative XP choices");
        }

        [TestCase(1, 100, 100)]
        [TestCase(2, 180, 180)]
        [TestCase(3, 80, 260)] // v0.19: human boss pool 356 XP, adds 276 (baton guards give 6)
        public void BossOwnShareIsPoolMinusAdds(int phase, int xp, int coins)
        {
            var t = BalanceCalculator.Phases(TestData.Baseline()).Single(x => x.Phase == phase);
            Assert.AreEqual(xp, t.BossOwnXp);
            Assert.AreEqual(coins, t.BossOwnCoins);
        }

        [Test]
        public void EveryWaveMatchesTheGddMaxTotal()
        {
            var p = TestData.Baseline();
            var totals = BalanceCalculator.Waves(p);
            for (int i = 0; i < p.Waves.Count; i++)
                Assert.AreEqual(p.Waves[i].GddMaxTotal, totals[i].MaxTotal, $"wave {p.Waves[i].Phase}-{p.Waves[i].Wave}");
        }

        [Test]
        public void ThresholdSequenceIs40Plus30()
        {
            var xp = TestData.Baseline().Xp;
            Assert.AreEqual(new[] { 40, 70, 100, 130, 160 }, Enumerable.Range(1, 5).Select(n => BalanceCalculator.ThresholdCost(n, xp)).ToArray());
            Assert.AreEqual(0, BalanceCalculator.ChoicesAffordable(39, xp));
            Assert.AreEqual(1, BalanceCalculator.ChoicesAffordable(40, xp));
            Assert.AreEqual(2, BalanceCalculator.ChoicesAffordable(110, xp));
        }

        [TestCase("Quiet")]
        [TestCase("Current baseline")]
        [TestCase("Intense")]
        public void BatchesPreserveExactTotalsUnderEveryTestProfile(string profileName)
        {
            var p = TestData.Baseline();
            var test = p.TestProfiles.Single(t => t.Name == profileName);
            foreach (var w in BalanceCalculator.Waves(p, test))
            {
                Assert.AreEqual(w.BaseCount, w.Batches.Sum(), $"batches of {w.Phase}-{w.Wave}");
                Assert.LessOrEqual(w.RangedCap, w.ActiveCap);
            }
        }

        [TestCase("Quiet")]
        [TestCase("Intense")]
        public void TutorialIsExemptFromTestProfiles(string profileName)
        {
            var p = TestData.Baseline();
            var tutorial = BalanceCalculator.Waves(p, p.TestProfiles.Single(t => t.Name == profileName)).Single(w => w.IsTutorial);
            Assert.AreEqual(6, tutorial.BaseCount);
            Assert.AreEqual(3, tutorial.ActiveCap);
            Assert.AreEqual(1.5f, tutorial.SpawnGap, 1e-6f);
        }

        [Test]
        public void ScaledBudgetsAreRecomputedFromRoundedCounts()
        {
            var p = TestData.Baseline();
            var intense = p.TestProfiles.Single(t => t.Name == "Intense");
            foreach (var w in BalanceCalculator.Waves(p, intense))
            {
                int expectedXp = w.Composition.Sum(c => c.Count * p.Enemy(c.Id).Xp);
                Assert.AreEqual(expectedXp, w.Xp);
                if (!w.IsTutorial)
                {
                    int original = p.Wave(w.Phase, w.Wave).Composition.Sum(c => c.Count);
                    Assert.AreEqual((int)System.Math.Round(original * 1.25, System.MidpointRounding.AwayFromZero), w.BaseCount);
                }
            }
        }

        [Test]
        public void SustainedDpsAccountsForReload()
        {
            var p = TestData.Baseline();
            var pistol = p.Weapon(WeaponId.Pistol);
            // 8 shots at 3/s = 2.667 s + 0.9 s reload; 18 × 8 / 3.567 s
            Assert.AreEqual(18f * 8f / (8f / 3f + 0.9f), BalanceCalculator.SustainedDps(pistol, pistol.Levels[0]), 1e-3f);
            var baton = p.Weapon(WeaponId.Baton);
            Assert.AreEqual(50f * 2.2f, BalanceCalculator.SustainedDps(baton, baton.Levels[0]), 1e-3f);
        }
    }
}
