using UnityEngine;

namespace Hollow.Sim
{
    public enum SimEventType
    {
        PlayerDashStarted,
        PlayerDashEnded,
        PlayerHit,
        PlayerDied,
        EnemyHit,
        EnemyDied,
        EnemySpawned,
        EnemyTelegraph,
        EnemyAttack,
        ShotFired,
        ProjectileImpact,
        ReloadStarted,
        ReloadFinished,
        ReloadCancelled,
        WeaponSwapped,
        MeleeSwing,
        CoinDropped,
        CoinCollected,
        XpGained,
        CompanionHit,
        CompanionDied,
        Explosion,
    }

    /// <summary>
    /// Something that happened during a simulation step, for presentation, audio and telemetry only.
    /// Events are never saved and never read back by gameplay.
    /// </summary>
    public struct SimEvent
    {
        public SimEventType Type;
        public int EntityId;
        public Vector2 Position;
        public Vector2 Direction;
        public float Value;
        public int Code;

        public SimEvent(SimEventType type, int entityId, Vector2 position, Vector2 direction = default, float value = 0, int code = 0)
        {
            Type = type; EntityId = entityId; Position = position; Direction = direction; Value = value; Code = code;
        }
    }
}
