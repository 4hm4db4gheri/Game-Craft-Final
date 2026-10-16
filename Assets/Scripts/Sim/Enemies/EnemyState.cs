using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
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
    }
}
