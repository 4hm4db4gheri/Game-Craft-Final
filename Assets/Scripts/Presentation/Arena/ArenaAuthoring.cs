using System.Collections.Generic;
using Hollow.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Hollow.Presentation
{
    /// <summary>
    /// Arena authoring in the Game scene: the designer paints Tilemaps, moves props and markers; the Editor bakes
    /// them into Resources/Arena/ArenaLayout.json, which is what the simulation uses (D13).
    /// Walls: blocked in every phase. Per phase i+1: ground layer, decor, props (blocking) and an optional
    /// obstacle Tilemap for extra painted blocking cells.
    /// </summary>
    public sealed class ArenaAuthoring : MonoBehaviour
    {
        public Grid Grid;
        public Tilemap Walls;
        public Tilemap[] Grounds = new Tilemap[3];
        public Tilemap[] Obstacles = new Tilemap[3];
        public Transform[] Props = new Transform[3];
        public Transform[] Decor = new Transform[3];
        /// <summary>Imported border art around the walls, per phase (visual only; the Walls Tilemap blocks).</summary>
        public Transform[] Borders = new Transform[3];
        public Vector2Int Size = new(48, 32);

        public Transform PlayerStart;
        public Transform CompanionStart;
        public Transform Shop;
        /// <summary>More service stations (v0.19 §13: two by default; count and place are the artist's).</summary>
        public List<Transform> ExtraShops = new();
        public Vector2Int ShopFootprint = new(2, 1);

        public IEnumerable<Transform> AllShops()
        {
            if (Shop != null) yield return Shop;
            foreach (var s in ExtraShops) if (s != null) yield return s;
        }

        public List<SpawnZone> SpawnZones = new();

        /// <summary>Shows the ground, decor, props, border and obstacle layer of the given phase only.</summary>
        public void ShowPhase(int phase)
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = i == phase - 1;
                if (Grounds.Length > i && Grounds[i] != null) Grounds[i].gameObject.SetActive(on);
                if (Obstacles.Length > i && Obstacles[i] != null) Obstacles[i].gameObject.SetActive(on);
                if (Props.Length > i && Props[i] != null) Props[i].gameObject.SetActive(on);
                if (Decor.Length > i && Decor[i] != null) Decor[i].gameObject.SetActive(on);
                if (Borders != null && Borders.Length > i && Borders[i] != null) Borders[i].gameObject.SetActive(on);
            }
        }

        /// <summary>Breaks the exact 8-by-8 art repeat with smooth, non-periodic ground brightness.</summary>
        public void ApplyGroundVariation(float strength)
        {
            strength = Mathf.Clamp(strength, 0f, 0.22f);
            foreach (var map in Grounds)
            {
                if (map == null) continue;
                var bounds = map.cellBounds;
                foreach (var cell in bounds.allPositionsWithin)
                {
                    if (!map.HasTile(cell)) continue;
                    float broad = Mathf.PerlinNoise((cell.x + 43f) * 0.075f, (cell.y + 97f) * 0.075f);
                    float detail = Mathf.PerlinNoise((cell.x + 211f) * 0.19f, (cell.y + 17f) * 0.19f);
                    float value = 1f + ((broad * 0.75f + detail * 0.25f) - 0.5f) * 2f * strength;
                    map.SetTileFlags(cell, TileFlags.None);
                    map.SetColor(cell, new Color(value, value, value, 1f));
                }
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            foreach (var z in SpawnZones)
                Gizmos.DrawCube((Vector3)z.Center, (Vector3)(z.Max - z.Min));
        }
    }
}
