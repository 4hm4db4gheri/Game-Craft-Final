using System.Collections.Generic;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>Cached fan meshes for melee swing arcs and melee telegraphs (exact arc angle and reach).</summary>
    public static class ArcMesh
    {
        static readonly Dictionary<int, Mesh> Cache = new();

        // Enter Play Mode skips the domain reload (D72): meshes from the last session are destroyed, so start empty.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Cache.Clear();

        /// <summary>A unit-radius fan of the given angle, centred on +X.</summary>
        public static Mesh Get(float arcDegrees)
        {
            int key = Mathf.RoundToInt(arcDegrees);
            if (Cache.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            int segments = Mathf.Max(4, key / 6);
            var verts = new Vector3[segments + 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Deg2Rad * (-key * 0.5f + key * i / (float)segments);
                verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                uvs[i + 1] = new Vector2(0.5f, 0.5f);
            }
            for (int i = 0; i < segments; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 2; tris[i * 3 + 2] = i + 1; }
            mesh = new Mesh { name = $"Arc{key}", vertices = verts, uv = uvs, triangles = tris };
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }
    }
}
