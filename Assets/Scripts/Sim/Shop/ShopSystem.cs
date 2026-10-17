using System.Collections.Generic;
using Hollow.Core.Input;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    public enum OfferKind { BuyWeapon, EquipWeapon, UpgradeWeapon, BuyItem, HealPlayer, HealCompanion }

    public enum LockReason
    {
        None, NotEnoughCoins, OutOfStock, CarryFull, MaxLevel, LevelNotDefined, FullHealth, NoUsesLeft,
        TutorialPurchaseFirst, NotUnlocked, NoCompanion, AlreadyEquipped
    }

    public struct ShopOffer
    {
        public OfferKind Kind;
        public WeaponId Weapon;
        public ItemId Item;
        public int Price;
        public LockReason Lock;
        /// <summary>Phase page the offer belongs to (unlock phase; pistol on page 1).</summary>
        public int Page;
        public int Level;
        public int StockLeft;
        public bool Enabled => Lock == LockReason.None;
    }

    public enum TutorialStage { None, FirstGroup, Purchase, SecondGroup }

    /// <summary>Shop state that must survive a save (open flag, price snapshot, per-encounter stock).</summary>
    public sealed class ShopState
    {
        public bool Open;
        /// <summary>Prices captured when the shop opened, by offer key: balance edits apply at the next opening (§13).</summary>
        public List<long> SnapshotKeys = new();
        public List<int> SnapshotPrices = new();
        public List<int> ItemStockLeft = new();
        public int PlayerHealsUsed;
        public int CompanionHealsUsed;
        public int PurchaseCount;
        public long FirstPurchaseTick = -1;
    }

    /// <summary>
    /// The live world-station shop (§11): never pauses combat, one-click purchases with no confirmation,
    /// failed purchases spend nothing, stock refreshes once per wave or boss preparation only.
    /// </summary>
    public static class ShopSystem
    {
        public static bool DevBuild = Application.isEditor || Debug.isDebugBuild;

        public static void Step(World w, in PlayerCommand cmd)
        {
            var s = w.Shop;
            bool inRange = InRange(w);
            if (cmd.InteractPressed && (inRange || s.Open)) SetOpen(w, !s.Open);
            if (s.Open && (!inRange || w.Player.Dashing || w.Player.Dead)) SetOpen(w, false);
        }

        /// <summary>Near any service station (v0.19 §13): each one opens the same shop.</summary>
        public static bool InRange(World w) =>
            Vector2.Distance(w.Player.Position, w.Layout.NearestShop(w.Player.Position)) <= w.Profile.Player.ShopInteractRange;

        public static void SetOpen(World w, bool open)
        {
            var s = w.Shop;
            if (s.Open == open) return;
            s.Open = open;
            if (open) TakeSnapshot(w);
        }

        /// <summary>New wave or boss preparation: restore item stock and healing uses (never on reopen or load).</summary>
        public static void RefreshStock(World w)
        {
            var s = w.Shop;
            s.ItemStockLeft.Clear();
            foreach (var item in w.Profile.Items) s.ItemStockLeft.Add(item.StockPerEncounter);
            s.PlayerHealsUsed = 0;
            s.CompanionHealsUsed = 0;
        }

        // ---------------------------------------------------------------- offers

        static readonly List<ShopOffer> Buffer = new(32);

        /// <summary>All offers with their prices (from the snapshot while open) and lock reasons.</summary>
        public static List<ShopOffer> Offers(World w)
        {
            Buffer.Clear();
            var p = w.Profile;
            var L = w.Player.Loadout;
            bool tutorialGate = w.Tutorial == TutorialStage.Purchase;

            foreach (var wd in p.Weapons)
            {
                int page = Mathf.Max(1, wd.UnlockPhase);
                bool unlocked = wd.UnlockPhase <= w.Phase;
                var owned = L.Get(wd.Id);
                if (owned == null)
                {
                    bool isTutorialWeapon = tutorialGate && wd.Id == p.Economy.TutorialWeapon;
                    int price = isTutorialWeapon ? p.Economy.TutorialWeaponPrice : wd.Levels[0].Cost;
                    var lockReason = !unlocked ? LockReason.NotUnlocked
                        : tutorialGate && !isTutorialWeapon ? LockReason.TutorialPurchaseFirst
                        : LockReason.None;
                    Buffer.Add(Finish(w, new ShopOffer { Kind = OfferKind.BuyWeapon, Weapon = wd.Id, Price = price, Lock = lockReason, Page = page, Level = 1 }));
                    continue;
                }

                if (wd.Id != L.SlotPistol)
                {
                    bool equipped = L.HasPrimary && L.SlotPrimary == wd.Id;
                    Buffer.Add(new ShopOffer { Kind = OfferKind.EquipWeapon, Weapon = wd.Id, Price = 0, Page = page, Level = owned.Level,
                        Lock = equipped ? LockReason.AlreadyEquipped : tutorialGate ? LockReason.TutorialPurchaseFirst : LockReason.None });
                }

                int next = owned.Level + 1;
                if (next > wd.Levels.Count)
                {
                    Buffer.Add(new ShopOffer { Kind = OfferKind.UpgradeWeapon, Weapon = wd.Id, Page = page, Level = owned.Level, Lock = LockReason.MaxLevel });
                }
                else
                {
                    var lvl = wd.Levels[next - 1];
                    var lockReason = !BalanceValidator.IsLevelPurchasable(p, wd, lvl, DevBuild) ? LockReason.LevelNotDefined
                        : tutorialGate ? LockReason.TutorialPurchaseFirst : LockReason.None;
                    Buffer.Add(Finish(w, new ShopOffer { Kind = OfferKind.UpgradeWeapon, Weapon = wd.Id, Price = lvl.Cost, Page = page, Level = next, Lock = lockReason }));
                }
            }

            for (int i = 0; i < p.Items.Count; i++)
            {
                var item = p.Items[i];
                int stock = i < w.Shop.ItemStockLeft.Count ? w.Shop.ItemStockLeft[i] : item.StockPerEncounter;
                var lockReason = item.UnlockPhase > w.Phase ? LockReason.NotUnlocked
                    : tutorialGate ? LockReason.TutorialPurchaseFirst
                    : stock <= 0 ? LockReason.OutOfStock
                    : w.Inventory.Count(item.Id) >= item.Carry ? LockReason.CarryFull
                    : LockReason.None;
                Buffer.Add(Finish(w, new ShopOffer { Kind = OfferKind.BuyItem, Item = item.Id, Price = item.Price, Page = Mathf.Max(1, item.UnlockPhase), StockLeft = stock, Lock = lockReason }));
            }

            var eco = p.Economy;
            var pl = w.Player;
            Buffer.Add(Finish(w, new ShopOffer
            {
                Kind = OfferKind.HealPlayer, Price = eco.PlayerHealPrice, Page = 0,
                StockLeft = eco.PlayerHealsPerEncounter - w.Shop.PlayerHealsUsed,
                Lock = tutorialGate ? LockReason.TutorialPurchaseFirst
                    : pl.Health >= PlayerStats.MaxHealth(w) ? LockReason.FullHealth
                    : w.Shop.PlayerHealsUsed >= eco.PlayerHealsPerEncounter ? LockReason.NoUsesLeft : LockReason.None,
            }));
            var c = w.Companion;
            Buffer.Add(Finish(w, new ShopOffer
            {
                Kind = OfferKind.HealCompanion, Price = eco.CompanionHealPrice, Page = 0,
                StockLeft = eco.CompanionHealsPerEncounter - w.Shop.CompanionHealsUsed,
                Lock = !CompanionSystem.Active(w) ? LockReason.NoCompanion
                    : tutorialGate ? LockReason.TutorialPurchaseFirst
                    : c.Health >= c.MaxHealth ? LockReason.FullHealth
                    : w.Shop.CompanionHealsUsed >= eco.CompanionHealsPerEncounter ? LockReason.NoUsesLeft : LockReason.None,
            }));
            return Buffer;
        }

        /// <summary>Applies the price snapshot (while open) and the wallet check.</summary>
        static ShopOffer Finish(World w, ShopOffer o)
        {
            if (w.Shop.Open)
            {
                int idx = w.Shop.SnapshotKeys.IndexOf(Key(o));
                if (idx >= 0) o.Price = w.Shop.SnapshotPrices[idx];
            }
            if (o.Lock == LockReason.None && w.Progress.Wallet < o.Price) o.Lock = LockReason.NotEnoughCoins;
            return o;
        }

        public static long Key(in ShopOffer o) =>
            (long)o.Kind * 1_000_000L + (long)o.Weapon * 10_000L + (long)o.Item * 100L + o.Level;

        static void TakeSnapshot(World w)
        {
            var s = w.Shop;
            s.Open = false; // read live prices
            s.SnapshotKeys.Clear();
            s.SnapshotPrices.Clear();
            foreach (var o in Offers(w)) { s.SnapshotKeys.Add(Key(o)); s.SnapshotPrices.Add(o.Price); }
            s.Open = true;
        }
        // ---------------------------------------------------------------- purchase

        /// <summary>Atomic purchase: either everything happens or nothing does (§11).</summary>
        public static bool Purchase(World w, OfferKind kind, WeaponId weapon = default, ItemId item = default)
        {
            var offers = Offers(w);
            int index = offers.FindIndex(o => o.Kind == kind && Matches(o, weapon, item));
            if (index < 0) return false;
            var offer = offers[index];
            if (!offer.Enabled) return false;

            w.Progress.Wallet -= offer.Price;
            switch (kind)
            {
                case OfferKind.BuyWeapon:
                    WeaponSystem.Give(w, weapon);
                    if (w.Tutorial == TutorialStage.Purchase && weapon == w.Profile.Economy.TutorialWeapon) w.Tutorial = TutorialStage.SecondGroup;
                    break;
                case OfferKind.EquipWeapon:
                    WeaponSystem.EquipPrimary(w, weapon);
                    break;
                case OfferKind.UpgradeWeapon:
                    WeaponSystem.SetLevel(w, weapon, offer.Level);
                    break;
                case OfferKind.BuyItem:
                    int i = w.Profile.Items.FindIndex(x => x.Id == item);
                    w.Shop.ItemStockLeft[i]--;
                    w.Inventory.Add(item, 1);
                    break;
                case OfferKind.HealPlayer:
                    var pl = w.Player;
                    pl.Health = Mathf.Min(PlayerStats.MaxHealth(w), pl.Health + w.Profile.Economy.PlayerHealAmount);
                    w.Shop.PlayerHealsUsed++;
                    break;
                case OfferKind.HealCompanion:
                    CompanionSystem.Heal(w, w.Profile.Economy.CompanionHealAmount);
                    w.Shop.CompanionHealsUsed++;
                    break;
            }
            w.Shop.PurchaseCount++;
            if (w.Shop.FirstPurchaseTick < 0) w.Shop.FirstPurchaseTick = w.Clock.Tick;
            int id = kind == OfferKind.BuyItem ? (int)item : (int)weapon;
            w.Emit(SimEventType.Purchased, id, w.Player.Position, new Vector2(offer.Level, 0), offer.Price, (int)kind);
            return true;
        }

        static bool Matches(in ShopOffer o, WeaponId weapon, ItemId item) => o.Kind switch
        {
            OfferKind.BuyItem => o.Item == item,
            OfferKind.HealPlayer or OfferKind.HealCompanion => true,
            _ => o.Weapon == weapon,
        };
    }
}
