using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    public sealed class PlayerState
    {
        public const int EntityId = 0;

        public Vector2 Position;
        public Vector2 PrevPosition;
        public Vector2 Velocity;
        public Vector2 Aim;
        public Vector2 LastMoveDir = Vector2.down;

        public float Health;
        public bool Dead;
        public long InvulnerableUntilTick;

        public bool Dashing;
        public Vector2 DashDir;
        public long DashEndTick;
        public long DashReadyTick;
        public List<int> DashHitIds = new();

        public LoadoutState Loadout = new();

        /// <summary>Rank taken per skill (index = (int)SkillId), 0 = not taken.</summary>
        /// <summary>The chosen AI (v0.19 §3); kept in saves, never chosen again on load.</summary>
        public AiId Ai;
        public int[] SkillRanks = new int[System.Enum.GetValues(typeof(SkillId)).Length];

        public int SkillRank(SkillId id) => SkillRanks[(int)id];
    }
}
