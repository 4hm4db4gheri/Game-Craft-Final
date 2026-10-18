using System.Collections.Generic;
using Hollow.Core.Input;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    public enum ItemUseResult { Used, NotHolding, Gap, Invalid }

    /// <summary>
    /// Items (GDD v0.19 §8): one selected type. G selects the next type with stock (skipping empty or locked
    /// ones), F equips the selected item without using it; grenades aim while the click is held and throw on release,
    /// while other items use on click. Right click
    /// or a weapon swap cancels without consuming, an invalid use consumes nothing, and one shared use gap
    /// never blocks weapon swaps or dash. Item damage ignores the general damage skill; grenades and mines
    /// never hurt the player or the companion.
    /// </summary>
    public static class ItemSystem
    {
        static readonly List<EnemyState> Candidates = new(32);

        public static void Step(World w, in PlayerCommand cmd)
        {
            var inv = w.Inventory;
            if (w.Player.Dead) { inv.Holding = false; return; }

            if (cmd.EquipItemPressed) EquipSelected(w);
            if (cmd.CycleItemPressed) Cycle(w);
            if (inv.Holding && (cmd.CancelPressed || cmd.SelectWeaponSlot != 0 || cmd.CycleWeapon != 0)) Cancel(w);
            if (inv.Holding && inv.Count(inv.Held) <= 0) Cancel(w);
            if (!cmd.FireHeld && !cmd.FireReleased) inv.SuppressFireUntilRelease = false;
            if (inv.Holding && inv.Held == ItemId.Grenade && cmd.FirePressed)
            {
                inv.GrenadeChargeStarted = true;
                inv.SuppressFireUntilRelease = true;
            }
            if (inv.Holding && ((inv.Held == ItemId.Grenade && cmd.FireReleased && inv.GrenadeChargeStarted)
                || (inv.Held != ItemId.Grenade && cmd.FirePressed)))
            {
                // The click belongs to the item: the weapon must not also fire on it or while it stays held.
                inv.SuppressFireUntilRelease = true;
                Use(w);
            }

            StepMines(w);
        }

        /// <summary>A type with no data (the removed push pulse in an old save) is never usable.</summary>
        public static bool Available(World w, ItemId id) =>
            w.Inventory.Count(id) > 0 && w.Profile.HasItem(id) && w.Profile.Item(id).UnlockPhase <= w.Phase;

        static readonly ItemId[] AllTypes = (ItemId[])System.Enum.GetValues(typeof(ItemId));

        /// <summary>The selected type if it can be used, otherwise the next type that can (null when none).</summary>
        public static ItemId? Selection(World w)
        {
            var inv = w.Inventory;
            if (inv.HasSelection && Available(w, inv.Selected)) return inv.Selected;
            int start = inv.HasSelection ? System.Array.IndexOf(AllTypes, inv.Selected) : -1;
            for (int i = 1; i <= AllTypes.Length; i++)
            {
                var id = AllTypes[(start + i) % AllTypes.Length];
                if (Available(w, id)) return id;
            }
            return null;
        }

        /// <summary>F: equips the selected type (or the next usable one) without consuming it.</summary>
        public static bool EquipSelected(World w)
        {
            var id = Selection(w);
            if (id == null) return false;
            Select(w, id.Value);
            return Equip(w, id.Value);
        }

        /// <summary>Equips without consuming (F or clicking the slot).</summary>
        public static bool Equip(World w, ItemId id)
        {
            if (!Available(w, id)) return false;
            Select(w, id);
            w.Inventory.Holding = true;
            w.Inventory.Held = id;
            return true;
        }

        static void Select(World w, ItemId id)
        {
            w.Inventory.HasSelection = true;
            w.Inventory.Selected = id;
        }

        /// <summary>G: the next type with stock becomes the selection; a held item follows the selection.</summary>
        public static void Cycle(World w)
        {
            var inv = w.Inventory;
            int start = inv.HasSelection ? System.Array.IndexOf(AllTypes, inv.Selected) : -1;
            for (int i = 1; i <= AllTypes.Length; i++)
            {
                var id = AllTypes[(start + i) % AllTypes.Length];
                if (!Available(w, id)) continue;
                Select(w, id);
                if (inv.Holding) inv.Held = id;
                return;
            }
        }

        public static void Cancel(World w)
        {
            w.Inventory.Holding = false;
            w.Inventory.GrenadeChargeStarted = false;
        }

        public static ItemUseResult Use(World w)
        {
            var inv = w.Inventory;
            if (!inv.Holding) return ItemUseResult.NotHolding;
            if (w.Clock.Tick < inv.NextUseTick) return ItemUseResult.Gap;
            var data = w.Profile.Item(inv.Held);
            bool ok = inv.Held switch
            {
                ItemId.Grenade => ThrowGrenade(w, data),
                ItemId.Mine => PlaceMine(w, data),
                ItemId.EmpPulse => Emp(w, data),
                ItemId.PersonalShield => Shield(w, data),
                _ => false,
            };
            if (!ok) return ItemUseResult.Invalid; // nothing consumed, still held

            inv.Get(inv.Held).Count--;
            inv.NextUseTick = w.Clock.Tick + w.Ticks(w.Profile.Economy.ItemUseGap);
            w.Progress.ItemsUsed++;
            if (w.Tutorial == TutorialStage.Grenade && inv.Held == ItemId.Grenade)
                w.Tutorial = TutorialStage.GrenadeThrown;
            w.Emit(SimEventType.ItemUsed, PlayerState.EntityId, w.Player.Position, default, 0, (int)inv.Held);
            inv.Holding = false; // the previous weapon comes back
            inv.GrenadeChargeStarted = false;
            if (inv.Count(inv.Held) <= 0 && Selection(w) is ItemId next) Select(w, next); // an empty type passes the slot on
            return ItemUseResult.Used;
        }

        /// <summary>Grenade landing point: the cursor, clamped to the throw range (shown as a preview while held).</summary>
        public static Vector2 GrenadeTarget(World w)
        {
            var p = w.Player;
            var data = w.Profile.Item(ItemId.Grenade);
            Vector2 to = Vector2.ClampMagnitude(p.Aim - p.Position, data.ThrowRange);
            if (w.Arena.SegmentHit(p.Position, p.Position + to, out float t)) to *= Mathf.Max(0, t - 0.05f);
            return p.Position + to;
        }

        static bool ThrowGrenade(World w, ItemData d)
        {
            Vector2 target = GrenadeTarget(w);
            // Lands and goes off FuseTime after the throw (flight included, D32).
            HazardSystem.Create(w, HazardOwner.Player, w.Player.Position, target, d.Radius, d.Damage, d.FuseTime, 0, default, d.FuseTime * d.FlightTimeFraction);
            return true;
        }

        static bool PlaceMine(World w, ItemData d)
        {
            var inv = w.Inventory;
            int active = 0;
            foreach (var m in inv.Mines) if (m.Alive) active++;
            if (active >= d.MaxActivePerPhase) return false;
            Vector2 at = w.Player.Position;
            if (w.Arena.CircleOverlaps(at, d.PlacementRadius)) return false;
            inv.Mines.Add(new MineState { Id = w.AllocateId(), Position = at, ArmedTick = w.Clock.Tick + w.Ticks(d.ArmingTime) });
            return true;
        }

        static void StepMines(World w)
        {
            var mines = w.Inventory.Mines;
            if (mines.Count == 0) return;
            var d = w.Profile.Item(ItemId.Mine);
            foreach (var m in mines)
            {
                if (!m.Alive || w.Clock.Tick < m.ArmedTick) continue;
                var boss = w.Boss;
                if (boss.Active && !boss.Dead && Vector2.Distance(boss.Position, m.Position) <= d.TriggerRadius + boss.Radius)
                {
                    m.Alive = false;
                    var hb = HazardSystem.Create(w, HazardOwner.Player, m.Position, m.Position, d.Radius, d.Damage, 0f);
                    hb.Cancelled = true;
                    HazardSystem.Detonate(w, hb);
                    continue;
                }
                foreach (var e in w.Enemies)
                {
                    if (!e.IsActive || Vector2.Distance(e.Position, m.Position) > d.TriggerRadius + e.Radius) continue;
                    m.Alive = false;
                    var h = HazardSystem.Create(w, HazardOwner.Player, m.Position, m.Position, d.Radius, d.Damage, 0f);
                    h.Cancelled = true;
                    HazardSystem.Detonate(w, h);
                    break;
                }
            }
            mines.RemoveAll(m => !m.Alive);
        }

        /// <summary>Phase change removes placed mines without refund (§9).</summary>
        public static void ClearMines(World w) => w.Inventory.Mines.Clear();

        static bool Emp(World w, ItemData d)
        {
            var p = w.Player;
            foreach (var e in w.Enemies)
            {
                if (!e.Alive || Vector2.Distance(e.Position, p.Position) > d.Radius + e.Radius) continue;
                if (!w.Profile.Enemy(e.Type).IsMachine) continue; // never human bodies (§9)
                Stun(w, e, d.StunRegular);
            }
            if (w.Boss.Active && Vector2.Distance(w.Boss.Position, p.Position) <= d.Radius + w.Boss.Radius) BossSystem.Emp(w, d.StunBoss, d.BossResistance);
            w.Emit(SimEventType.Explosion, PlayerState.EntityId, p.Position, default, d.Radius, -2);
            return true;
        }

        /// <summary>Interrupts the current action (including a windup) for the stun duration.</summary>
        public static void Stun(World w, EnemyState e, float seconds)
        {
            if (w.Clock.Tick < e.EmpResistantUntilTick) return;
            e.StunnedUntilTick = w.Clock.Tick + w.Ticks(seconds);
            if (e.Ai != EnemyAi.Spawning) e.Ai = EnemyAi.Chase;
            e.Velocity = Vector2.zero;
        }

        static bool Shield(World w, ItemData d)
        {
            var inv = w.Inventory;
            if (w.Clock.Tick < inv.ShieldUntilTick) return false; // never stacks
            inv.ShieldUntilTick = w.Clock.Tick + w.Ticks(d.Duration);
            return true;
        }
    }
}
