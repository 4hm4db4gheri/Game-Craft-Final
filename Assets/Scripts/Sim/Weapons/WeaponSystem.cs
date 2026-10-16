using System;
using Hollow.Core.Input;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// Guns (GDD §4, §8): per-level records, unlimited reserve with finite magazines, automatic reload on the
    /// last round, manual reload with R, reload cancelled by swap or dash (except weapons flagged otherwise),
    /// automatic reload restarts when returning to an empty gun, 0.15 s swap. General skill modifiers are
    /// applied exactly once, after reading the level record.
    /// </summary>
    public static class WeaponSystem
    {
        public static WeaponLevel LevelData(World w, OwnedWeapon o)
        {
            var levels = w.Profile.Weapon(o.Id).Levels;
            return levels[Mathf.Clamp(o.Level - 1, 0, levels.Count - 1)];
        }

        public static float ShotDamage(World w, WeaponLevel l) => l.Damage * PlayerStats.WeaponDamageMultiplier(w);

        public static float ReloadSeconds(World w, WeaponLevel l) =>
            Mathf.Max(w.Profile.Combat.ReloadFloor, l.ReloadTime * PlayerStats.ReloadMultiplier(w));

        /// <summary>Adds a weapon (full magazine). Non-pistol weapons go to the primary slot (newest purchase).</summary>
        public static OwnedWeapon Give(World w, WeaponId id, int level = 1, bool equip = true)
        {
            var L = w.Player.Loadout;
            var owned = L.Get(id);
            if (owned == null)
            {
                owned = new OwnedWeapon { Id = id, Level = level };
                owned.Ammo = LevelData(w, owned).Magazine;
                L.Owned.Add(owned);
            }
            if (equip && id != L.SlotPistol) EquipPrimary(w, id);
            return owned;
        }

        /// <summary>Puts an owned weapon in the primary slot (free re-equip at the shop).</summary>
        public static void EquipPrimary(World w, WeaponId id)
        {
            var L = w.Player.Loadout;
            if (!L.Owns(id) || id == L.SlotPistol) return;
            if (L.ActiveSlot == 2) CancelReload(w);
            L.HasPrimary = true;
            L.SlotPrimary = id;
        }

        /// <summary>Applies a new level; the current magazine count is kept, clamped to the new size (D27).</summary>
        public static void SetLevel(World w, WeaponId id, int level)
        {
            var o = w.Player.Loadout.Get(id);
            if (o == null) return;
            o.Level = level;
            var l = LevelData(w, o);
            if (w.Profile.Weapon(id).Kind == WeaponKind.Gun) o.Ammo = Mathf.Min(o.Ammo, l.Magazine);
        }

        public static void Step(World w, in PlayerCommand cmd)
        {
            var p = w.Player;
            var L = p.Loadout;
            if (p.Dead || L.Owned.Count == 0) return;
            long now = w.Clock.Tick;

            int target = cmd.SelectWeaponSlot;
            if (cmd.CycleWeapon != 0) target = L.ActiveSlot == 1 ? 2 : 1;
            if (target != 0 && target != L.ActiveSlot && (target == 1 || L.HasPrimary)) Swap(w, target);

            var active = L.Active;
            var data = w.Profile.Weapon(active.Id);
            var level = LevelData(w, active);

            if (data.Kind == WeaponKind.Melee)
            {
                if (!w.Inventory.Holding && !w.Inventory.SuppressFireUntilRelease) MeleeSystem.Step(w, cmd, active, level);
                return;
            }

            if (L.Reloading && now >= L.ReloadEndTick)
            {
                L.Reloading = false;
                active.Ammo = level.Magazine;
                w.Emit(SimEventType.ReloadFinished, PlayerState.EntityId, p.Position, default, 0, (int)active.Id);
            }

            bool swapping = now < L.SwapReadyTick;
            if (cmd.ReloadPressed && !L.Reloading && active.Ammo < level.Magazine) StartReload(w, active, level);
            if (active.Ammo <= 0 && !L.Reloading && !swapping) StartReload(w, active, level);

            bool wantsFire = (cmd.FireHeld || cmd.FirePressed) && !w.Inventory.Holding && !w.Inventory.SuppressFireUntilRelease;
            double seconds = w.Clock.Seconds;
            if (wantsFire && !L.Reloading && !swapping && active.Ammo > 0 && seconds >= L.NextFireTime - 1e-9)
            {
                Fire(w, active, level);
                double interval = 1.0 / Math.Max(0.01f, level.Rate);
                // Keep an exact cadence while the trigger stays held; restart it after a pause.
                L.NextFireTime = (seconds - L.NextFireTime < interval ? L.NextFireTime : seconds) + interval;
                if (active.Ammo <= 0) StartReload(w, active, level);
            }
        }

        static void Fire(World w, OwnedWeapon active, WeaponLevel l)
        {
            var p = w.Player;
            Vector2 dir = SimMath.SafeNormal(p.Aim - p.Position, p.LastMoveDir);
            int count = Mathf.Max(1, l.ProjectileCount);
            float damage = ShotDamage(w, l);
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1
                    ? w.Rng.Range(-l.SpreadDeg * 0.5f, l.SpreadDeg * 0.5f)
                    : -l.SpreadDeg * 0.5f + l.SpreadDeg * (i + w.Rng.Value) / count;
                Vector2 d = SimMath.Rotate(dir, angle);
                w.Projectiles.Add(new ProjectileState
                {
                    Id = w.AllocateId(), Owner = ProjectileOwner.Player, SourceId = PlayerState.EntityId, Weapon = active.Id,
                    Position = p.Position, PrevPosition = p.Position, Origin = p.Position,
                    Velocity = d * l.ProjectileSpeed, Damage = damage, Range = l.Range,
                    Radius = w.Profile.Combat.PlayerProjectileRadius, FalloffAtMaxRange = l.FalloffAtMaxRange,
                    TargetsLeft = Mathf.Max(1, l.Targets),
                });
            }
            active.Ammo--;
            w.Emit(SimEventType.ShotFired, PlayerState.EntityId, p.Position, dir, count, (int)active.Id);
        }

        static void StartReload(World w, OwnedWeapon active, WeaponLevel l)
        {
            var L = w.Player.Loadout;
            L.Reloading = true;
            L.ReloadTotalTicks = Math.Max(1, w.Ticks(ReloadSeconds(w, l)));
            L.ReloadEndTick = w.Clock.Tick + L.ReloadTotalTicks;
            w.Emit(SimEventType.ReloadStarted, PlayerState.EntityId, w.Player.Position, default, ReloadSeconds(w, l), (int)active.Id);
        }

        public static void CancelReload(World w)
        {
            var L = w.Player.Loadout;
            if (!L.Reloading) return;
            L.Reloading = false;
            w.Emit(SimEventType.ReloadCancelled, PlayerState.EntityId, w.Player.Position, default, 0, (int)L.ActiveId);
        }

        static void Swap(World w, int slot)
        {
            var L = w.Player.Loadout;
            CancelReload(w);
            L.ActiveSlot = slot;
            L.SwapReadyTick = w.Clock.Tick + w.Ticks(w.Profile.Combat.WeaponSwapTime);
            w.Emit(SimEventType.WeaponSwapped, PlayerState.EntityId, w.Player.Position, default, slot, (int)L.ActiveId);
        }

        /// <summary>Dash cancels a reload without filling the magazine, unless the weapon keeps it (D22).</summary>
        public static void OnDashStarted(World w)
        {
            var L = w.Player.Loadout;
            if (!L.Reloading || L.Owned.Count == 0) return;
            if (w.Profile.Weapon(L.ActiveId).ReloadSurvivesDash) return;
            CancelReload(w);
        }
    }
}
