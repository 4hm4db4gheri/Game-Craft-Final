using Hollow.Data;
using Hollow.Sim;

namespace Hollow.UI
{
    public enum HintId { Item, ItemSwitch, WeaponSwitch, HealCompanion }

    /// <summary>The HUD part a hint points at.</summary>
    public enum HintTarget { Item, Weapon, Companion }

    /// <summary>What the world looked like when a hint appeared, so it can close once the player acts on it.</summary>
    public struct HintStart
    {
        public int ActiveSlot;
        public ItemId? Selection;
    }

    /// <summary>
    /// One-time contextual hints (a second weapon, the first item, a second item type, a hurt operator). Each shows
    /// once per game: shown hints are kept with the save in <see cref="NarrativeState.SeenScenes"/> as <c>hint:Id</c>.
    /// The threshold is a UI cue, not a gameplay number.
    /// </summary>
    public static class HintRules
    {
        const float LowCompanionFraction = 0.5f;

        static readonly HintId[] Order = { HintId.WeaponSwitch, HintId.Item, HintId.ItemSwitch, HintId.HealCompanion };

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
            HintId.HealCompanion => HintTarget.Companion,
            _ => HintTarget.Weapon,
        };

        /// <summary>The first unseen hint whose moment has come, or null.</summary>
        public static HintId? Next(World w)
        {
            foreach (var id in Order)
                if (!Seen(w, id) && Due(w, id)) return id;
            return null;
        }

        public static bool Due(World w, HintId id) => id switch
        {
            HintId.Item => ItemSystem.Selection(w) != null,
            HintId.ItemSwitch => ItemTypesCarried(w) >= 2,
            HintId.WeaponSwitch => w.Player.Loadout.HasPrimary,
            HintId.HealCompanion => CompanionSystem.Active(w) && w.Companion.Health < w.Companion.MaxHealth * LowCompanionFraction,
            _ => false,
        };

        public static HintStart Capture(World w) => new() { ActiveSlot = w.Player.Loadout.ActiveSlot, Selection = ItemSystem.Selection(w) };

        /// <summary>True once the player did what the hint teaches; the hint may then close early.</summary>
        public static bool Done(World w, HintId id, HintStart start) => id switch
        {
            HintId.Item => w.Inventory.Holding,
            HintId.ItemSwitch => ItemSystem.Selection(w) != start.Selection,
            HintId.WeaponSwitch => w.Player.Loadout.ActiveSlot != start.ActiveSlot,
            HintId.HealCompanion => w.Shop.Open,
            _ => false,
        };

        static int ItemTypesCarried(World w)
        {
            int n = 0;
            foreach (var s in w.Inventory.Slots) if (s.Count > 0 && ItemSystem.Available(w, s.Id)) n++;
            return n;
        }
    }
}
