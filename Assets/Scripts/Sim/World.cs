using System.Collections.Generic;
using Hollow.Core;
using Hollow.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// The complete gameplay state. Everything serialisable here is saved exactly (GDD §12);
    /// [JsonIgnore] members are rebuilt on load (profile, arena grid) or are presentation-only (events).
    /// </summary>
    public sealed class World
    {
        public SimClock Clock = new();
        public SimRandom Rng = new(1);
        public int Phase = 1;
        public int NextEntityId = 1;

        /// <summary>True between waves and in other safe moments: active gameplay seconds stop (G4).</summary>
        public bool SafeInterval;

        public PlayerState Player = new();
        public CompanionState Companion = new();
        public FailureReason Failure;
        public List<EnemyState> Enemies = new();
        public List<ProjectileState> Projectiles = new();
        public List<CoinState> Coins = new();
        public List<HazardState> Hazards = new();
        public ProgressState Progress = new();
        public InventoryState Inventory = new();
        public ShopState Shop = new();
        public TutorialStage Tutorial;
        public WaveState Waves = new();

        /// <summary>Death checkpoint: the world as it was at the start of the current wave or boss fight.</summary>
        public string CheckpointJson;
        public int Retries;
        /// <summary>The run reached the ending; continuing shows results, never an endless mode (§11).</summary>
        public bool Completed;

        [JsonIgnore] public BalanceProfile Profile;
        [JsonIgnore] public ArenaLayout Layout;
        [JsonIgnore] public ArenaGrid Arena;
        [JsonIgnore] public FlowField PlayerField;
        [JsonIgnore] public FlowField CompanionField;
        [JsonIgnore] public SpatialHash Crowd;

        /// <summary>Accumulates until the presentation layer drains it.</summary>
        [JsonIgnore] public readonly List<SimEvent> Events = new();

        public int AllocateId() => NextEntityId++;

        public void Emit(SimEventType type, int entityId, Vector2 position, Vector2 direction = default, float value = 0, int code = 0) =>
            Events.Add(new SimEvent(type, entityId, position, direction, value, code));

        /// <summary>Attach the non-saved context after creating or loading a world.</summary>
        public void Bind(BalanceProfile profile, ArenaLayout layout)
        {
            Profile = profile;
            Layout = layout;
            Arena = ArenaGrid.FromLayout(layout, Phase);
            PlayerField = new FlowField(Arena);
            CompanionField = new FlowField(Arena);
            Crowd = new SpatialHash(2f);
        }

        public long Ticks(float seconds) => SimClock.ToTicks(seconds);
    }
}
