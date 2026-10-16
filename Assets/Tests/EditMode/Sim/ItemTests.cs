using System.Linq;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>T12 acceptance tests (GDD §9, §10).</summary>
    public class ItemTests
    {
        static readonly Vector2 AimRight = new(5, 0);

        static Simulation WithItems(int phase = 3, bool companion = false)
        {
            var sim = SimTestUtil.NewSim(companion: companion);
            var w = sim.World;
            w.Phase = phase;
            foreach (ItemId id in System.Enum.GetValues(typeof(ItemId))) w.Inventory.Add(id, 2);
            return sim;
        }

        static PlayerCommand Click => new() { FirePressed = true, FireHeld = true, AimWorld = AimRight };
        static PlayerCommand Idle => new() { AimWorld = AimRight };

        [Test]
        public void FEquipsWithoutConsumingAndClickUsesThenRestoresTheWeapon()
        {
            var sim = WithItems();
            var w = sim.World;
            w.Inventory.MoveToFirst(ItemId.PushPulse);
            sim.Step(new PlayerCommand { EquipItemPressed = true, AimWorld = AimRight });
            Assert.IsTrue(w.Inventory.Holding);
            Assert.AreEqual(ItemId.PushPulse, w.Inventory.Held);
            Assert.AreEqual(2, w.Inventory.Count(ItemId.PushPulse), "F only equips");
            int ammo = w.Player.Loadout.Active.Ammo;
            sim.Step(Click);
            Assert.AreEqual(1, w.Inventory.Count(ItemId.PushPulse));
            Assert.IsFalse(w.Inventory.Holding, "previous weapon restored");
            Assert.AreEqual(ammo, w.Player.Loadout.Active.Ammo, "the item click did not fire the gun");
            sim.Run(10, new PlayerCommand { FireHeld = true, AimWorld = AimRight });
            Assert.AreEqual(ammo, w.Player.Loadout.Active.Ammo, "holding the same click does not start firing");
        }

        [Test]
        public void GCyclesSkippingEmptyAndLockedSlots()
        {
            var sim = WithItems(phase: 1);
            var w = sim.World;
            w.Inventory.Get(ItemId.Grenade).Count = 0;
            sim.Step(new PlayerCommand { CycleItemPressed = true });
            Assert.AreEqual(ItemId.PushPulse, w.Inventory.Held, "grenade empty, push pulse next");
            sim.Step(new PlayerCommand { CycleItemPressed = true });
            Assert.AreEqual(ItemId.PushPulse, w.Inventory.Held, "mine/EMP/shield are locked in phase 1");
        }

        [TestCase("right click")]
        [TestCase("weapon swap")]
        public void CancelPathsConsumeNothing(string how)
        {
            var sim = WithItems();
            var w = sim.World;
            ItemSystem.Equip(w, ItemId.Grenade);
            sim.Step(how == "right click" ? new PlayerCommand { CancelPressed = true } : new PlayerCommand { SelectWeaponSlot = 1 });
            Assert.IsFalse(w.Inventory.Holding);
            Assert.AreEqual(2, w.Inventory.Count(ItemId.Grenade));
        }

        [Test]
        public void SharedUseGapBlocksItemsButNotDashOrSwaps()
        {
            var sim = WithItems();
            var w = sim.World;
            ItemSystem.Equip(w, ItemId.PushPulse);
            sim.Step(Click);
            sim.Step(Idle);
            ItemSystem.Equip(w, ItemId.EmpPulse);
            sim.Step(Click);
            Assert.AreEqual(2, w.Inventory.Count(ItemId.EmpPulse), "inside the 0.5 s gap");
            sim.Step(new PlayerCommand { DashPressed = true, Move = Vector2.up });
            Assert.IsTrue(w.Player.Dashing, "dash is never blocked by the gap");
            sim.Run(40, Idle);
            ItemSystem.Equip(w, ItemId.EmpPulse);
            sim.Step(Click);
            Assert.AreEqual(1, w.Inventory.Count(ItemId.EmpPulse));
        }

        [Test]
        public void ShieldNeverStacksAndInvalidUseConsumesNothing()
        {
            var sim = WithItems();
            var w = sim.World;
            ItemSystem.Equip(w, ItemId.PersonalShield);
            sim.Step(Click);
            Assert.AreEqual(1, w.Inventory.Count(ItemId.PersonalShield));
            Assert.IsFalse(PlayerSystem.Damage(w, 50, Vector2.left), "shielded");
            sim.Run(40, Idle);
            ItemSystem.Equip(w, ItemId.PersonalShield);
            sim.Step(Click);
            Assert.AreEqual(1, w.Inventory.Count(ItemId.PersonalShield), "second shield refused while one is active");
            Assert.IsTrue(w.Inventory.Holding, "still held after an invalid use");
        }

        [Test]
        public void ShieldDoesNotProtectTheCompanion()
        {
            var sim = WithItems(phase: 1, companion: true);
            var w = sim.World;
            w.Inventory.ShieldUntilTick = long.MaxValue;
            Assert.IsTrue(CompanionSystem.Damage(w, 10, Vector2.left));
        }

        [Test]
        public void MineCapAndRemovalOnPhaseChange()
        {
            var sim = WithItems(phase: 2);
            var w = sim.World;
            w.Inventory.Get(ItemId.Mine).Count = 10;
            for (int i = 0; i < 5; i++)
            {
                w.Player.Position = new Vector2(i * 2f - 4f, 3);
                ItemSystem.Equip(w, ItemId.Mine);
                sim.Step(Click);
                sim.Run(40, Idle);
            }
            Assert.AreEqual(4, w.Inventory.Mines.Count, "at most four active mines");
            Assert.AreEqual(6, w.Inventory.Count(ItemId.Mine), "the fifth (invalid) placement consumed nothing");
            PhaseSystem.EnterPhase(w, 3);
            Assert.AreEqual(0, w.Inventory.Mines.Count, "removed on phase change without refund");
            Assert.AreEqual(6, w.Inventory.Count(ItemId.Mine));
        }

        [Test]
        public void MineArmsThenTriggersOnEnemies()
        {
            var sim = WithItems(phase: 2);
            var w = sim.World;
            ItemSystem.Equip(w, ItemId.Mine);
            sim.Step(Click);
            var target = w.AddEnemy(EnemyId.R1_Crawler, w.Inventory.Mines[0].Position + new Vector2(0.5f, 0), health: 1000);
            target.StunnedUntilTick = long.MaxValue;
            sim.Step(Idle);
            Assert.AreEqual(1000f, target.Health, "not armed yet");
            sim.Run(40, Idle);
            Assert.AreEqual(1000f - w.Profile.Item(ItemId.Mine).Damage, target.Health, 1e-3f);
            Assert.AreEqual(100f, w.Player.Health, "never hurts the player");
        }

        [Test]
        public void GrenadeDamagesEnemiesInRadiusButNeverThePlayerOrCompanion()
        {
            var sim = WithItems(phase: 1, companion: true);
            var w = sim.World;
            w.Companion.Position = new Vector2(4.5f, 0.5f);
            var target = w.AddEnemy(EnemyId.H1_BatonGuard, new Vector2(5, 0), health: 1000);
            target.StunnedUntilTick = long.MaxValue;
            w.Player.SkillRanks[(int)SkillId.WeaponDamage] = 3; // must not affect items
            ItemSystem.Equip(w, ItemId.Grenade);
            w.Drain();
            sim.Step(Click);
            sim.Run(70, Idle);
            float blast = w.Drain().Where(e => e.Type == SimEventType.EnemyHit && e.Code == (int)DamageSource.Explosion).Sum(e => e.Value);
            Assert.AreEqual(w.Profile.Item(ItemId.Grenade).Damage, blast, 1e-3f, "raw item damage, no skill bonus");
            Assert.AreEqual(100f, w.Player.Health);
            Assert.AreEqual(140f, w.Companion.Health);
        }

        [Test]
        public void EmpStunsMachinesButNeverHumanBodies()
        {
            var sim = WithItems();
            var w = sim.World;
            var robot = w.AddEnemy(EnemyId.R1_Crawler, new Vector2(2, 0));
            var human = w.AddEnemy(EnemyId.H1_BatonGuard, new Vector2(-2, 0));
            var turret = w.AddEnemy(EnemyId.H7c_Turret, new Vector2(0, 2));
            ItemSystem.Equip(w, ItemId.EmpPulse);
            sim.Step(Click);
            Assert.Greater(robot.StunnedUntilTick, w.Clock.Tick);
            Assert.Greater(turret.StunnedUntilTick, w.Clock.Tick, "turrets are machine equipment");
            Assert.LessOrEqual(human.StunnedUntilTick, w.Clock.Tick);
        }

        [Test]
        public void PushPulseMovesEnemiesAndDealsNoDamage()
        {
            var sim = WithItems();
            var w = sim.World;
            var e = w.AddEnemy(EnemyId.A1_RunnerWolf, new Vector2(1.5f, 0));
            ItemSystem.Equip(w, ItemId.PushPulse);
            sim.Step(Click);
            sim.Run(15, Idle);
            Assert.AreEqual(1000f, e.Health);
            Assert.Greater(e.Position.x, 3f);
        }

        [Test]
        public void SlotOrderIsSaved()
        {
            var sim = WithItems();
            var w = sim.World;
            w.Inventory.MoveToFirst(ItemId.EmpPulse);
            var loaded = WorldSerializer.FromJson(WorldSerializer.ToJson(w), w.Profile, w.Layout);
            Assert.AreEqual(ItemId.EmpPulse, loaded.Inventory.Slots[0].Id);
        }
    }
}
