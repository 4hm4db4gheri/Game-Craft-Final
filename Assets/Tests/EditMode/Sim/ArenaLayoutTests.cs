using System.Collections.Generic;
using System.Linq;
using Hollow.Data;
using Hollow.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>
    /// T16 acceptance on the baked arena (§14): every phase keeps escape routes from every spawn zone, never has
    /// one-cell slots or closed pockets, and nobody is left inside a new obstacle when the phase changes.
    /// </summary>
    public class ArenaLayoutTests
    {
        static readonly int[] Phases = { 1, 2, 3 };

        static ArenaGrid Grid(int phase) => ArenaGrid.FromLayout(WaveTests.RealArena(), phase);

        static IEnumerable<Vector2Int> FreeCells(ArenaGrid g)
        {
            for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
                if (!g.IsBlockedCell(x, y)) yield return new Vector2Int(x, y);
        }

        static HashSet<Vector2Int> Flood(ArenaGrid g, Vector2Int start, System.Func<Vector2Int, bool> passable)
        {
            var seen = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>(seen);
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (n.x < 0 || n.y < 0 || n.x >= g.Width || n.y >= g.Height || !passable(n) || !seen.Add(n)) continue;
                    queue.Enqueue(n);
                }
            }
            return seen;
        }

        /// <summary>Lower-left cell of a free 2 × 2 square.</summary>
        static bool Wide(ArenaGrid g, Vector2Int c) =>
            c.x + 1 < g.Width && c.y + 1 < g.Height &&
            !g.IsBlockedCell(c.x, c.y) && !g.IsBlockedCell(c.x + 1, c.y) && !g.IsBlockedCell(c.x, c.y + 1) && !g.IsBlockedCell(c.x + 1, c.y + 1);

        [TestCaseSource(nameof(Phases))]
        public void EveryFreeCellIsReachableFromThePlayerStart(int phase)
        {
            var g = Grid(phase);
            var start = g.CellOf(WaveTests.RealArena().PlayerStart);
            var reached = Flood(g, start, c => !g.IsBlockedCell(c.x, c.y));
            var pockets = FreeCells(g).Where(c => !reached.Contains(c)).ToList();
            Assert.IsEmpty(pockets, $"phase {phase}: closed pockets at {string.Join(" ", pockets.Take(5))}");
        }

        [TestCaseSource(nameof(Phases))]
        public void NoOneCellWideSlotsAnywhere(int phase)
        {
            // Every free cell is part of a free 2 × 2 square, so groups, big enemies and the companion always fit
            // and nothing can corner the player in a slot.
            var g = Grid(phase);
            var slots = FreeCells(g).Where(c =>
                !Wide(g, c) && !Wide(g, c + Vector2Int.left) && !Wide(g, c + Vector2Int.down) && !Wide(g, c + Vector2Int.left + Vector2Int.down)).ToList();
            Assert.IsEmpty(slots, $"phase {phase}: one-cell slots at {string.Join(" ", slots.Take(5))}");
        }

        [TestCaseSource(nameof(Phases))]
        public void EverySpawnZoneIsOpenAndLeadsToTheCentreByWideRoutes(int phase)
        {
            var layout = WaveTests.RealArena();
            var g = Grid(phase);
            var start = g.CellOf(layout.PlayerStart);
            var wide = Flood(g, start, c => Wide(g, c));
            foreach (var zone in layout.SpawnZones)
            {
                var min = g.CellOf(zone.Min);
                var max = g.CellOf(zone.Max - Vector2.one * 0.01f);
                for (int y = min.y - 1; y <= max.y + 1; y++)
                for (int x = min.x - 1; x <= max.x + 1; x++)
                    Assert.IsFalse(g.IsBlockedCell(x, y), $"phase {phase}: {zone.Side} zone (or its margin) blocked at ({x},{y})");
                Assert.IsTrue(wide.Contains(g.CellOf(zone.Center)), $"phase {phase}: {zone.Side} zone has no wide route to the centre");
            }
        }

        [TestCaseSource(nameof(Phases))]
        public void StartsAndShopFrontStayClear(int phase)
        {
            var layout = WaveTests.RealArena();
            var g = Grid(phase);
            // The row in front of the shop (where the player stands to use it) and the row below it.
            var points = new System.Collections.Generic.List<Vector2> { layout.PlayerStart, layout.CompanionStart };
            foreach (var shop in layout.Shops) points.Add(shop + Vector2.down * 2f);
            Assert.AreEqual(2, layout.Shops.Count, "v0.19 §13: two service stations");
            foreach (var p in points)
            {
                var c = g.CellOf(p);
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    Assert.IsFalse(g.IsBlockedCell(c.x + dx, c.y + dy), $"phase {phase}: blocked next to {p}");
            }
        }

        [Test]
        public void PhasesChangeTheCoverNotJustThePalette()
        {
            var layout = WaveTests.RealArena();
            var sets = layout.PhaseBlocked.Select(p => new HashSet<int>(p.Cells)).ToList();
            Assert.AreEqual(3, sets.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.Greater(sets[i].Count, 10, $"phase {i + 1} has almost no cover");
                for (int j = i + 1; j < 3; j++)
                    Assert.Less(sets[i].Intersect(sets[j]).Count(), sets[i].Count / 2, $"phases {i + 1} and {j + 1} share most of their cover");
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        public void NobodyIsLeftInsideANewObstacleAtTheTransition(int phase)
        {
            var layout = WaveTests.RealArena();
            var before = new HashSet<int>(layout.BlockedFor(phase - 1));
            var fresh = layout.BlockedFor(phase).Where(c => !before.Contains(c)).ToList();
            Assert.IsNotEmpty(fresh);

            var sim = SimTestUtil.NewSim(layout, companion: true);
            var w = sim.World;
            if (phase == 3) PhaseSystem.EnterPhase(w, 2);
            foreach (int cell in fresh)
            {
                var at = w.Arena.CellCenter(cell % layout.Width, cell / layout.Width);
                w.Enemies.Clear();
                var enemy = w.AddEnemy(EnemyId.A1_RunnerWolf, at);
                w.Player.Position = w.Player.PrevPosition = at;
                w.Companion.Present = true;
                w.Companion.Position = w.Companion.PrevPosition = at;
                w.Coins.Clear();
                var coin = new CoinState { Position = at, PrevPosition = at };
                w.Coins.Add(coin);

                PhaseSystem.EnterPhase(w, phase);

                Assert.IsFalse(w.Arena.CircleOverlaps(w.Player.Position, w.Profile.Player.BodyRadius), $"player left inside cell {cell}");
                if (w.Companion.Present)
                    Assert.IsFalse(w.Arena.CircleOverlaps(w.Companion.Position, w.Profile.Companion.BodyRadius), $"companion left inside cell {cell}");
                Assert.IsFalse(w.Arena.CircleOverlaps(enemy.Position, enemy.Radius), $"enemy left inside cell {cell}");
                Assert.IsFalse(w.Arena.IsBlocked(coin.Position), $"coin left inside cell {cell}");
            }
        }

        [Test]
        public void NearestFreeLeavesADeepObstacleByTheShortestWay()
        {
            var g = new ArenaGrid(20, 20, Vector2.zero);
            for (int y = 8; y < 11; y++)
            for (int x = 5; x < 15; x++)
                g.SetBlocked(x, y, true); // a 10 × 3 block

            var at = g.NearestFree(new Vector2(10.5f, 9.5f), 0.4f);
            Assert.IsFalse(g.CircleOverlaps(at, 0.4f));
            Assert.AreEqual(10.5f, at.x, 1e-4f, "should leave straight up or down, not along the block");
            Assert.AreEqual(2f, Mathf.Abs(at.y - 9.5f), 1e-4f, "the first free row above or below");

            var free = new Vector2(2.3f, 2.7f);
            Assert.AreEqual(free, g.NearestFree(free, 0.4f), "a point that fits stays where it is");
        }
    }
}
