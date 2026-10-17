using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollow.Sim
{
    public enum SpawnSide { North, East, South, West }

    [Serializable]
    public sealed class SpawnZone
    {
        public SpawnSide Side;
        public Vector2 Min;
        public Vector2 Max;

        public Vector2 Center => (Min + Max) * 0.5f;
    }

    /// <summary>
    /// Baked arena layout (authored with Tilemaps in the Game scene, baked to JSON by an Editor tool) so the
    /// simulation, saves and headless tests all use exactly the same collision grid. Cells are 1 unit.
    /// </summary>
    [Serializable]
    public sealed class ArenaLayout
    {
        public int Width;
        public int Height;
        /// <summary>World position of the lower-left corner of cell (0,0).</summary>
        public Vector2 Origin;
        /// <summary>Cells blocked in every phase (outer walls).</summary>
        public List<int> BaseBlocked = new();
        /// <summary>Extra blocked cells per phase (index 0 = phase 1).</summary>
        public List<PhaseCells> PhaseBlocked = new();
        public List<SpawnZone> SpawnZones = new();
        public Vector2 PlayerStart;
        public Vector2 CompanionStart;
        /// <summary>First service station (kept for layouts baked before T28).</summary>
        public Vector2 ShopPosition;
        /// <summary>v0.19 §13: every service station; they share one wallet, ownership and stock.</summary>
        public List<Vector2> ShopPositions = new();

        /// <summary>The stations (falls back to <see cref="ShopPosition"/> for an old layout).</summary>
        public List<Vector2> Shops
        {
            get
            {
                if (ShopPositions.Count == 0) ShopPositions.Add(ShopPosition);
                return ShopPositions;
            }
        }

        public Vector2 NearestShop(Vector2 from)
        {
            var shops = Shops;
            Vector2 best = shops[0];
            for (int i = 1; i < shops.Count; i++)
                if ((shops[i] - from).sqrMagnitude < (best - from).sqrMagnitude) best = shops[i];
            return best;
        }

        public int CellIndex(int x, int y) => y * Width + x;

        public List<int> BlockedFor(int phase)
        {
            var cells = new List<int>(BaseBlocked);
            int i = phase - 1;
            if (i >= 0 && i < PhaseBlocked.Count) cells.AddRange(PhaseBlocked[i].Cells);
            return cells;
        }
    }

    [Serializable]
    public sealed class PhaseCells
    {
        public List<int> Cells = new();
    }
}
