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
        static Simulation ArenaWithRock(out int[] rock)
        {
            var layout = SimTestUtil.OpenArena();
            rock = new[] { layout.CellIndex(19, 25), layout.CellIndex(20, 25), layout.CellIndex(19, 26), layout.CellIndex(20, 26) };
            layout.PhaseBlocked[0].Cells.AddRange(rock);
            var sim = SimTestUtil.NewSim(layout);
            var w = sim.World;
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
