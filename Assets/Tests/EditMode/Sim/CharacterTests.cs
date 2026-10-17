using Hollow.Data;
using Hollow.Sim;
using Hollow.Tests.Data;
using NUnit.Framework;

namespace Hollow.Tests.Sim
{
    /// <summary>T25: the chosen AI starts with one rank of its skill (GDD v0.19 §3).</summary>
    public class CharacterTests
    {
        static World NewWorld(AiId ai) => Simulation.NewWorld(TestData.Baseline(), SimTestUtil.OpenArena(), 1, ai);

        [TestCase(AiId.Gpt, SkillId.ReloadMultiplier, 0.92f)]
        [TestCase(AiId.Claude, SkillId.MagnetRadius, 1.3f)]
        [TestCase(AiId.Grok, SkillId.DashDamage, 60f)]
        [TestCase(AiId.DeepSeek, SkillId.MoveSpeed, 5.3f)]
        public void EachAiStartsWithRankOneOfItsSkill(AiId ai, SkillId skill, float startValue)
        {
            var w = NewWorld(ai);
            Assert.AreEqual(ai, w.Player.Ai);
            foreach (SkillId id in System.Enum.GetValues(typeof(SkillId)))
                Assert.AreEqual(id == skill ? 1 : 0, w.Player.SkillRank(id), id.ToString());
            Assert.AreEqual(startValue, XpSystem.Preview(w, skill).before, 1e-4f, "the start value is the rank-one value");
            Assert.AreEqual(2, XpSystem.MaxRank(w, skill) - w.Player.SkillRank(skill), "ranks two and three are left to earn");
            Assert.AreEqual(PlayerStats.MaxHealth(w), w.Player.Health);
            Assert.AreEqual(0, w.Progress.TotalXp);
            Assert.AreEqual(0, w.Progress.Wallet);
        }

        [Test]
        public void TheStartingRankAppliesOnceAndCountsTowardTheCap()
        {
            var w = NewWorld(AiId.DeepSeek);
            Assert.AreEqual(5.3f, PlayerStats.MoveSpeed(w), 1e-4f, "set once, not added to the base");
            w.Player.SkillRanks[(int)SkillId.MoveSpeed] = XpSystem.MaxRank(w, SkillId.MoveSpeed);
            Assert.AreEqual(5.9f, PlayerStats.MoveSpeed(w), 1e-4f, "rank three is the cap");
        }

        [Test]
        public void TheAiIsSavedAndNeverChosenAgainOnLoad()
        {
            var w = NewWorld(AiId.Claude);
            var loaded = WorldSerializer.FromJson(WorldSerializer.ToJson(w), w.Profile, w.Layout);
            Assert.AreEqual(AiId.Claude, loaded.Player.Ai);
            Assert.AreEqual(1, loaded.Player.SkillRank(SkillId.MagnetRadius));
            Assert.AreEqual(AiId.Claude, SaveFile.Create(w, 0).Summary.Ai);
        }

        [Test]
        public void EveryAiHasDataAndAPersianName()
        {
            var p = TestData.Baseline();
            foreach (AiId ai in System.Enum.GetValues(typeof(AiId)))
            {
                Assert.IsTrue(Hollow.UI.Loc.Has("ai." + ai), ai.ToString());
                if (ai != AiId.None) Assert.IsNotNull(p.Character(ai), ai.ToString());
            }
        }
    }
}
