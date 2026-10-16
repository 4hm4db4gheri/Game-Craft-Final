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
        public Vector2Int Size = new(48, 32);

        public Transform PlayerStart;
        public Transform CompanionStart;
        public Transform Shop;
        public Vector2Int ShopFootprint = new(2, 1);

        public List<SpawnZone> SpawnZones = new();

        /// <summary>Shows the ground, decor, props and obstacle layer of the given phase only.</summary>
        public void ShowPhase(int phase)
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = i == phase - 1;
                if (Grounds.Length > i && Grounds[i] != null) Grounds[i].gameObject.SetActive(on);
                if (Obstacles.Length > i && Obstacles[i] != null) Obstacles[i].gameObject.SetActive(on);
                if (Props.Length > i && Props[i] != null) Props[i].gameObject.SetActive(on);
                if (Decor.Length > i && Decor[i] != null) Decor[i].gameObject.SetActive(on);
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
