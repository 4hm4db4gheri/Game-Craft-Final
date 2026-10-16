using System.Collections.Generic;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>Uniform grid of enemy indices rebuilt each tick for neighbour queries (no per-query allocation).</summary>
    public sealed class SpatialHash
    {
        readonly float cellSize;
        readonly Dictionary<long, List<EnemyState>> cells = new();
        readonly Stack<List<EnemyState>> pool = new();

        public SpatialHash(float cellSize) { this.cellSize = cellSize; }

        public void Rebuild(List<EnemyState> enemies)
        {
            foreach (var list in cells.Values) { list.Clear(); pool.Push(list); }
            cells.Clear();
            foreach (var e in enemies)
            {
                if (!e.IsActive) continue;
                long key = Key(Cell(e.Position.x), Cell(e.Position.y));
                if (!cells.TryGetValue(key, out var list))
                {
                    list = pool.Count > 0 ? pool.Pop() : new List<EnemyState>(8);
                    cells[key] = list;
                }
                list.Add(e);
            }
        }

        /// <summary>Calls <paramref name="visit"/> for active enemies whose cell is within radius of the point.</summary>
        public void Query(Vector2 p, float radius, List<EnemyState> results)
        {
            results.Clear();
            int x0 = Cell(p.x - radius), x1 = Cell(p.x + radius), y0 = Cell(p.y - radius), y1 = Cell(p.y + radius);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (cells.TryGetValue(Key(x, y), out var list)) results.AddRange(list);
        }

        int Cell(float v) => Mathf.FloorToInt(v / cellSize);
        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
