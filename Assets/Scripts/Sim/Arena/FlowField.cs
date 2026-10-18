using System.Collections.Generic;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// Shared path map toward one target (D5): one breadth-first pass over the arena grid when the target
    /// changes cell, then every enemy just reads the next step for its own cell.
    /// </summary>
    public sealed class FlowField
    {
        const int Unreached = int.MaxValue;

        readonly ArenaGrid grid;
        readonly int[] dist;
        readonly Queue<int> queue = new();
        Vector2Int targetCell = new(int.MinValue, int.MinValue);

        static readonly Vector2Int[] Orthogonal = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        static readonly Vector2Int[] AllDirs =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
        };

        public FlowField(ArenaGrid grid)
        {
            this.grid = grid;
            dist = new int[grid.Width * grid.Height];
        }

        public void SetTarget(Vector2 target)
        {
            var cell = grid.CellOf(target);
            if (cell == targetCell) return;
            targetCell = cell;
            for (int i = 0; i < dist.Length; i++) dist[i] = Unreached;
            if (grid.IsBlockedCell(cell.x, cell.y)) return;

            dist[cell.y * grid.Width + cell.x] = 0;
            queue.Clear();
            queue.Enqueue(cell.y * grid.Width + cell.x);
            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                int x = idx % grid.Width, y = idx / grid.Width;
                foreach (var d in Orthogonal)
                {
                    int nx = x + d.x, ny = y + d.y;
                    if (grid.IsBlockedCell(nx, ny)) continue;
                    int n = ny * grid.Width + nx;
                    if (dist[n] != Unreached) continue;
                    dist[n] = dist[idx] + 1;
                    queue.Enqueue(n);
                }
            }
        }

        /// <summary>The grid changed (a broken obstacle): rebuild on the next <see cref="SetTarget"/>.</summary>
        public void Invalidate() => targetCell = new Vector2Int(int.MinValue, int.MinValue);

        /// <summary>Direction to walk from <paramref name="from"/>: straight at the target when the way is clear, else the flow.</summary>
        public Vector2 Direction(Vector2 from, Vector2 target)
        {
            if (grid.LineOfSight(from, target)) return SimMath.SafeNormal(target - from, Vector2.zero);
            var c = grid.CellOf(from);
            if (c.x < 0 || c.y < 0 || c.x >= grid.Width || c.y >= grid.Height) return SimMath.SafeNormal(target - from, Vector2.zero);
            int here = dist[c.y * grid.Width + c.x];
            int best = here;
            Vector2Int bestDir = Vector2Int.zero;
            foreach (var d in AllDirs)
            {
                int nx = c.x + d.x, ny = c.y + d.y;
                if (grid.IsBlockedCell(nx, ny)) continue;
                // No corner cutting: a diagonal step needs both orthogonal cells free.
                if (d.x != 0 && d.y != 0 && (grid.IsBlockedCell(c.x + d.x, c.y) || grid.IsBlockedCell(c.x, c.y + d.y))) continue;
                int v = dist[ny * grid.Width + nx];
                if (v < best) { best = v; bestDir = d; }
            }
            if (bestDir == Vector2Int.zero) return SimMath.SafeNormal(target - from, Vector2.zero);
            return SimMath.SafeNormal(grid.CellCenter(c.x + bestDir.x, c.y + bestDir.y) - from, Vector2.zero);
        }
    }
}
