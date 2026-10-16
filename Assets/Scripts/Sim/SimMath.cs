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

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static Vector2 SafeNormal(Vector2 v, Vector2 fallback) => v.sqrMagnitude > 1e-8f ? v.normalized : fallback;
    }
}
