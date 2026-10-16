using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    public enum EnemyAi { Spawning, Chase, Windup, Attack, Recover, Charge, Support, Stunned }

    public enum TargetKind { Player, Companion }

    /// <summary>One live enemy. Plain data so a save restores its exact behaviour state (GDD §12).</summary>
    public sealed class EnemyState
    {
        public int Id;
        public EnemyId Type;
        public Vector2 Position;
        public Vector2 PrevPosition;
        public Vector2 Velocity;
        public Vector2 Facing = Vector2.down;
        public float Radius;
        public float Health;
        public float MaxHealth;
        public bool Alive = true;

        public EnemyAi Ai;
        public long AiUntilTick;
        public long NextAttackTick;
        public Vector2 AttackDir;
        public Vector2 AttackPoint;
        public TargetKind Target;
        public int BurstLeft;
        public long NextBurstTick;

        public Vector2 KnockbackVelocity;
        public long KnockbackUntilTick;
        public long StunnedUntilTick;
        public long EmpResistantUntilTick;
        public long SpeedBoostUntilTick;

        public float HealGiven;
        public int ChildrenProduced;
        public long NextProduceTick;
        public int ParentId;
        public int XpPaid;
        public int CoinsPaid;

        public long SpawnTick;
        /// <summary>0 = regular wave enemy; otherwise the boss encounter group it belongs to.</summary>
        public int EncounterGroup;

        public bool IsActive => Alive && Ai != EnemyAi.Spawning;
    }
}
