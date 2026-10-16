using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    public enum ProjectileOwner { Player, Enemy, Companion }

    public sealed class ProjectileState
    {
        public int Id;
        public ProjectileOwner Owner;
        public int SourceId;
        public Vector2 Position;
        public Vector2 PrevPosition;
        public Vector2 Origin;
        public Vector2 Velocity;
        public float Damage;
        public float Range;
        public float Radius;
        /// <summary>Damage multiplier reached at maximum range (shotguns ≈ 0.5, others 1).</summary>
        public float FalloffAtMaxRange = 1f;
        public int TargetsLeft = 1;
        public bool IgnoresArmor;
        public bool Alive = true;
        public WeaponId Weapon;
        public EnemyId EnemyType;
        public List<int> HitIds = new();
    }
}
