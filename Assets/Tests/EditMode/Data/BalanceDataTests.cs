using System.Collections.Generic;
using System.Linq;
using Hollow.Data;
using NUnit.Framework;

namespace Hollow.Tests.Data
{
    public class BalanceDataTests
    {
        [Test]
        public void OlderProfileGetsOnlyTheNewOperatorRecoverySettings()
        {
            var baseline = TestData.Baseline();
            var older = BalanceJson.Clone(baseline);
            older.Revision = 2;
            older.Companion.RegenerationDelay = 0;
            older.Companion.RegenerationRate = 0;
            older.Companion.Phases[0].MaxHealth = 123;

            BalanceProfileStore.AddCompanionRegeneration(older, baseline);

            Assert.AreEqual(baseline.Companion.RegenerationDelay, older.Companion.RegenerationDelay);
            Assert.AreEqual(baseline.Companion.RegenerationRate, older.Companion.RegenerationRate);
            Assert.AreEqual(123, older.Companion.Phases[0].MaxHealth);
            Assert.AreEqual(baseline.Revision, older.Revision);
        }

        [Test]
        public void RevisionThreeKeepsRecoveryTuningAndGetsNewPlayerSettings()
        {
            var baseline = TestData.Baseline();
            var older = BalanceJson.Clone(baseline);
            older.Revision = 3;
            older.Companion.RegenerationRate = 7f;
            older.Player.RegenerationRate = 0;
            older.Skills.RemoveAll(s => s.Id == SkillId.DashMine);
            BalanceProfileStore.AddCompanionRegeneration(older, baseline);
            Assert.AreEqual(7f, older.Companion.RegenerationRate);
            Assert.AreEqual(baseline.Player.RegenerationRate, older.Player.RegenerationRate);
            Assert.IsNotNull(older.Skill(SkillId.DashMine));
        }

        [Test]
        public void BaselineHasNoValidationErrors()
        {
            var errors = BalanceValidator.Validate(TestData.Baseline()).Where(i => i.Severity == IssueSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void PlaceholderLevelsAreFlaggedAndNotSoldInRelease()
        {
            var p = TestData.Baseline();
            var issues = BalanceValidator.Validate(p);
            foreach (var w in p.Weapons)
            {
                Assert.IsTrue(w.Levels[0].Approved, $"{w.Id} L1 comes from the GDD");
                foreach (var l in w.Levels.Where(l => !l.Approved))
                {
                    Assert.IsTrue(issues.Any(i => i.Path == $"Weapons.{w.Id}.L{l.Level}" && i.Severity == IssueSeverity.Warning));
                    Assert.IsFalse(BalanceValidator.IsLevelPurchasable(p, w, l, devBuild: false));
                    Assert.IsTrue(BalanceValidator.IsLevelPurchasable(p, w, l, devBuild: true));
                }
            }
        }

        [Test]
        public void IncompleteLevelIsNeverPurchasable()
        {
            var p = TestData.Baseline();
            var smg = p.Weapon(WeaponId.LightSmg);
            smg.Levels[1].Magazine = 0;
            smg.Levels[1].Approved = true;
            Assert.IsFalse(BalanceValidator.IsLevelPurchasable(p, smg, smg.Levels[1], devBuild: true));
            Assert.IsTrue(BalanceValidator.Validate(p).Any(i => i.Path == "Weapons.LightSmg.L2" && i.Severity == IssueSeverity.Error));
        }

        [Test]
        public void RangedCapAboveTotalCapIsAnError()
        {
            var p = TestData.Baseline();
            p.Wave(1, 2).RangedCap = 11;
            Assert.IsTrue(BalanceValidator.Validate(p).Any(i => i.Path == "Waves.P1W2.RangedCap" && i.Severity == IssueSeverity.Error));
        }

        [Test]
        public void InvalidNumbersAreErrors()
        {
            var p = TestData.Baseline();
            p.Player.MoveSpeed = float.NaN;
            p.Enemy(EnemyId.A1_RunnerWolf).Damage = -1;
            var errors = BalanceValidator.Validate(p).Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Path).ToList();
            CollectionAssert.Contains(errors, "Profile.Player.MoveSpeed");
            Assert.IsTrue(errors.Any(e => e.EndsWith(".Damage")));
        }

        [Test]
        public void JsonRoundTripIsLossless()
        {
            var p = TestData.Baseline();
            var copy = BalanceJson.Clone(p);
            Assert.AreEqual(BalanceJson.ToJson(p), BalanceJson.ToJson(copy));
            Assert.AreEqual(BalanceJson.Hash(p), BalanceJson.Hash(copy));
        }

        [Test]
        public void EditingACloneDoesNotTouchTheOriginal()
        {
            var p = TestData.Baseline();
            var copy = BalanceJson.Clone(p);
            copy.Weapon(WeaponId.Pistol).Levels[0].Damage = 999;
            Assert.AreEqual(18f, p.Weapon(WeaponId.Pistol).Levels[0].Damage);
            Assert.AreNotEqual(BalanceJson.Hash(p), BalanceJson.Hash(copy));
        }

        [Test]
        public void WeaponCsvRoundTripIsLossless()
        {
            var p = TestData.Baseline();
            string csv = WeaponLevelCsv.Export(p);
            var target = TestData.Baseline();
            foreach (var w in target.Weapons) w.Levels.Clear();
            var errors = WeaponLevelCsv.Import(target, csv);
            Assert.IsEmpty(errors);
            Assert.AreEqual(BalanceJson.ToJson(p), BalanceJson.ToJson(target));
        }

        [Test]
        public void CsvWithABadRowChangesNothing()
        {
            var p = TestData.Baseline();
            string before = BalanceJson.ToJson(p);
            string csv = WeaponLevelCsv.Export(p) + "LightSmg,2,true,60,abc,9,1,8,30,1.2,10,1,28,1,0,0\n";
            var errors = WeaponLevelCsv.Import(p, csv);
            Assert.IsNotEmpty(errors);
            Assert.AreEqual(before, BalanceJson.ToJson(p));
        }

        [TestCase(6, 2, 2, 2)]
        [TestCase(32, 10, 9, 13)]
        [TestCase(7, 2, 2, 3)]
        public void BatchSplitPreservesTotal(int total, int a, int b, int c)
        {
            var batches = Rounding.SplitByShares(total, new List<float> { 0.3f, 0.3f, 0.4f });
            Assert.AreEqual(new[] { a, b, c }, batches);
            Assert.AreEqual(total, batches.Sum());
        }

        [Test]
        public void ScaledCountsHitTheRoundedTarget()
        {
            var counts = new List<EnemyCount> { new(EnemyId.A1_RunnerWolf, 30), new(EnemyId.A2_QuillShooter, 16), new(EnemyId.A7_MotherSpider, 6) };
            for (float m = 0.5f; m <= 1.5f; m += 0.05f)
            {
                var scaled = Rounding.ScaleCounts(counts, m);
                Assert.AreEqual((int)System.Math.Round(52 * (double)m, System.MidpointRounding.AwayFromZero), scaled.Sum(c => c.Count), $"multiplier {m}");
            }
        }
    }
}
