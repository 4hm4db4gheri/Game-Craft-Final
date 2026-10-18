using System.Linq;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>D87: boss charges break obstacles; bosses call endless reward-free reinforcements.</summary>
    public class BossPressureTests
    {
        /// <summary>Open 40 × 40 arena with a 2 × 2 phase-one obstacle at world (-1..1, 5..7).</summary>
        static Simulation ArenaWithRock(out int[] rock, int phase = 1)
        {
            var layout = SimTestUtil.OpenArena();
            rock = new[] { layout.CellIndex(19, 25), layout.CellIndex(20, 25), layout.CellIndex(19, 26), layout.CellIndex(20, 26) };
            while (layout.PhaseBlocked.Count < phase) layout.PhaseBlocked.Add(new PhaseCells());
            layout.PhaseBlocked[phase - 1].Cells.AddRange(rock);
            var sim = SimTestUtil.NewSim(layout, companion: false);
            var w = sim.World;
            if (phase != 1) PhaseSystem.EnterPhase(w, phase);
            w.Tutorial = TutorialStage.None;
            w.Progress.ChoicesBlocked = true;
            w.Player.InvulnerableUntilTick = long.MaxValue;
            BossSystem.StartEncounter(w);
            w.Boss.AddQueue.Clear();
            return sim;
        }

        static void Charge(World w, Vector2 from, Vector2 dir)
        {
            var b = w.Boss;
            b.Position = b.PrevPosition = from;
            b.AttackDir = dir;
            b.ChargeLength = w.Profile.Boss(1).ChargeMaxDistance;
            b.ChargeTravelled = 0;
            b.ChargeHit = true;
            b.Ai = BossAi.Charging;
        }

        [Test]
        public void ChargeBreaksTheWholeObstacleAndKeepsGoing()
        {
            var sim = ArenaWithRock(out var rock);
            var w = sim.World;
            w.Player.Position = w.Player.PrevPosition = new Vector2(8, -6);
            Charge(w, new Vector2(0, 12), Vector2.down);
            w.Drain();
            sim.Run(80, PlayerCommand.None);

            Assert.Less(w.Boss.Position.y, 3f, "the charge went through where the rock was");
            CollectionAssert.AreEquivalent(rock, w.BrokenCells);
            foreach (int c in rock) Assert.IsFalse(w.Arena.IsBlockedCell(c % w.Arena.Width, c / w.Arena.Width));
            Assert.AreEqual(1, w.Drain().Count(e => e.Type == SimEventType.ObstacleBroken));

            // Saved, and still open after loading.
            var loaded = WorldSerializer.FromJson(WorldSerializer.ToJson(w), w.Profile, w.Layout);
            Assert.IsFalse(loaded.Arena.IsBlockedCell(19, 25));
        }

        public enum HeavyMove { Strike, PounceLanding, LeapLanding, RobotBlast, FormerCompanionGrenade }

        /// <summary>D89: every heavy move clears the obstacles in its zone, not only the charge.</summary>
        [TestCase(HeavyMove.Strike, 1)]
        [TestCase(HeavyMove.PounceLanding, 1)]
        [TestCase(HeavyMove.PounceLanding, 3)]
        [TestCase(HeavyMove.LeapLanding, 2)]
        [TestCase(HeavyMove.LeapLanding, 3)]
        [TestCase(HeavyMove.RobotBlast, 2)]
        [TestCase(HeavyMove.FormerCompanionGrenade, 3)]
        public void HeavyMovesBreakTheObstaclesInTheirZone(HeavyMove move, int phase)
        {
            var sim = ArenaWithRock(out var rock, phase);
            var w = sim.World;
            var b = w.Boss;
            var d = w.Profile.Boss(phase);
            w.Player.Position = w.Player.PrevPosition = new Vector2(10, -10);
            Vector2 near = new(0, 3.6f); // just below the rock at (-1..1, 5..7)
            long now = w.Clock.Tick;
            switch (move)
            {
                case HeavyMove.Strike:
                    b.Position = b.PrevPosition = near;
                    b.Ai = BossAi.StrikeWindup;
                    b.AiUntilTick = now;
                    break;
                case HeavyMove.PounceLanding:
                    b.LeapFrom = b.Position = b.PrevPosition = new Vector2(0, -4);
                    b.AttackPoint = near;
                    b.Ai = BossAi.Pouncing;
                    b.MoveStartTick = now;
                    b.AiUntilTick = now + 2;
                    break;
                case HeavyMove.LeapLanding:
                    b.Position = b.PrevPosition = b.AttackPoint = near;
                    b.Ai = BossAi.LeapLock;
                    b.MoveStartTick = now;
                    b.AiUntilTick = now + 2;
                    break;
                default:
                    b.Position = b.PrevPosition = new Vector2(0, -6);
                    b.Ai = BossAi.BlastRecover;
                    b.AiUntilTick = long.MaxValue;
                    HazardSystem.Create(w, HazardOwner.Enemy, b.Position, near, d.BlastRadius, d.Damage, 0.2f, BossState.EntityId);
                    break;
            }
            w.Drain();
            sim.Run(20, PlayerCommand.None);
            CollectionAssert.AreEquivalent(rock, w.BrokenCells, $"{move} cleared the rock");
            Assert.AreEqual(1, w.Drain().Count(e => e.Type == SimEventType.ObstacleBroken));
        }

        [Test]
        public void ShotsAndRegularEnemyBlastsBreakNothing()
        {
            var sim = ArenaWithRock(out _, 2);
            var w = sim.World;
            w.Player.Position = w.Player.PrevPosition = new Vector2(10, -10);
            w.Boss.Ai = BossAi.BlastRecover;
            w.Boss.AiUntilTick = long.MaxValue;
            HazardSystem.Create(w, HazardOwner.Enemy, Vector2.zero, new Vector2(0, 3.6f), 3f, 10f, 0.2f, sourceId: 12345);
            w.Projectiles.Add(new ProjectileState
            {
                Id = w.AllocateId(), Owner = ProjectileOwner.Enemy, SourceId = BossState.EntityId, Position = new Vector2(0, 2),
                PrevPosition = new Vector2(0, 2), Origin = new Vector2(0, 2), Velocity = Vector2.up * 10, Damage = 10, Range = 10, Radius = 0.2f,
            });
            sim.Run(30, PlayerCommand.None);
            Assert.IsEmpty(w.BrokenCells);
        }

        [Test]
        public void WallsAndStationsNeverBreak()
        {
            var sim = ArenaWithRock(out _);
            var w = sim.World;
            w.Player.Position = w.Player.PrevPosition = new Vector2(0, -6);
            Charge(w, new Vector2(12, 0), Vector2.right);
            sim.Run(120, PlayerCommand.None);
            Assert.IsEmpty(w.BrokenCells);
            Assert.IsTrue(w.Arena.IsBlockedCell(39, 20), "the outer wall stands");
            Assert.AreNotEqual(BossAi.Charging, w.Boss.Ai, "the wall still ends the charge");
        }

        [Test]
        public void CheckpointRetryBringsBrokenObstaclesBack()
        {
            var sim = ArenaWithRock(out _);
            var w = sim.World;
            w.Player.Position = w.Player.PrevPosition = new Vector2(8, -6);
            Charge(w, new Vector2(0, 12), Vector2.down);
            sim.Run(60, PlayerCommand.None);
            Assert.IsNotEmpty(w.BrokenCells);
            var retried = Checkpoints.Restore(w);
            Assert.IsEmpty(retried.BrokenCells);
            Assert.IsTrue(retried.Arena.IsBlockedCell(19, 25));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void BossCallsReinforcementsThatPayNothing(int phase)
        {
            var sim = SimTestUtil.NewSim(WaveTests.RealArena(), seed: 5, companion: false);
            var w = sim.World;
            if (phase != 1) PhaseSystem.EnterPhase(w, phase);
            w.Enemies.Clear();
            w.Tutorial = TutorialStage.None;
            w.Progress.ChoicesBlocked = true;
            w.Player.InvulnerableUntilTick = long.MaxValue;
            BossSystem.StartEncounter(w);
            w.Boss.AddQueue.Clear(); // skip the opening adds
            var d = w.Profile.Boss(phase);
            Assert.Greater(d.ReinforceInterval, 0f);

            int xp0 = w.Progress.TotalXp, coins0 = w.Progress.CoinsGenerated;
            sim.Run((int)w.Ticks(d.ReinforceInterval * 2 + 3), PlayerCommand.None);
            var reinforcements = w.Enemies.Where(e => e.Alive && e.EncounterGroup == BossState.ReinforcementGroup).ToList();
            Assert.GreaterOrEqual(reinforcements.Count, d.Reinforcements.Sum(c => c.Count), "at least one full group arrived");
            foreach (var e in reinforcements) Combat.DamageEnemy(w, e, 1e7f, DamageSource.Weapon, Vector2.right);
            Assert.AreEqual(xp0, w.Progress.TotalXp, "no XP from reinforcements");
            Assert.AreEqual(coins0, w.Progress.CoinsGenerated, "no coins from reinforcements");
        }
    }
}
