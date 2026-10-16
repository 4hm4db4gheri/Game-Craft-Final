using System.Linq;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>T02 acceptance tests (GDD §4).</summary>
    public class PlayerTests
    {
        [Test]
        public void DiagonalSpeedEqualsStraightSpeed()
        {
            var straight = SimTestUtil.NewSim();
            straight.Run(60, SimTestUtil.Move(Vector2.right));
            var diagonal = SimTestUtil.NewSim();
            diagonal.Run(60, SimTestUtil.Move(new Vector2(1, 1)));

            float s = straight.World.Player.Velocity.magnitude;
            float d = diagonal.World.Player.Velocity.magnitude;
            Assert.AreEqual(5f, s, 1e-3f);
            Assert.AreEqual(s, d, 1e-3f);
            Assert.AreEqual(straight.World.Player.Position.magnitude, diagonal.World.Player.Position.magnitude, 1e-3f);
        }

        [Test]
        public void ReachesFullSpeedQuicklyAndStopsWithoutLongSlide()
        {
            var sim = SimTestUtil.NewSim();
            sim.Run(6, SimTestUtil.Move(Vector2.right)); // 0.1 s
            Assert.AreEqual(5f, sim.World.Player.Velocity.magnitude, 1e-3f, "full speed within 0.1 s");
            Vector2 before = sim.World.Player.Position;
            sim.Run(6, PlayerCommand.None);
            Assert.AreEqual(0f, sim.World.Player.Velocity.magnitude, 1e-4f, "stopped within 0.1 s");
            Assert.Less((sim.World.Player.Position - before).magnitude, 0.2f, "no long slide");
        }

        [Test]
        public void DashCoversDataDistanceInDataDuration()
        {
            var sim = SimTestUtil.NewSim();
            var data = sim.World.Profile.Player;
            var start = sim.World.Player.Position;
            sim.Step(new PlayerCommand { Move = Vector2.right, DashPressed = true });
            int ticks = 1;
            while (sim.World.Player.Dashing && ticks < 100) { sim.Step(new PlayerCommand { Move = Vector2.right }); ticks++; }

            Assert.AreEqual(PlayerSystem.DashTicks(sim.World), ticks);
            Assert.AreEqual(data.DashDuration, ticks / 60f, 1f / 60f);
            Assert.AreEqual(data.DashDistance, sim.World.Player.Position.x - start.x, 1e-3f);
        }

        [Test]
        public void DashFollowsMovementNotAimAndFallsBackToLastDirection()
        {
            var sim = SimTestUtil.NewSim();
            sim.Run(2, SimTestUtil.Move(Vector2.up));
            sim.Run(10, PlayerCommand.None);
            Vector2 before = sim.World.Player.Position;
            sim.Step(new PlayerCommand { DashPressed = true, AimWorld = new Vector2(10, 0) });
            sim.Run(15, new PlayerCommand { AimWorld = new Vector2(10, 0) });
            Vector2 moved = sim.World.Player.Position - before;
            Assert.Greater(moved.y, 2.9f);
            Assert.AreEqual(0f, moved.x, 1e-3f);
        }

        [Test]
        public void DashRespectsCooldown()
        {
            var sim = SimTestUtil.NewSim();
            sim.Step(new PlayerCommand { Move = Vector2.right, DashPressed = true });
            sim.Run(20, SimTestUtil.Move(Vector2.right));
            sim.Step(new PlayerCommand { Move = Vector2.right, DashPressed = true });
            Assert.IsFalse(sim.World.Player.Dashing, "second dash during cooldown is ignored");
            sim.Run(150, SimTestUtil.Move(Vector2.zero));
            sim.Step(new PlayerCommand { Move = Vector2.left, DashPressed = true });
            Assert.IsTrue(sim.World.Player.Dashing);
        }

        [Test]
        public void DashCannotCrossAWall()
        {
            var layout = SimTestUtil.OpenArena();
            // One-cell-thick vertical wall at world x in [1, 2).
            int wallX = Mathf.FloorToInt(1 - layout.Origin.x);
            for (int y = 1; y < layout.Height - 1; y++) layout.BaseBlocked.Add(layout.CellIndex(wallX, y));
            var sim = SimTestUtil.NewSim(layout);

            sim.Step(new PlayerCommand { Move = Vector2.right, DashPressed = true });
            sim.Run(20, SimTestUtil.Move(Vector2.right));

            float r = sim.World.Profile.Player.BodyRadius;
            Assert.LessOrEqual(sim.World.Player.Position.x, 1f - r + 1e-3f);
        }

        [Test]
        public void DashDamagesEachEnemyItPassesThroughExactlyOnce()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            var a = w.AddEnemy(EnemyId.A1_RunnerWolf, new Vector2(1.0f, 0));
            var b = w.AddEnemy(EnemyId.A1_RunnerWolf, new Vector2(2.0f, 0.2f));
            var far = w.AddEnemy(EnemyId.A1_RunnerWolf, new Vector2(1.5f, 3f));

            sim.Step(new PlayerCommand { Move = Vector2.right, DashPressed = true });
            sim.Run(20, SimTestUtil.Move(Vector2.right));

            float dash = w.Profile.Player.DashDamage;
            Assert.AreEqual(1000f - dash, a.Health, 1e-3f);
            Assert.AreEqual(1000f - dash, b.Health, 1e-3f);
            Assert.AreEqual(1000f, far.Health, 1e-3f);
            Assert.AreEqual(2, w.Events.Count(e => e.Type == SimEventType.EnemyHit));
        }

        [Test]
        public void DashMakesThePlayerInvulnerable()
        {
            var sim = SimTestUtil.NewSim();
            sim.Step(new PlayerCommand { Move = Vector2.right, DashPressed = true });
            Assert.IsFalse(PlayerSystem.Damage(sim.World, 10, Vector2.left));
            Assert.AreEqual(100f, sim.World.Player.Health);
        }

        [Test]
        public void PostHitInvulnerabilityNeverBlocksInput()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            Assert.IsTrue(PlayerSystem.Damage(w, 10, Vector2.left));
            Assert.IsFalse(PlayerSystem.Damage(w, 10, Vector2.left), "second hit inside 0.5 s is ignored");

            Vector2 before = w.Player.Position;
            sim.Run(5, SimTestUtil.Move(Vector2.up));
            Assert.Greater(w.Player.Position.y - before.y, 0.1f, "movement works while invulnerable");
            sim.Step(new PlayerCommand { Move = Vector2.right, DashPressed = true });
            Assert.IsTrue(w.Player.Dashing, "dash works while invulnerable");

            sim.Run(40, PlayerCommand.None);
            Assert.IsTrue(PlayerSystem.Damage(w, 10, Vector2.left), "vulnerable again after 0.5 s");
            Assert.AreEqual(80f, w.Player.Health, 1e-4f);
        }

        [Test]
        public void WalkingSlidesAlongWallsInsteadOfStopping()
        {
            var sim = SimTestUtil.NewSim();
            // Push diagonally into the top-right corner region: x must still advance while y is blocked.
            sim.World.Player.Position = new Vector2(0, 18.5f);
            sim.Run(30, SimTestUtil.Move(new Vector2(1, 1)));
            Assert.Greater(sim.World.Player.Position.x, 1.5f);
            Assert.LessOrEqual(sim.World.Player.Position.y, 19f - sim.World.Profile.Player.BodyRadius + 1e-3f);
        }
    }
}
