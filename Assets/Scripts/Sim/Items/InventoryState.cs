using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    public sealed class ItemStack
    {
        public ItemId Id;
        public int Count;
    }

    public sealed class MineState
    {
        public int Id;
        public Vector2 Position;
        public long ArmedTick;
        public bool Alive = true;
    }

    /// <summary>
    /// Five item slots in a player-chosen order (saved, §10), the held (equipped, not yet used) item,
    /// the shared use gap, the personal shield and placed mines.
    /// </summary>
    public sealed class InventoryState
    {
        public List<ItemStack> Slots = new();

        public bool Holding;
        public ItemId Held;
        public long NextUseTick;
        public long ShieldUntilTick;
        public bool SuppressFireUntilRelease;
        public List<MineState> Mines = new();

        public ItemStack Get(ItemId id) => Slots.Find(s => s.Id == id);
        public int Count(ItemId id) => Get(id)?.Count ?? 0;

        public void Add(ItemId id, int count)
        {
            var s = Get(id);
            if (s == null) Slots.Add(s = new ItemStack { Id = id });
            s.Count += count;
        }

        /// <summary>Shop action "place item in first slot" for fast F access (§10).</summary>
        public void MoveToFirst(ItemId id)
        {
            var s = Get(id);
            if (s == null) return;
            Slots.Remove(s);
            Slots.Insert(0, s);
        }
    }
}
