using System.Collections.Generic;
using Hollow.Data;

namespace Hollow.Sim
{
    public sealed class ItemStack
    {
        public ItemId Id;
        public int Count;
    }

    /// <summary>Five item slots in a player-chosen order (the order is saved, §10).</summary>
    public sealed class InventoryState
    {
        public List<ItemStack> Slots = new();

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
