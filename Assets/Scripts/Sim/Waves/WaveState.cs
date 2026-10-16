using System.Collections.Generic;
using Hollow.Data;

namespace Hollow.Sim
{
    public enum EncounterKind { NotStarted, Wave, SafeInterval, BossPrep, Boss, PhaseComplete }

    /// <summary>Everything the wave runner needs to resume exactly after a load (§12).</summary>
    public sealed class WaveState
    {
        public int Wave;
        public EncounterKind Kind;

        /// <summary>Remaining base queue in spawn order.</summary>
        public List<EnemyId> Queue = new();
        public List<int> BatchSizes = new();
        public int CurrentBatch;
        public int SpawnedInBatch;
        public int SpawnedTotal;
        public int PlannedTotal;

        public long NextSpawnActiveTick;
        public long SafeIntervalEndTick;
        public int ZoneCursor;

        /// <summary>Types introduced so far in this run and the hold that lets the player read a new type.</summary>
        public List<EnemyId> Introduced = new();
        public EnemyId HeldType;
        public long HeldUntilActiveTick;
        public long LastSupportIntroActiveTick = long.MinValue / 2;
        public long LastExplosiveIntroActiveTick = long.MinValue / 2;

        /// <summary>Wave settings captured at wave start (edits apply on the next wave start, §13).</summary>
        public int ActiveCap;
        public int RangedCap;
        public float SpawnGap;
        public List<float> HealthSnapshot = new();

        public bool BonusPaid;
    }
}
