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
    /// Carried item stacks, the one selected type shown in the HUD slot (v0.19 §8, saved), the held (equipped,
    /// not yet used) item, the shared use gap, the personal shield and placed mines.
    /// </summary>
    public sealed class InventoryState
    {
        public List<ItemStack> Slots = new();
        /// <summary>The type F equips; G moves it to the next type with stock. Set by the first purchase.</summary>
        public bool HasSelection;
        public ItemId Selected;

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
            if (!HasSelection && count > 0) { HasSelection = true; Selected = id; } // the first purchase selects
        }
    }
}
