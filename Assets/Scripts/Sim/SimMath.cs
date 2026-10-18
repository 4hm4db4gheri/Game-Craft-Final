using UnityEngine;

namespace Hollow.Sim
{
    public static class SimMath
    {
        public static float SegmentPointDistanceSq(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-10f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return (a + ab * t - p).sqrMagnitude;
        }

        /// <summary>Fraction along a→b where the segment first touches the circle, or -1.</summary>
        public static float SegmentCircleHit(Vector2 a, Vector2 b, Vector2 center, float radius)
        {
            Vector2 d = b - a;
            Vector2 f = a - center;
            float A = Vector2.Dot(d, d);
            float B = 2 * Vector2.Dot(f, d);
            float C = Vector2.Dot(f, f) - radius * radius;
            if (C <= 0) return 0f; // starts inside
            if (A < 1e-10f) return -1f;
            float disc = B * B - 4 * A * C;
            if (disc < 0) return -1f;
            float t = (-B - Mathf.Sqrt(disc)) / (2 * A);
            return t >= 0 && t <= 1 ? t : -1f;
        }

        /// <summary>
        /// Fraction along a→b where the segment first touches an upright capsule: the circle at <paramref name="bottom"/>
        /// swept <paramref name="height"/> up the screen (3/4 view: a body stands above its feet), or -1.
        /// A segment that starts inside the raised part only hits when it heads into the body, so a shot fired
        /// away from an enemy whose head overlaps the shooter does not hit it.
        /// </summary>
        public static float SegmentCapsuleHit(Vector2 a, Vector2 b, Vector2 bottom, float height, float radius)
        {
            float feet = SegmentCircleHit(a, b, bottom, radius);
            if (height <= 0f || feet == 0f) return feet;
            Vector2 top = bottom + Vector2.up * height;
            float best = Earliest(feet, SegmentCircleHit(a, b, top, radius));
            best = Earliest(best, SegmentBoxHit(a, b, new Vector2(bottom.x - radius, bottom.y), new Vector2(bottom.x + radius, top.y)));
            if (best == 0f && Vector2.Dot(b - a, (bottom + top) * 0.5f - a) < 0f) return -1f;
            return best;
        }

        static float Earliest(float x, float y) => x < 0f ? y : y < 0f ? x : Mathf.Min(x, y);

        /// <summary>Fraction along a→b where the segment first enters the axis-aligned box, 0 if it starts inside, or -1.</summary>
        public static float SegmentBoxHit(Vector2 a, Vector2 b, Vector2 min, Vector2 max)
        {
            Vector2 d = b - a;
            float enter = 0f, exit = 1f;
            for (int i = 0; i < 2; i++)
            {
                float o = a[i], dir = d[i];
                if (Mathf.Abs(dir) < 1e-10f)
                {
                    if (o < min[i] || o > max[i]) return -1f;
                    continue;
                }
                float t0 = (min[i] - o) / dir, t1 = (max[i] - o) / dir;
                if (t0 > t1) (t0, t1) = (t1, t0);
                enter = Mathf.Max(enter, t0);
                exit = Mathf.Min(exit, t1);
                if (enter > exit) return -1f;
            }
            return enter;
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static Vector2 SafeNormal(Vector2 v, Vector2 fallback) => v.sqrMagnitude > 1e-8f ? v.normalized : fallback;
    }
}
