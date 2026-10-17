using UnityEngine;

namespace Hollow.Core.Input
{
    /// <summary>
    /// Decides whether input belongs to the UI or to the world (G5) and turns per-frame samples into
    /// per-tick <see cref="PlayerCommand"/>s.
    /// Rules: a click that starts over UI, or while the world is not accepting input, is latched and
    /// never fires until the button is released; button presses between ticks are kept until consumed.
    /// </summary>
    public sealed class InputRouter
    {
        bool fireLatched;
        bool worldAcceptsInput;
        bool lastFireHeld;
        PlayerCommand pending;

        public bool WorldAcceptsInput => worldAcceptsInput;

        /// <summary>Called by the app when the game state or a modal panel changes.</summary>
        public void SetWorldAcceptsInput(bool accepts)
        {
            if (accepts && !worldAcceptsInput && lastFireHeld)
                fireLatched = true; // the click that closed a modal must not shoot
            if (!accepts)
                pending = default;
            worldAcceptsInput = accepts;
        }

        /// <summary>Feed one rendered frame of input.</summary>
        public void Sample(in RawInputSample s)
        {
            bool fireReleased = lastFireHeld && !s.FireHeld && !fireLatched && !s.PointerOverUI;
            lastFireHeld = s.FireHeld;

            if (s.FireDown && (s.PointerOverUI || !worldAcceptsInput))
                fireLatched = true;
            if (!s.FireHeld)
                fireLatched = false;

            if (!worldAcceptsInput)
            {
                pending = default;
                return;
            }

            pending.Move = Vector2.ClampMagnitude(s.Move, 1f);
            pending.AimWorld = s.AimWorld;
            pending.FireHeld = s.FireHeld && !fireLatched;
            pending.FirePressed |= s.FireDown && !fireLatched;
            pending.FireReleased |= fireReleased;

            bool overUI = s.PointerOverUI;
            pending.CancelPressed |= s.CancelDown && !overUI;
            pending.DashPressed |= s.DashDown;
            pending.ReloadPressed |= s.ReloadDown;
            if (s.Weapon1Down) pending.SelectWeaponSlot = 1;
            if (s.Weapon2Down) pending.SelectWeaponSlot = 2;
            if (s.Scroll > 0.01f) pending.CycleWeapon = 1;
            else if (s.Scroll < -0.01f) pending.CycleWeapon = -1;
            pending.EquipItemPressed |= s.EquipItemDown;
            pending.CycleItemPressed |= s.CycleItemDown;
            pending.InteractPressed |= s.InteractDown;
        }

        /// <summary>Called once per simulation tick. Continuous values persist; one-shot presses are cleared.</summary>
        public PlayerCommand Consume()
        {
            var cmd = pending;
            pending.FirePressed = false;
            pending.FireReleased = false;
            pending.CancelPressed = false;
            pending.DashPressed = false;
            pending.ReloadPressed = false;
            pending.SelectWeaponSlot = 0;
            pending.CycleWeapon = 0;
            pending.EquipItemPressed = false;
            pending.CycleItemPressed = false;
            pending.InteractPressed = false;
            return cmd;
        }
    }
}
