using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>3/4 view: shots must hit a body where it is drawn, from the feet up to the head, not only at the feet.</summary>
    public class HitShapeTests
    {
        static float ShotAt(EnemyId type, float aimHeight)
        {
            var sim = SimTestUtil.NewSim();
            var e = sim.World.AddEnemy(type, new Vector2(6f, 0f));
            var cmd = new PlayerCommand { FireHeld = true, AimWorld = e.Position + Vector2.up * aimHeight };
            sim.Run(20, cmd);
            return e.MaxHealth - e.Health;
        }

        [TestCase(EnemyId.H1_BatonGuard)]
        [TestCase(EnemyId.H2_RifleGuard)]
        [TestCase(EnemyId.H6_Grenadier)]
        [TestCase(EnemyId.R2_Shooter)]
        public void AShotAcrossTheChestHits(EnemyId type)
        {
            Assert.Greater(ShotAt(type, 1.1f), 0f, "aimed at the drawn chest, well above the feet circle");
        }

        [Test]
        public void AShotOverTheHeadStillMisses()
        {
            Assert.AreEqual(0f, ShotAt(EnemyId.H2_RifleGuard, 2.6f));
        }

        [Test]
        public void TheBossIsHitAcrossItsWholeBody()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            var d = w.Profile.Boss(w.Phase);
            Vector2 feet = new(5f, 0f);
            float top = d.HitHeight + d.BodyRadius;
            Assert.Greater(SimMath.SegmentCapsuleHit(Vector2.zero, new Vector2(10f, top * 0.9f * 2f), feet, d.HitHeight, d.BodyRadius), 0f);
        }

        [Test]
        public void CapsuleHitsAlongItsRaisedBody()
        {
            Vector2 feet = new(5f, 0f);
            Assert.AreEqual(-1f, SimMath.SegmentCircleHit(new Vector2(0f, 1f), new Vector2(10f, 1f), feet, 0.4f), "the old feet circle misses");
            float t = SimMath.SegmentCapsuleHit(new Vector2(0f, 1f), new Vector2(10f, 1f), feet, 1f, 0.4f);
            Assert.AreEqual(0.46f, t, 1e-4f, "enters the raised body at its left side");
            Assert.AreEqual(-1f, SimMath.SegmentCapsuleHit(new Vector2(0f, 1.5f), new Vector2(10f, 1.5f), feet, 1f, 0.4f), "above the head");
            Assert.AreEqual(0f, SimMath.SegmentCapsuleHit(new Vector2(5f, 0.1f), new Vector2(6f, 0.1f), feet, 1f, 0.4f), "point blank at the feet");
        }

        [Test]
        public void AShotFiredAwayFromAHeadOverlappingTheShooterDoesNotHitIt()
        {
            // The enemy stands just below the shooter, so its raised body covers the muzzle.
            Vector2 feet = new(0f, -1f);
            Assert.AreEqual(-1f, SimMath.SegmentCapsuleHit(Vector2.zero, new Vector2(0f, 3f), feet, 1f, 0.4f), "shooting up, away from it");
            Assert.AreEqual(0f, SimMath.SegmentCapsuleHit(Vector2.zero, new Vector2(0f, -3f), feet, 1f, 0.4f), "shooting down into it");
        }

        [Test]
        public void EveryStandingHumanHasARaisedHitBody()
        {
            var p = Data.TestData.Baseline();
            foreach (var e in p.Enemies)
            {
                Assert.GreaterOrEqual(e.HitHeight, 0f, e.Id.ToString());
                if (e.Faction == Faction.Humans && e.Id != EnemyId.H7c_Turret)
                    Assert.Greater(e.HitHeight + e.BodyRadius, 1.2f, $"{e.Id} is drawn about 1.75 units tall");
            }
            foreach (var b in p.Bosses) Assert.Greater(b.HitHeight, 1f, b.Id.ToString());
        }
    }
}
