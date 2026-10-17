using Hollow.Data;
using Hollow.Sim;

namespace Hollow.UI
{
    public enum HintId { Item, ItemSwitch, WeaponSwitch, Loadout, Reload, Heal, HealCompanion, Upgrade }

    /// <summary>The HUD part a hint points at.</summary>
    public enum HintTarget { Item, Weapon, Health, Companion, Wallet }

    /// <summary>What the world looked like when a hint appeared, so it can close once the player acts on it.</summary>
    public struct HintStart
    {
        public int ActiveSlot;
        public ItemId? Selection;
    }

    /// <summary>
    /// One-time contextual hints (a new item, a second item type, a second weapon...). Each shows once per game:
    /// shown hints are kept with the save in <see cref="NarrativeState.SeenScenes"/> as <c>hint:Id</c>.
    /// The thresholds are UI cues, not gameplay numbers.
    /// </summary>
    public static class HintRules
    {
        const float LowHealthFraction = 0.4f;
        const float LowCompanionFraction = 0.5f;

        static readonly HintId[] Order =
        {
            HintId.WeaponSwitch, HintId.Item, HintId.ItemSwitch, HintId.Loadout, HintId.Reload, HintId.HealCompanion, HintId.Heal, HintId.Upgrade,
        };

        public static string Key(HintId id) => "hint:" + id;
        public static bool Seen(World w, HintId id) => w.Narrative.SeenScenes.Contains(Key(id));

        public static void MarkSeen(World w, HintId id)
        {
            if (!Seen(w, id)) w.Narrative.SeenScenes.Add(Key(id));
        }

        public static string Text(HintId id) => Loc.T("hint." + id);

        public static HintTarget Target(HintId id) => id switch
        {
            HintId.Item or HintId.ItemSwitch => HintTarget.Item,
            HintId.Heal => HintTarget.Health,
            HintId.HealCompanion => HintTarget.Companion,
            HintId.Upgrade => HintTarget.Wallet,
            _ => HintTarget.Weapon,
        };

        /// <summary>The first unseen hint whose moment has come, or null.</summary>
        public static HintId? Next(World w)
        {
            foreach (var id in Order)
                if (!Seen(w, id) && Due(w, id)) return id;
            return null;
        }

        public static bool Due(World w, HintId id)
        {
            var L = w.Player.Loadout;
            return id switch
            {
                HintId.Item => ItemSystem.Selection(w) != null,
                HintId.ItemSwitch => ItemTypesCarried(w) >= 2,
                HintId.WeaponSwitch => L.HasPrimary,
                HintId.Loadout => L.Owned.Count >= 3,
                HintId.Reload => L.Reloading && L.Active != null && L.Active.Ammo == 0,
                HintId.Heal => w.Player.Health < PlayerStats.MaxHealth(w) * LowHealthFraction && !w.Player.Dead,
                HintId.HealCompanion => CompanionSystem.Active(w) && w.Companion.Health < w.Companion.MaxHealth * LowCompanionFraction,
                HintId.Upgrade => CanAffordAnUpgrade(w),
                _ => false,
            };
        }

        public static HintStart Capture(World w) => new() { ActiveSlot = w.Player.Loadout.ActiveSlot, Selection = ItemSystem.Selection(w) };

        /// <summary>True once the player did what the hint teaches; the hint may then close early.</summary>
        public static bool Done(World w, HintId id, HintStart start) => id switch
        {
            HintId.Item => w.Inventory.Holding,
            HintId.ItemSwitch => ItemSystem.Selection(w) != start.Selection,
            HintId.WeaponSwitch => w.Player.Loadout.ActiveSlot != start.ActiveSlot,
            HintId.Loadout or HintId.Heal or HintId.HealCompanion or HintId.Upgrade => w.Shop.Open,
            _ => false,
        };

        static bool CanAffordAnUpgrade(World w)
        {
            if (w.Tutorial != TutorialStage.None) return false;
            foreach (var o in ShopSystem.Offers(w))
                if (o.Kind == OfferKind.UpgradeWeapon && o.Enabled) return true;
            return false;
        }

        static int ItemTypesCarried(World w)
        {
            int n = 0;
            foreach (var s in w.Inventory.Slots) if (s.Count > 0 && ItemSystem.Available(w, s.Id)) n++;
            return n;
        }
    }
}
