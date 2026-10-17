using Hollow.Data;
using NUnit.Framework;

namespace Hollow.Tests.Data
{
    public class BalanceServiceTests
    {
        static BalanceService NewService() => new(TestData.Baseline());

        [Test]
        public void DraftEditsDoNotTouchLiveUntilApply()
        {
            var s = NewService();
            s.Draft.Player.MoveSpeed = 7;
            Assert.AreEqual(5f, s.Live.Player.MoveSpeed);
            Assert.IsTrue(s.Apply());
            Assert.AreEqual(7f, s.Live.Player.MoveSpeed);
            Assert.AreEqual(TestData.Baseline().Revision + 1, s.Live.Revision);
        }

        [Test]
        public void AutoApplyMakesEditsLiveImmediately()
        {
            var s = NewService();
            BalanceProfile applied = null;
            s.Applied += p => applied = p;
            s.AutoApply = true;
            s.Draft.Enemy(EnemyId.A1_RunnerWolf).Health = 45;
            s.NotifyDraftEdited();
            Assert.AreEqual(45f, s.Live.Enemy(EnemyId.A1_RunnerWolf).Health);
            Assert.AreSame(s.Live, applied);
        }

        [Test]
        public void ErrorsBlockApply()
        {
            var s = NewService();
            s.Draft.Wave(1, 2).RangedCap = 99;
            Assert.IsFalse(s.Apply());
            Assert.AreEqual(3, s.Live.Wave(1, 2).RangedCap);
        }

        [Test]
        public void UndoRestoresThePreviousLiveProfile()
        {
            var s = NewService();
            s.Draft.Player.DashDistance = 4; s.Apply();
            s.Draft.Player.DashDistance = 5; s.Apply();
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(4f, s.Live.Player.DashDistance);
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(3f, s.Live.Player.DashDistance);
            Assert.IsFalse(s.Undo());
        }

        [Test]
        public void ResetGroupOnlyTouchesThatGroup()
        {
            var s = NewService();
            s.Draft.Player.MoveSpeed = 9;
            s.Draft.Economy.PlayerHealPrice = 1;
            s.ResetGroupToBaseline(PanelGroups.PlayerCompanion);
            Assert.AreEqual(5f, s.Draft.Player.MoveSpeed);
            Assert.AreEqual(1, s.Draft.Economy.PlayerHealPrice);
        }

        [Test]
        public void ImportedProfileWaitsForApply()
        {
            var s = NewService();
            var imported = TestData.Baseline();
            imported.Xp.FirstThreshold = 50;
            s.LoadIntoDraft(imported);
            Assert.AreEqual(40, s.Live.Xp.FirstThreshold);
            s.Apply();
            Assert.AreEqual(50, s.Live.Xp.FirstThreshold);
        }
    }
}
