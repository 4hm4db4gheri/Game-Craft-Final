using System.Collections.Generic;
using Hollow.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Hollow.Presentation
{
    /// <summary>
    /// Arena authoring in the Game scene: the designer paints Tilemaps and moves the markers; the Editor bakes
    /// them into Resources/Arena/ArenaLayout.json, which is what the simulation uses (D13).
    /// Walls: blocked in every phase. Obstacles[i]: extra blocked cells in phase i+1.
    /// </summary>
    public sealed class ArenaAuthoring : MonoBehaviour
    {
        public Grid Grid;
        public Tilemap Ground;
        public Tilemap Walls;
        public Tilemap[] Obstacles = new Tilemap[3];
        public Vector2Int Size = new(48, 32);

        public Transform PlayerStart;
        public Transform CompanionStart;
        public Transform Shop;
        public Vector2Int ShopFootprint = new(2, 1);

        public List<SpawnZone> SpawnZones = new();

        /// <summary>Shows only the obstacle layer of the given phase.</summary>
        public void ShowPhase(int phase)
        {
            for (int i = 0; i < Obstacles.Length; i++)
                if (Obstacles[i] != null) Obstacles[i].gameObject.SetActive(i == phase - 1);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            foreach (var z in SpawnZones)
                Gizmos.DrawCube((Vector3)z.Center, (Vector3)(z.Max - z.Min));
        }
    }
}
