using System.Linq;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>T11 acceptance tests (GDD §8, §9, §11).</summary>
    public class ShopTests
    {
        static Simulation Rich(int coins = 10000, bool companion = false)
        {
            var layout = SimTestUtil.OpenArena();
            layout.ShopPosition = new Vector2(0, -1.5f);
            layout.CompanionStart = new Vector2(-2, 0);
            var sim = SimTestUtil.NewSim(layout, companion: companion);
            sim.World.Progress.Wallet = coins;
            return sim;
        }

        static ShopOffer Find(World w, OfferKind kind, WeaponId weapon = default, ItemId item = default) =>
            ShopSystem.Offers(w).First(o => o.Kind == kind && (kind == OfferKind.BuyItem ? o.Item == item : kind is OfferKind.HealPlayer or OfferKind.HealCompanion || o.Weapon == weapon));

        [Test]
        public void FailedPurchaseSpendsNothingAndChangesNothing()
        {
            var sim = Rich(coins: 79);
            var w = sim.World;
            Assert.IsFalse(ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg));
            Assert.AreEqual(79, w.Progress.Wallet);
            Assert.IsFalse(w.Player.Loadout.Owns(WeaponId.LightSmg));
            Assert.AreEqual(LockReason.NotEnoughCoins, Find(w, OfferKind.BuyWeapon, WeaponId.LightSmg).Lock);
        }

        [Test]
        public void SuccessfulPurchaseChargesExactlyOnceAndEquipsAsPrimary()
        {
            var sim = Rich(coins: 100);
            var w = sim.World;
            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg));
            Assert.AreEqual(20, w.Progress.Wallet);
            Assert.IsTrue(w.Player.Loadout.HasPrimary);
            Assert.AreEqual(WeaponId.LightSmg, w.Player.Loadout.SlotPrimary);
            Assert.IsFalse(ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg), "cannot buy an owned weapon again");
            Assert.AreEqual(20, w.Progress.Wallet);
        }

        [Test]
        public void OwnedWeaponsReEquipForFreeAndStayOwned()
        {
            var sim = Rich();
            var w = sim.World;
            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg);
            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.DoubleBarrel);
            Assert.AreEqual(WeaponId.DoubleBarrel, w.Player.Loadout.SlotPrimary, "newest purchase goes to the primary slot");
            int wallet = w.Progress.Wallet;
            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.EquipWeapon, WeaponId.LightSmg));
            Assert.AreEqual(wallet, w.Progress.Wallet);
            Assert.AreEqual(WeaponId.LightSmg, w.Player.Loadout.SlotPrimary);
            Assert.IsTrue(w.Player.Loadout.Owns(WeaponId.DoubleBarrel));
        }

        [Test]
        public void ThePistolSlotTakesAnyOwnedWeaponAndThePistolComesBack()
        {
            var sim = Rich();
            var w = sim.World;
            var L = w.Player.Loadout;
            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg);
            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.DoubleBarrel);
            int wallet = w.Progress.Wallet;

            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.EquipWeapon, WeaponId.LightSmg, slot: 1), "a bought weapon replaces the pistol");
            Assert.AreEqual(WeaponId.LightSmg, L.Slot1);
            Assert.AreEqual(WeaponId.DoubleBarrel, L.SlotPrimary);
            Assert.IsTrue(L.Owns(WeaponId.Pistol), "the pistol stays owned");
            Assert.AreEqual(wallet, w.Progress.Wallet, "free");

            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.EquipWeapon, WeaponId.Pistol, slot: 2), "the pistol can go to slot 2");
            Assert.AreEqual(WeaponId.Pistol, L.SlotPrimary);
            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.EquipWeapon, WeaponId.Pistol, slot: 1), "moving between slots swaps them");
            Assert.AreEqual(WeaponId.Pistol, L.Slot1);
            Assert.AreEqual(WeaponId.LightSmg, L.SlotPrimary);
            Assert.IsFalse(ShopSystem.Purchase(w, OfferKind.EquipWeapon, WeaponId.Pistol, slot: 1), "already there");
        }

        [Test]
        public void SlotOneIsNeverLeftEmptyAndSlotsSurviveASave()
        {
            var sim = Rich();
            var w = sim.World;
            Assert.IsFalse(ShopSystem.Offers(w).Any(o => o.Kind == OfferKind.EquipWeapon && o.Slot == 2),
                "with only the pistol there is nothing to swap it with");
            Assert.IsFalse(ShopSystem.Purchase(w, OfferKind.EquipWeapon, WeaponId.Pistol, slot: 2));
            Assert.AreEqual(WeaponId.Pistol, w.Player.Loadout.Slot1);

            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg);
            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.DoubleBarrel);
            ShopSystem.Purchase(w, OfferKind.EquipWeapon, WeaponId.LightSmg, slot: 1);
            w.Player.Loadout.ActiveSlot = 1;
            var restored = WorldSerializer.FromJson(WorldSerializer.ToJson(w), w.Profile, w.Layout);
            Assert.AreEqual(WeaponId.LightSmg, restored.Player.Loadout.Slot1);
            Assert.AreEqual(WeaponId.LightSmg, restored.Player.Loadout.ActiveId);
            StringAssert.Contains("\"SlotPistol\":\"LightSmg\"", WorldSerializer.ToJson(w), "older saves keep loading");
        }

        [Test]
        public void UndefinedUpgradesAreNotSoldInReleaseButAreInDev()
        {
            var sim = Rich();
            var w = sim.World;
            ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg);
            w.Profile.Weapon(WeaponId.LightSmg).Levels[1].Approved = false; // every v0.19 level is defined; simulate one that is not
            bool saved = ShopSystem.DevBuild;
            try
            {
                ShopSystem.DevBuild = false;
                Assert.AreEqual(LockReason.LevelNotDefined, Find(w, OfferKind.UpgradeWeapon, WeaponId.LightSmg).Lock);
                Assert.IsFalse(ShopSystem.Purchase(w, OfferKind.UpgradeWeapon, WeaponId.LightSmg));
                ShopSystem.DevBuild = true;
                Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.UpgradeWeapon, WeaponId.LightSmg));
                Assert.AreEqual(2, w.Player.Loadout.Get(WeaponId.LightSmg).Level);
                ShopSystem.Purchase(w, OfferKind.UpgradeWeapon, WeaponId.LightSmg);
                Assert.AreEqual(LockReason.MaxLevel, Find(w, OfferKind.UpgradeWeapon, WeaponId.LightSmg).Lock);
            }
            finally { ShopSystem.DevBuild = saved; }
        }

        [Test]
        public void ItemStockRefreshesOnlyPerEncounterNotOnReopening()
        {
            var sim = Rich();
            var w = sim.World;
            for (int i = 0; i < 3; i++) Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.BuyItem, item: ItemId.Grenade));
            Assert.AreEqual(3, w.Inventory.Count(ItemId.Grenade));
            Assert.AreEqual(LockReason.OutOfStock, Find(w, OfferKind.BuyItem, item: ItemId.Grenade).Lock);
            ShopSystem.SetOpen(w, true); ShopSystem.SetOpen(w, false); ShopSystem.SetOpen(w, true);
            Assert.AreEqual(LockReason.OutOfStock, Find(w, OfferKind.BuyItem, item: ItemId.Grenade).Lock, "reopening does not refresh");
            w.Inventory.Get(ItemId.Grenade).Count = 0;
            ShopSystem.RefreshStock(w);
            Assert.AreEqual(LockReason.None, Find(w, OfferKind.BuyItem, item: ItemId.Grenade).Lock, "new wave refreshes");
        }

        [Test]
        public void EveryServiceStationOpensTheSameShopWithSharedStock()
        {
            var layout = SimTestUtil.OpenArena();
            layout.ShopPositions = new System.Collections.Generic.List<Vector2> { new(0, -1.5f), new(10, 5) };
            var sim = SimTestUtil.NewSim(layout);
            var w = sim.World;
            w.Progress.Wallet = 1000;
            for (int i = 0; i < 3; i++) Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.BuyItem, item: ItemId.Grenade));

            w.Player.Position = w.Player.PrevPosition = new Vector2(10, 4);
            Assert.IsTrue(ShopSystem.InRange(w), "the second station is a shop too");
            sim.Step(new Hollow.Core.Input.PlayerCommand { InteractPressed = true });
            Assert.IsTrue(w.Shop.Open);
            Assert.AreEqual(LockReason.OutOfStock, Find(w, OfferKind.BuyItem, item: ItemId.Grenade).Lock, "a second station cannot refresh stock");
            Assert.AreEqual(1000 - 3 * w.Profile.Item(ItemId.Grenade).Price, w.Progress.Wallet, "one wallet");

            w.Player.Position = w.Player.PrevPosition = new Vector2(5, 2);
            Assert.IsFalse(ShopSystem.InRange(w));
        }

        [Test]
        public void CarryLimitBlocksBuying()
        {
            var sim = Rich();
            var w = sim.World;
            ShopSystem.Purchase(w, OfferKind.BuyItem, item: ItemId.PushPulse);
            ShopSystem.Purchase(w, OfferKind.BuyItem, item: ItemId.PushPulse);
            ShopSystem.RefreshStock(w);
            Assert.AreEqual(LockReason.CarryFull, Find(w, OfferKind.BuyItem, item: ItemId.PushPulse).Lock);
        }

        [Test]
        public void ItemsUnlockByPhase()
        {
            var w = Rich().World;
            Assert.AreEqual(LockReason.NotUnlocked, Find(w, OfferKind.BuyItem, item: ItemId.Mine).Lock);
            Assert.AreEqual(LockReason.NotUnlocked, Find(w, OfferKind.BuyWeapon, WeaponId.IndustrialRifle).Lock);
            w.Phase = 2;
            Assert.AreEqual(LockReason.None, Find(w, OfferKind.BuyItem, item: ItemId.Mine).Lock);
            Assert.AreEqual(LockReason.None, Find(w, OfferKind.BuyWeapon, WeaponId.IndustrialRifle).Lock);
        }

        [Test]
        public void HealingLimitsAndFullHealth()
        {
            var sim = Rich(companion: true);
            var w = sim.World;
            Assert.AreEqual(LockReason.FullHealth, Find(w, OfferKind.HealPlayer).Lock);
            w.Player.Health = 10;
            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.HealPlayer));
            Assert.AreEqual(50f, w.Player.Health);
            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.HealPlayer));
            Assert.AreEqual(LockReason.NoUsesLeft, Find(w, OfferKind.HealPlayer).Lock, "two per encounter");

            w.Companion.Health = 50;
            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.HealCompanion));
            Assert.AreEqual(90f, w.Companion.Health);
            Assert.AreEqual(LockReason.NoUsesLeft, Find(w, OfferKind.HealCompanion).Lock, "one per encounter");
        }

        [Test]
        public void TutorialSellsOnlyTheSmgAtEightUntilBought()
        {
            var sim = Rich(coins: 8);
            var w = sim.World;
            w.Tutorial = TutorialStage.Purchase;
            var smg = Find(w, OfferKind.BuyWeapon, WeaponId.LightSmg);
            Assert.AreEqual(8, smg.Price);
            Assert.AreEqual(LockReason.None, smg.Lock);
            Assert.AreEqual(LockReason.TutorialPurchaseFirst, Find(w, OfferKind.BuyWeapon, WeaponId.DoubleBarrel).Lock);
            Assert.AreEqual(LockReason.TutorialPurchaseFirst, Find(w, OfferKind.BuyItem, item: ItemId.Grenade).Lock);
            Assert.IsTrue(ShopSystem.Purchase(w, OfferKind.BuyWeapon, WeaponId.LightSmg));
            Assert.AreEqual(0, w.Progress.Wallet);
            Assert.AreEqual(TutorialStage.SecondGroup, w.Tutorial);
            Assert.AreEqual(LockReason.NotEnoughCoins, Find(w, OfferKind.BuyItem, item: ItemId.Grenade).Lock, "gate lifted");
        }

        [Test]
        public void PriceEditsApplyAtTheNextOpening()
        {
            var sim = Rich();
            var w = sim.World;
            ShopSystem.SetOpen(w, true);
            w.Profile.Item(ItemId.Grenade).Price = 99;
            Assert.AreEqual(20, Find(w, OfferKind.BuyItem, item: ItemId.Grenade).Price, "open shop keeps its prices");
            ShopSystem.SetOpen(w, false);
            ShopSystem.SetOpen(w, true);
            Assert.AreEqual(99, Find(w, OfferKind.BuyItem, item: ItemId.Grenade).Price);
        }

        [Test]
        public void EOpensInRangeAndDashOrLeavingCloses()
        {
            var sim = Rich();
            var w = sim.World;
            sim.Step(new PlayerCommand { InteractPressed = true });
            Assert.IsTrue(w.Shop.Open, "in range");
            sim.Step(new PlayerCommand { DashPressed = true, Move = Vector2.up });
            Assert.IsFalse(w.Shop.Open, "dash closes");
            sim.Run(40, PlayerCommand.None);
            sim.Step(new PlayerCommand { InteractPressed = true });
            Assert.IsFalse(w.Shop.Open, "out of range after the dash");
        }

        [Test]
        public void ShoppingNeverPausesTheWorld()
        {
            var sim = Rich();
            var w = sim.World;
            ShopSystem.SetOpen(w, true);
            long tick = w.Clock.ActiveTicks;
            sim.Run(60, SimTestUtil.Move(Vector2.right * 0.1f));
            Assert.AreEqual(tick + 60, w.Clock.ActiveTicks, "active time keeps running while shopping");
        }
    }
}
