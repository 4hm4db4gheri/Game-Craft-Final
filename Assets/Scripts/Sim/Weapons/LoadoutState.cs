using System.Collections.Generic;
using Hollow.Data;

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
    /// Two field slots: 1 = pistol, 2 = current primary (gun or melee). Other owned weapons stay owned and
    /// are equipped at the shop for free (D18).
    /// </summary>
    public sealed class LoadoutState
    {
        public List<OwnedWeapon> Owned = new();
        public WeaponId SlotPistol = WeaponId.Pistol;
        public bool HasPrimary;
        public WeaponId SlotPrimary;
        /// <summary>1 = pistol, 2 = primary.</summary>
        public int ActiveSlot = 1;

        public long SwapReadyTick;
        public double NextFireTime;
        public bool Reloading;
        public long ReloadEndTick;
        public long ReloadTotalTicks;

        public WeaponId ActiveId => ActiveSlot == 2 && HasPrimary ? SlotPrimary : SlotPistol;

        public OwnedWeapon Get(WeaponId id) => Owned.Find(o => o.Id == id);
        public OwnedWeapon Active => Get(ActiveId);
        public bool Owns(WeaponId id) => Owned.Exists(o => o.Id == id);
    }
}
