using System.Collections.Generic;
using Hollow.Data;
using Newtonsoft.Json;

namespace Hollow.Sim
{
    /// <summary>A weapon the player owns: its level and magazine persist while it is holstered (§8).</summary>
    public sealed class OwnedWeapon
    {
        public WeaponId Id;
        public int Level = 1;
        public int Ammo;
    }

    /// <summary>
    /// Two field slots. Slot 1 starts with the pistol, slot 2 holds the newest purchase; at the shop any owned
    /// weapon, the pistol included, can be put in either slot for free (D18, D73). Slot 1 is never empty.
    /// </summary>
    public sealed class LoadoutState
    {
        public List<OwnedWeapon> Owned = new();
        /// <summary>Slot 1 (saves keep its pre-D73 name).</summary>
        [JsonProperty("SlotPistol")] public WeaponId Slot1 = WeaponId.Pistol;
        /// <summary>Slot 2 is empty until the first purchase.</summary>
        public bool HasPrimary;
        public WeaponId SlotPrimary;
        /// <summary>1 or 2.</summary>
        public int ActiveSlot = 1;

        public long SwapReadyTick;
        public double NextFireTime;
        public bool Reloading;
        public long ReloadEndTick;
        public long ReloadTotalTicks;
        public int ReloadStartAmmo;

        public WeaponId ActiveId => ActiveSlot == 2 && HasPrimary ? SlotPrimary : Slot1;

        /// <summary>The slot holding this weapon: 1, 2, or 0 when it is only owned.</summary>
        public int SlotOf(WeaponId id) => Slot1 == id ? 1 : HasPrimary && SlotPrimary == id ? 2 : 0;

        public OwnedWeapon Get(WeaponId id) => Owned.Find(o => o.Id == id);
        public OwnedWeapon Active => Get(ActiveId);
        public bool Owns(WeaponId id) => Owned.Exists(o => o.Id == id);
    }
}
