using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>
    /// A named blocking obstacle of one phase (§14). Its footprint is what the arena bake turns into blocked
    /// cells, so moving the object in the scene and re-baking moves both the look and the collision.
    /// The object sits at the centre of the footprint's front (lower) edge — the art's ground point.
    /// </summary>
    public sealed class ArenaProp : MonoBehaviour
    {
        public int Phase = 1;
        public string PropName;
        /// <summary>Lower-left cell of the footprint in arena cell coordinates.</summary>
        public Vector2Int Cell;
        public Vector2Int Footprint = Vector2Int.one;

        public RectInt Rect => new(Cell, Footprint);
    }
}
