using Hollow.Data;
using Hollow.Sim;
using NUnit.Framework;

namespace Hollow.Tests.Sim
{
    /// <summary>T26: the human donates the industrial rifle once after phase one (GDD v0.19 §5).</summary>
    public class GiftTests
    {
        [Test]
        public void EnteringPhaseTwoGrantsTheRifleOnceForFreeWithoutEquippingIt()
        {
            var sim = SimTestUtil.NewSim(WaveTests.RealArena(), companion: true);
            var w = sim.World;
            w.Progress.Wallet = 123;
            var primaryBefore = w.Player.Loadout.HasPrimary;
            PhaseSystem.EnterPhase(w, 2);
            var gift = w.Profile.Economy.GiftWeapon;
            Assert.AreEqual(WeaponId.IndustrialRifle, gift);
            Assert.IsTrue(w.Player.Loadout.Owns(gift), "granted");
            Assert.AreEqual(1, w.Player.Loadout.Get(gift).Level);
            Assert.AreEqual(123, w.Progress.Wallet, "no coins spent");
            Assert.AreEqual(primaryBefore, w.Player.Loadout.HasPrimary, "Equip or Store is the player's choice");
            Assert.IsTrue(w.Narrative.GiftReceived);

            int owned = w.Player.Loadout.Owned.Count;
            Assert.IsFalse(PhaseSystem.GiveGift(w, 2), "never twice (skip, load, retry)");
            Assert.AreEqual(owned, w.Player.Loadout.Owned.Count);
        }

        [Test]
        public void TheRetryCheckpointKeepsTheGiftAndOtherPhasesGiveNothing()
        {
            var sim = SimTestUtil.NewSim(WaveTests.RealArena(), companion: true);
            var w = sim.World;
            Assert.IsFalse(PhaseSystem.GiveGift(w, 1));
            Assert.IsFalse(PhaseSystem.GiveGift(w, 3));
            PhaseSystem.EnterPhase(w, 2);
            var restored = Checkpoints.Restore(w);
            Assert.IsTrue(restored.Player.Loadout.Owns(WeaponId.IndustrialRifle));
            Assert.IsTrue(restored.Narrative.GiftReceived);
        }
    }
}
