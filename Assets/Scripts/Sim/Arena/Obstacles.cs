using System.Collections.Generic;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// D87: a boss charge breaks the obstacles in its way. Only the phase's obstacles and set pieces break — never
    /// the outer walls or a service station. A broken obstacle goes as a whole (the connected blocked cells it was
    /// made of), stays broken for the rest of the phase (saved in <see cref="World.BrokenCells"/>) and the path maps
    /// are rebuilt around the new opening.
    /// </summary>
    public static class Obstacles
    {
        static readonly Vector2Int[] Orthogonal = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        static readonly HashSet<int> breakable = new();
        static readonly HashSet<int> fixedCells = new();
        static readonly Queue<int> queue = new();
        static readonly List<int> found = new();

        /// <summary>Clears the obstacles a boss body at <paramref name="center"/> would touch. Returns the cells broken.</summary>
        public static int BreakAround(World w, Vector2 center, float radius)
        {
            var grid = w.Arena;
            int x0 = Mathf.FloorToInt(center.x - radius - grid.Origin.x), x1 = Mathf.FloorToInt(center.x + radius - grid.Origin.x);
            int y0 = Mathf.FloorToInt(center.y - radius - grid.Origin.y), y1 = Mathf.FloorToInt(center.y + radius - grid.Origin.y);
            int broken = 0;
            bool collected = false;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (x < 0 || y < 0 || x >= grid.Width || y >= grid.Height || !grid.IsBlockedCell(x, y)) continue;
                Vector2 min = grid.Origin + new Vector2(x, y);
                Vector2 closest = new(Mathf.Clamp(center.x, min.x, min.x + 1), Mathf.Clamp(center.y, min.y, min.y + 1));
                if ((center - closest).sqrMagnitude >= radius * radius) continue;
                if (!collected) { Collect(w); collected = true; }
                broken += BreakFrom(w, y * grid.Width + x);
            }
            if (broken > 0)
            {
                w.PlayerField.Invalidate();
                w.CompanionField.Invalidate();
            }
            return broken;
        }

        /// <summary>The cells a charge may break this phase: phase obstacles and set pieces minus walls and stations.</summary>
        static void Collect(World w)
        {
            fixedCells.Clear();
            fixedCells.UnionWith(w.Layout.BaseBlocked);
            breakable.Clear();
            foreach (int c in w.Layout.BlockedFor(w.Phase))
                if (!fixedCells.Contains(c)) breakable.Add(c);
        }

        static int BreakFrom(World w, int start)
        {
            var grid = w.Arena;
            if (!breakable.Contains(start) || w.BrokenCells.Contains(start)) return 0;
            found.Clear();
            queue.Clear();
            queue.Enqueue(start);
            breakable.Remove(start);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                found.Add(c);
                int x = c % grid.Width, y = c / grid.Width;
                foreach (var d in Orthogonal)
                {
                    int nx = x + d.x, ny = y + d.y;
                    if (nx < 0 || ny < 0 || nx >= grid.Width || ny >= grid.Height) continue;
                    int n = ny * grid.Width + nx;
                    if (!breakable.Remove(n) || w.BrokenCells.Contains(n)) continue;
                    queue.Enqueue(n);
                }
            }
            Vector2 sum = Vector2.zero;
            foreach (int c in found)
            {
                int x = c % grid.Width, y = c / grid.Width;
                grid.SetBlocked(x, y, false);
                w.BrokenCells.Add(c);
                sum += grid.CellCenter(x, y);
            }
            w.Emit(SimEventType.ObstacleBroken, 0, sum / found.Count, default, found.Count);
            return found.Count;
        }
    }
}
