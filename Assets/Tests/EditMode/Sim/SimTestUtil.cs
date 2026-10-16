using System.Collections.Generic;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using Hollow.Tests.Data;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    public static class SimTestUtil
    {
        /// <summary>An open square arena (walls only on the border), centred on the origin.</summary>
        public static ArenaLayout OpenArena(int size = 40)
        {
            var layout = new ArenaLayout { Width = size, Height = size, Origin = new Vector2(-size / 2f, -size / 2f) };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (x == 0 || y == 0 || x == size - 1 || y == size - 1)
                    layout.BaseBlocked.Add(layout.CellIndex(x, y));
            layout.PhaseBlocked.Add(new PhaseCells());
            layout.PlayerStart = Vector2.zero;
            return layout;
        }

        public static Simulation NewSim(ArenaLayout layout = null, BalanceProfile profile = null, ulong seed = 1)
        {
            var world = Simulation.NewWorld(profile ?? TestData.Baseline(), layout ?? OpenArena(), seed);
            return new Simulation(world);
        }

        public static void Run(this Simulation sim, int ticks, PlayerCommand cmd)
        {
            for (int i = 0; i < ticks; i++) sim.Step(cmd);
        }

        public static PlayerCommand Move(Vector2 dir) => new() { Move = dir };

        public static EnemyState AddEnemy(this World w, EnemyId type, Vector2 position, float health = 1000f)
        {
            var e = new EnemyState
            {
                Id = w.AllocateId(), Type = type, Position = position, PrevPosition = position,
                Radius = w.Profile.Enemy(type).BodyRadius, Health = health, MaxHealth = health,
            };
            w.Enemies.Add(e);
            return e;
        }

        public static List<SimEvent> Drain(this World w)
        {
            var list = new List<SimEvent>(w.Events);
            w.Events.Clear();
            return list;
        }
    }
}
