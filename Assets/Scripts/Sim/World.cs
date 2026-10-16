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
        public List<EnemyState> Enemies = new();

        [JsonIgnore] public BalanceProfile Profile;
        [JsonIgnore] public ArenaLayout Layout;
        [JsonIgnore] public ArenaGrid Arena;

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
        }

        public long Ticks(float seconds) => SimClock.ToTicks(seconds);
    }
}
