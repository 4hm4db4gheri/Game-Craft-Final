using System.Linq;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using Hollow.Tests.Data;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>T08 acceptance tests (GDD §9).</summary>
    public class XpAndCoinTests
    {
        static void Gain(Simulation sim, int xp)
        {
            Rewards.GrantXp(sim.World, xp, Vector2.zero);
            XpSystem.Step(sim.World);
        }

        [Test]
        public void ThresholdsAre40Then70ThenPlus30WithOverflowKept()
        {
            var sim = SimTestUtil.NewSim();
            var pr = sim.World.Progress;
            Gain(sim, 39);
            Assert.IsNull(pr.Pending);
            Gain(sim, 6);
            Assert.IsNotNull(pr.Pending, "first card at 40");
            Assert.AreEqual(5, pr.XpTowardNext, "overflow kept");
            XpSystem.Choose(sim.World, pr.Pending.Options[0]);
            Gain(sim, 64);
            Assert.IsNull(pr.Pending, "69 < 70");
            Gain(sim, 1);
            Assert.IsNotNull(pr.Pending, "second card at 70");
            Assert.AreEqual(0, pr.XpTowardNext);
            XpSystem.Choose(sim.World, pr.Pending.Options[0]);
            Assert.AreEqual(100, XpSystem.NextThreshold(sim.World), "third card costs 100");
        }

        [Test]
        public void CrossingSeveralThresholdsGivesConsecutiveChoices()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            Gain(sim, 40 + 70 + 100 + 7);
            int cards = 0;
            while (w.Progress.Pending != null && cards < 10)
            {
                XpSystem.Choose(w, w.Progress.Pending.Options[0]);
                cards++;
            }
            Assert.AreEqual(3, cards);
            Assert.AreEqual(7, w.Progress.XpTowardNext);
            Assert.AreEqual(217, w.Progress.TotalXp);
            Assert.AreEqual(0, w.Progress.Wallet, "XP is never currency");
        }

        [Test]
        public void CardsHaveNoDuplicatesAndNoMaxedSkills()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var sim = SimTestUtil.NewSim(seed: seed);
                var w = sim.World;
                w.Player.SkillRanks[(int)SkillId.MoveSpeed] = 3;
                w.Player.SkillRanks[(int)SkillId.DashDamage] = 3;
                Gain(sim, 40);
                var options = w.Progress.Pending.Options;
                Assert.AreEqual(3, options.Count);
                Assert.AreEqual(3, options.Distinct().Count(), "no duplicates");
                CollectionAssert.DoesNotContain(options, SkillId.MoveSpeed);
                CollectionAssert.DoesNotContain(options, SkillId.DashDamage);
            }
        }

        [Test]
        public void FewerOptionsWhenFewerSkillsRemain()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            foreach (var s in w.Profile.Skills) w.Player.SkillRanks[(int)s.Id] = s.Ranks.Count;
            w.Player.SkillRanks[(int)SkillId.MagnetRadius] = 2;
            w.Player.SkillRanks[(int)SkillId.MaxHealth] = 0;
            Gain(sim, 40);
            CollectionAssert.AreEquivalent(new[] { SkillId.MagnetRadius, SkillId.MaxHealth }, w.Progress.Pending.Options);
        }

        [Test]
        public void OnceEverySkillIsMaxedXpIsOnlyAStatistic()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            foreach (var s in w.Profile.Skills) w.Player.SkillRanks[(int)s.Id] = s.Ranks.Count;
            Gain(sim, 5000);
            Assert.IsNull(w.Progress.Pending);
            Assert.AreEqual(5000, w.Progress.TotalXp);
        }

        [Test]
        public void HealthRankAddsTwentyToMaxAndCurrentWithoutFullHeal()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            w.Player.Health = 50;
            w.Progress.Pending = new PendingChoice { Options = { SkillId.MaxHealth } };
            Assert.IsTrue(XpSystem.Choose(w, SkillId.MaxHealth));
            Assert.AreEqual(120f, PlayerStats.MaxHealth(w));
            Assert.AreEqual(70f, w.Player.Health);
        }

        [Test]
        public void RanksAreRelativeToBaselineNotCompounded()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            w.Player.SkillRanks[(int)SkillId.WeaponDamage] = 2;
            Assert.AreEqual(1.16f, PlayerStats.WeaponDamageMultiplier(w), 1e-6f);
            var (before, after) = XpSystem.Preview(w, SkillId.WeaponDamage);
            Assert.AreEqual(1.16f, before, 1e-6f);
            Assert.AreEqual(1.24f, after, 1e-6f);
            var (mb, ma) = XpSystem.Preview(w, SkillId.MoveSpeed);
            Assert.AreEqual(5f, mb); Assert.AreEqual(5.6f, ma, 1e-6f);
        }

        [Test]
        public void ChoosingAnOptionNotOnTheCardIsRejected()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            w.Progress.Pending = new PendingChoice { Options = { SkillId.MoveSpeed } };
            Assert.IsFalse(XpSystem.Choose(w, SkillId.DashDamage));
            Assert.IsNotNull(w.Progress.Pending);
        }

        [Test]
        public void TutorialGateHoldsTheCard()
        {
            var sim = SimTestUtil.NewSim();
            sim.World.Progress.ChoicesBlocked = true;
            Gain(sim, 80);
            Assert.IsNull(sim.World.Progress.Pending);
            sim.World.Progress.ChoicesBlocked = false;
            XpSystem.Step(sim.World);
            Assert.IsNotNull(sim.World.Progress.Pending);
        }

        // ---------------------------------------------------------------- coins

        static CoinState DropOne(Simulation sim, Vector2 at, bool neverExpires = false)
        {
            Rewards.DropCoins(sim.World, at, 1, neverExpires);
            return sim.World.Coins.Last();
        }

        [Test]
        public void CoinsExpireAfterLifetimeInActiveSeconds()
        {
            var sim = SimTestUtil.NewSim();
            var coin = DropOne(sim, new Vector2(10, 10));
            sim.Run(60 * 59, PlayerCommand.None);
            Assert.IsTrue(coin.Alive);
            Assert.IsTrue(CoinSystem.RemainingLifetime(sim.World, coin) <= sim.World.Profile.Economy.CoinBlinkTime, "blinking in the last 5 s");
            sim.Run(61, PlayerCommand.None);
            Assert.IsFalse(coin.Alive);
        }

        [Test]
        public void SafeIntervalsFreezeCoinLifetime()
        {
            var sim = SimTestUtil.NewSim();
            var coin = DropOne(sim, new Vector2(10, 10));
            sim.World.SafeInterval = true;
            sim.Run(60 * 120, PlayerCommand.None);
            Assert.IsTrue(coin.Alive);
            Assert.AreEqual(60f, CoinSystem.RemainingLifetime(sim.World, coin), 1e-3f);
        }

        [Test]
        public void TutorialCoinsAndDisabledLifetimeNeverExpire()
        {
            var sim = SimTestUtil.NewSim();
            var tutorial = DropOne(sim, new Vector2(10, 10), neverExpires: true);
            sim.Run(60 * 70, PlayerCommand.None);
            Assert.IsTrue(tutorial.Alive);

            var profile = TestData.Baseline();
            profile.Economy.CoinLifetimeEnabled = false;
            var sim2 = SimTestUtil.NewSim(profile: profile);
            var c = DropOne(sim2, new Vector2(10, 10));
            sim2.Run(60 * 70, PlayerCommand.None);
            Assert.IsTrue(c.Alive);
        }

        [Test]
        public void BiggerMagnetPullsFromFurtherButNeverRaisesValue()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            DropOne(sim, new Vector2(1.8f, 0));
            sim.Run(60, PlayerCommand.None);
            Assert.AreEqual(0, w.Progress.Wallet, "outside the 1-unit magnet");
            w.Player.SkillRanks[(int)SkillId.MagnetRadius] = 3; // radius 2
            sim.Run(60, PlayerCommand.None);
            Assert.AreEqual(1, w.Progress.Wallet, "pulled in, worth exactly one coin");
        }
    }
}
