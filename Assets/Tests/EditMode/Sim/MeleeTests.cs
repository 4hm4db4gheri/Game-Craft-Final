using System.Linq;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>T04 acceptance tests (GDD §8, §10).</summary>
    public class MeleeTests
    {
        static readonly Vector2 AimRight = new(10, 0);

        static Simulation WithMelee(WeaponId id)
        {
            var sim = SimTestUtil.NewSim();
            WeaponSystem.Give(sim.World, id);
            sim.Step(new PlayerCommand { SelectWeaponSlot = 2, AimWorld = AimRight });
            sim.Run(12, new PlayerCommand { AimWorld = AimRight });
            return sim;
        }

        static EnemyState Frozen(World w, EnemyId id, Vector2 at)
        {
            var e = w.AddEnemy(id, at);
            e.StunnedUntilTick = long.MaxValue;
            return e;
        }

        [Test]
        public void OneSwingHitsEachTargetInsideTheArcOnce()
        {
            var sim = WithMelee(WeaponId.Baton);
            var w = sim.World;
            var a = Frozen(w, EnemyId.A1_RunnerWolf, new Vector2(1.2f, 0));
            var b = Frozen(w, EnemyId.A1_RunnerWolf, new Vector2(1.0f, 0.7f));
            var c = Frozen(w, EnemyId.A1_RunnerWolf, new Vector2(1.0f, -0.7f));
            w.Drain();
            sim.Step(new PlayerCommand { FirePressed = true, AimWorld = AimRight });
            var hits = w.Drain().Where(e => e.Type == SimEventType.EnemyHit).ToList();
            Assert.AreEqual(3, hits.Count);
            Assert.AreEqual(3, hits.Select(h => h.EntityId).Distinct().Count());
        }

        [TestCase(40f, true)]
        [TestCase(75f, false)]
        public void ArcBoundary(float angleDeg, bool expectHit)
        {
            var sim = WithMelee(WeaponId.Baton); // 100° arc
            var w = sim.World;
            Vector2 at = SimMath.Rotate(Vector2.right, angleDeg) * 1.3f;
            var e = Frozen(w, EnemyId.A7c_Spiderling, at);
            sim.Step(new PlayerCommand { FirePressed = true, AimWorld = AimRight });
            Assert.AreEqual(expectHit, e.Health < 1000f);
        }

        [TestCase(1.6f, true)]
        [TestCase(2.3f, false)]
        public void ReachBoundary(float distance, bool expectHit)
        {
            var sim = WithMelee(WeaponId.Baton); // reach 1.7 to the target's edge
            var w = sim.World;
            var e = Frozen(w, EnemyId.A7c_Spiderling, new Vector2(distance, 0));
            sim.Step(new PlayerCommand { FirePressed = true, AimWorld = AimRight });
            Assert.AreEqual(expectHit, e.Health < 1000f);
        }

        [Test]
        public void SwingKnocksTargetsBack()
        {
            var sim = WithMelee(WeaponId.IndustrialHammer);
            var w = sim.World;
            var e = w.AddEnemy(EnemyId.A1_RunnerWolf, new Vector2(1.5f, 0));
            sim.Step(new PlayerCommand { FirePressed = true, AimWorld = AimRight });
            float x0 = e.Position.x;
            sim.Run(15, new PlayerCommand { AimWorld = AimRight });
            Assert.Greater(e.Position.x - x0, 1.0f, "pushed away from the player");
        }

        [Test]
        public void MovementIsNeverLockedBySwinging()
        {
            var sim = WithMelee(WeaponId.IndustrialHammer);
            var still = SimTestUtil.NewSim();
            sim.Run(60, new PlayerCommand { FireHeld = true, Move = Vector2.up, AimWorld = AimRight });
            still.Run(60, new PlayerCommand { Move = Vector2.up });
            Assert.AreEqual(still.World.Player.Position.y, sim.World.Player.Position.y, 1e-3f);
            Assert.Greater(sim.World.Events.Count(e => e.Type == SimEventType.MeleeSwing), 0);
        }

        [TestCase(WeaponId.Baton, 50f * 0.3f)]
        [TestCase(WeaponId.EnergyBlade, 180f)]
        public void OnlyTheEnergyBladeIgnoresFrontalArmor(WeaponId weapon, float expected)
        {
            var sim = WithMelee(weapon);
            var w = sim.World;
            var tortoise = Frozen(w, EnemyId.A4_ArmoredTortoise, new Vector2(1.5f, 0));
            tortoise.Facing = Vector2.left; // facing the player
            w.Drain();
            sim.Step(new PlayerCommand { FirePressed = true, AimWorld = AimRight });
            var hit = w.Drain().Single(e => e.Type == SimEventType.EnemyHit);
            Assert.AreEqual(expected, hit.Value, 1e-3f);
        }

        [Test]
        public void MeleeHasNoMagazineOrReload()
        {
            var sim = WithMelee(WeaponId.Baton);
            sim.Run(300, new PlayerCommand { FireHeld = true, ReloadPressed = true, AimWorld = AimRight });
            Assert.IsFalse(sim.World.Player.Loadout.Reloading);
            int swings = sim.World.Events.Count(e => e.Type == SimEventType.MeleeSwing);
            Assert.AreEqual(11, swings, 1, "2.2 attacks per second for 5 s");
        }
    }
}
