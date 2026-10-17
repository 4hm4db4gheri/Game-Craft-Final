using System;
using UnityEngine;

namespace Hollow.Core.Input
{
    /// <summary>
    /// Everything the world receives from the player for one simulation tick.
    /// Plain data: it can be recorded, replayed by a bot, or built by tests.
    /// </summary>
    [Serializable]
    public struct PlayerCommand
    {
        public Vector2 Move;          // magnitude <= 1, screen-aligned (W = +Y)
        public Vector2 AimWorld;      // world-space point under the cursor
        public bool FireHeld;
        public bool FirePressed;
        public bool FireReleased;
        public bool CancelPressed;    // right click
        public bool DashPressed;
        public bool ReloadPressed;
        public int SelectWeaponSlot;  // 0 = none, 1 = pistol, 2 = primary
        public int CycleWeapon;       // mouse wheel: -1, 0, +1
        public bool EquipItemPressed; // F
        public bool CycleItemPressed; // G
        public bool InteractPressed;  // E

        public static readonly PlayerCommand None = default;
    }

    /// <summary>Raw device state for one rendered frame, already reduced to what the router needs.</summary>
    public struct RawInputSample
    {
        public Vector2 Move;
        public Vector2 AimWorld;
        public bool FireHeld;
        public bool FireDown;
        public bool CancelDown;
        public bool DashDown;
        public bool ReloadDown;
        public bool Weapon1Down;
        public bool Weapon2Down;
        public float Scroll;
        public bool EquipItemDown;
        public bool CycleItemDown;
        public bool InteractDown;

        /// <summary>True when the pointer is over an interactive UI element this frame.</summary>
        public bool PointerOverUI;
    }
}
