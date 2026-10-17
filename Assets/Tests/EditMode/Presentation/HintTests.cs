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
        static World NewWorld()
        {
            var w = SimTestUtil.NewSim().World;
            w.Tutorial = TutorialStage.None;
            w.Progress.Wallet = 10000;
            HintRules.MarkSeen(w, HintId.Upgrade); // the rich test wallet could always afford one
            return w;
        }

        [Test]
        public void EnoughCoinsForAnUpgradePointsAtTheWallet()
        {
            var w = SimTestUtil.NewSim().World;
            w.Tutorial = TutorialStage.None;
            w.Progress.Wallet = 0;
            Assert.IsNull(HintRules.Next(w));
            w.Progress.Wallet = 10000;
            Assert.AreEqual(HintId.Upgrade, HintRules.Next(w));
            Assert.AreEqual(HintTarget.Wallet, HintRules.Target(HintId.Upgrade));
            w.Tutorial = TutorialStage.Purchase;
            Assert.IsNull(HintRules.Next(w), "not during the opening tutorial");
        }

        [Test]
        public void NothingIsDueAtTheStart()
        {
            Assert.IsNull(HintRules.Next(NewWorld()));
        }

        [Test]
        public void ASecondWeaponTeachesSwitchingAndAThirdTeachesTheShopSlots()
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
            Assert.IsNull(HintRules.Next(w));
            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.DoubleBarrel);
            Assert.AreEqual(HintId.Loadout, HintRules.Next(w));
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
            ShopSystem.Purchase(w, OfferKind.BuyItem, item: ItemId.PushPulse);
            Assert.AreEqual(HintId.ItemSwitch, HintRules.Next(w));
        }

        [Test]
        public void LowHealthPointsAtTheHealthBar()
        {
            var w = NewWorld();
            w.Player.Health = PlayerStats.MaxHealth(w) * 0.3f;
            Assert.AreEqual(HintId.Heal, HintRules.Next(w));
            Assert.AreEqual(HintTarget.Health, HintRules.Target(HintId.Heal));
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
