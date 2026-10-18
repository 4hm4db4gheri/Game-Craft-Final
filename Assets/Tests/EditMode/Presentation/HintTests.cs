using Hollow.Data;
using Hollow.Sim;
using Hollow.Tests.Sim;
using Hollow.UI;
using NUnit.Framework;

namespace Hollow.Tests.Presentation
{
    /// <summary>One-time contextual hints: when each appears, when it may close, and that a save remembers it.</summary>
    public class HintTests
    {
        static World NewWorld(bool companion = false)
        {
            var w = SimTestUtil.NewSim(companion: companion).World;
            w.Tutorial = TutorialStage.None;
            w.Progress.Wallet = 10000;
            return w;
        }

        [Test]
        public void NothingIsDueAtTheStart()
        {
            Assert.IsNull(HintRules.Next(NewWorld()));
        }

        [Test]
        public void ASecondWeaponTeachesSwitchingOnce()
        {
            var w = NewWorld();
            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg);
            Assert.AreEqual(HintId.WeaponSwitch, HintRules.Next(w));
            Assert.AreEqual(HintTarget.Weapon, HintRules.Target(HintId.WeaponSwitch));

            var start = HintRules.Capture(w);
            Assert.IsFalse(HintRules.Done(w, HintId.WeaponSwitch, start));
            w.Player.Loadout.ActiveSlot = 2;
            Assert.IsTrue(HintRules.Done(w, HintId.WeaponSwitch, start), "closes once the player switches");

            HintRules.MarkSeen(w, HintId.WeaponSwitch);
            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.DoubleBarrel);
            Assert.IsNull(HintRules.Next(w), "a third weapon brings no hint");
        }

        [Test]
        public void TheFirstItemTeachesFAndASecondTypeTeachesG()
        {
            var w = NewWorld();
            ShopSystem.Purchase(w, OfferKind.BuyItem, item: ItemId.Grenade);
            Assert.AreEqual(HintId.Item, HintRules.Next(w));
            Assert.AreEqual(HintTarget.Item, HintRules.Target(HintId.Item));
            HintRules.MarkSeen(w, HintId.Item);
            ShopSystem.Purchase(w, OfferKind.BuyItem, item: ItemId.Grenade);
            Assert.IsNull(HintRules.Next(w), "more of the same type is not a second type");
            w.Phase = 2;
            ShopSystem.Purchase(w, OfferKind.BuyItem, item: ItemId.Mine);
            Assert.AreEqual(HintId.ItemSwitch, HintRules.Next(w));
        }

        [Test]
        public void AHurtOperatorPointsAtItsBarAndLowPlayerHealthAloneDoesNot()
        {
            var w = NewWorld(companion: true);
            w.Player.Health = PlayerStats.MaxHealth(w) * 0.2f;
            Assert.IsNull(HintRules.Next(w), "the player's own health has no hint of its own");
            w.Companion.Health = w.Companion.MaxHealth * 0.3f;
            Assert.AreEqual(HintId.HealCompanion, HintRules.Next(w));
            Assert.AreEqual(HintTarget.Companion, HintRules.Target(HintId.HealCompanion));
        }

        [Test]
        public void ShownHintsStaySeenAfterASave()
        {
            var w = NewWorld();
            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg);
            HintRules.MarkSeen(w, HintId.WeaponSwitch);
            var restored = WorldSerializer.FromJson(WorldSerializer.ToJson(w), w.Profile, w.Layout);
            Assert.IsTrue(HintRules.Seen(restored, HintId.WeaponSwitch));
            Assert.IsNull(HintRules.Next(restored));
        }

        [Test]
        public void EveryHintHasAPersianText()
        {
            foreach (HintId id in System.Enum.GetValues(typeof(HintId)))
                Assert.IsTrue(Loc.Has("hint." + id), id.ToString());
        }
    }
}
