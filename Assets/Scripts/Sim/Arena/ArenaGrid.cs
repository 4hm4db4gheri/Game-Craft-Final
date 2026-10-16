using System;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// Collision against the arena's blocked cells: circle movement with sliding, push-out and segment casts.
    /// Everything outside the grid counts as blocked.
    /// </summary>
    public sealed class ArenaGrid
    {
        readonly bool[] blocked;

        public int Width { get; }
        public int Height { get; }
        public Vector2 Origin { get; }

        public ArenaGrid(int width, int height, Vector2 origin)
        {
            Width = width;
            Height = height;
            Origin = origin;
            blocked = new bool[width * height];
        }

        public static ArenaGrid FromLayout(ArenaLayout layout, int phase)
        {
            var g = new ArenaGrid(layout.Width, layout.Height, layout.Origin);
            foreach (int c in layout.BlockedFor(phase))
                if (c >= 0 && c < g.blocked.Length) g.blocked[c] = true;
            return g;
        }

        public void SetBlocked(int x, int y, bool value)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height) blocked[y * Width + x] = value;
        }

        public bool IsBlockedCell(int x, int y) =>
            x < 0 || y < 0 || x >= Width || y >= Height || blocked[y * Width + x];

        public Vector2Int CellOf(Vector2 p) =>
            new(Mathf.FloorToInt(p.x - Origin.x), Mathf.FloorToInt(p.y - Origin.y));

        public bool IsBlocked(Vector2 p)
        {
            var c = CellOf(p);
            return IsBlockedCell(c.x, c.y);
        }

        public Vector2 CellCenter(int x, int y) => Origin + new Vector2(x + 0.5f, y + 0.5f);

        public bool CircleOverlaps(Vector2 center, float radius)
        {
            ForCellsAround(center, radius, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!IsBlockedCell(x, y)) continue;
                Vector2 min = Origin + new Vector2(x, y);
                Vector2 closest = new(Mathf.Clamp(center.x, min.x, min.x + 1), Mathf.Clamp(center.y, min.y, min.y + 1));
                if ((center - closest).sqrMagnitude < radius * radius - 1e-6f) return true;
            }
            return false;
        }

        /// <summary>Pushes a circle out of any blocked cell it overlaps.</summary>
        public Vector2 Resolve(Vector2 center, float radius)
        {
            for (int iteration = 0; iteration < 4; iteration++)
            {
                bool moved = false;
                ForCellsAround(center, radius, out int x0, out int y0, out int x1, out int y1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!IsBlockedCell(x, y)) continue;
                    Vector2 min = Origin + new Vector2(x, y);
                    Vector2 max = min + Vector2.one;
                    Vector2 closest = new(Mathf.Clamp(center.x, min.x, max.x), Mathf.Clamp(center.y, min.y, max.y));
                    Vector2 diff = center - closest;
                    float d2 = diff.sqrMagnitude;
                    if (d2 >= radius * radius) continue;
                    if (d2 > 1e-10f)
                    {
                        float d = Mathf.Sqrt(d2);
                        center += diff / d * (radius - d);
                    }
                    else
                    {
                        // Centre inside the box: leave through the nearest face.
                        float left = center.x - min.x, right = max.x - center.x, down = center.y - min.y, up = max.y - center.y;
                        float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
                        if (m == left) center.x = min.x - radius;
                        else if (m == right) center.x = max.x + radius;
                        else if (m == down) center.y = min.y - radius;
                        else center.y = max.y + radius;
                    }
                    moved = true;
                }
                if (!moved) break;
            }
            return center;
        }

        /// <summary>
        /// Where to put something that appears or is teleported (spawns, drops, a phase change that puts a new
        /// obstacle on someone): the point itself if the circle fits there, otherwise the nearest free-cell centre
        /// where it fits. <see cref="Resolve"/> only fixes small overlaps and can fail deep inside a long obstacle.
        /// </summary>
        public Vector2 NearestFree(Vector2 p, float radius)
        {
            if (!CircleOverlaps(p, radius)) return p;
            var c = CellOf(p);
            Vector2 best = p;
            float bestD = float.MaxValue;
            int foundRing = -1, maxRing = Mathf.Max(Width, Height);
            // Rings are square; one more ring after the first hit catches a closer point on its straight edge.
            for (int ring = 0; ring <= maxRing && (foundRing < 0 || ring <= foundRing + 1); ring++)
            for (int y = c.y - ring; y <= c.y + ring; y++)
            for (int x = c.x - ring; x <= c.x + ring; x++)
            {
                if (Mathf.Max(Mathf.Abs(x - c.x), Mathf.Abs(y - c.y)) != ring || IsBlockedCell(x, y)) continue;
                var q = CellCenter(x, y);
                if (CircleOverlaps(q, radius)) continue;
                float d = (q - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = q; if (foundRing < 0) foundRing = ring; }
            }
            return best;
        }

        /// <summary>Moves a circle by delta, sliding along walls, in sub-steps short enough never to tunnel.</summary>
        public Vector2 MoveCircle(Vector2 from, Vector2 delta, float radius)
        {
            float length = delta.magnitude;
            if (length < 1e-7f) return Resolve(from, radius);
            float maxStep = Mathf.Max(0.05f, radius * 0.5f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / maxStep));
            Vector2 step = delta / steps;
            Vector2 p = from;
            for (int i = 0; i < steps; i++) p = Resolve(p + step, radius);
            return p;
        }

        /// <summary>First blocked cell hit by the segment a→b; t is the fraction along the segment (0..1).</summary>
        public bool SegmentHit(Vector2 a, Vector2 b, out float t)
        {
            t = 1f;
            Vector2 start = a - Origin, end = b - Origin;
            int x = Mathf.FloorToInt(start.x), y = Mathf.FloorToInt(start.y);
            if (IsBlockedCell(x, y)) { t = 0; return true; }
            Vector2 d = end - start;
            int stepX = Math.Sign(d.x), stepY = Math.Sign(d.y);
            float tDeltaX = stepX != 0 ? Mathf.Abs(1f / d.x) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? Mathf.Abs(1f / d.y) : float.PositiveInfinity;
            float tMaxX = stepX > 0 ? (x + 1 - start.x) * tDeltaX : stepX < 0 ? (start.x - x) * tDeltaX : float.PositiveInfinity;
            float tMaxY = stepY > 0 ? (y + 1 - start.y) * tDeltaY : stepY < 0 ? (start.y - y) * tDeltaY : float.PositiveInfinity;

            while (true)
            {
                float tNext;
                if (tMaxX < tMaxY) { x += stepX; tNext = tMaxX; tMaxX += tDeltaX; }
                else { y += stepY; tNext = tMaxY; tMaxY += tDeltaY; }
                if (tNext > 1f) return false;
                if (IsBlockedCell(x, y)) { t = tNext; return true; }
            }
        }

        public bool LineOfSight(Vector2 a, Vector2 b) => !SegmentHit(a, b, out _);

        void ForCellsAround(Vector2 c, float r, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = Mathf.FloorToInt(c.x - r - Origin.x);
            y0 = Mathf.FloorToInt(c.y - r - Origin.y);
            x1 = Mathf.FloorToInt(c.x + r - Origin.x);
            y1 = Mathf.FloorToInt(c.y + r - Origin.y);
        }
    }
}
