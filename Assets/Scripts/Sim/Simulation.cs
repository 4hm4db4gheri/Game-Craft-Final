using Hollow.Core.Input;
using Hollow.Data;

namespace Hollow.Sim
{
    /// <summary>Runs one fixed simulation step over a <see cref="World"/> in a fixed system order.</summary>
    public sealed class Simulation
    {
        public World World { get; private set; }

        public Simulation(World world)
        {
            World = world;
        }

        public static World NewWorld(BalanceProfile profile, ArenaLayout layout, ulong seed)
        {
            var w = new World();
            w.Rng.Reseed(seed);
            w.Bind(profile, layout);
            PlayerSystem.Spawn(w, layout.PlayerStart);
            WeaponSystem.Give(w, WeaponId.Pistol);
            CompanionSystem.SpawnForPhase(w, layout.CompanionStart);
            ShopSystem.RefreshStock(w);
            return w;
        }

        public void Step(in PlayerCommand cmd)
        {
            var w = World;
            PlayerSystem.Step(w, cmd);
            ShopSystem.Step(w, cmd);
            w.Crowd.Rebuild(w.Enemies);
            ItemSystem.Step(w, cmd);
            WeaponSystem.Step(w, cmd);
            WaveSystem.Step(w);
            CompanionSystem.Step(w);
            EnemySystem.Step(w);
            EnemySystem.Separate(w);
            ProjectileSystem.Step(w);
            HazardSystem.Step(w);
            CoinSystem.Step(w);
            XpSystem.Step(w);
            w.Enemies.RemoveAll(e => !e.Alive);
            w.Clock.Step(activeTimeRunning: !w.SafeInterval);
        }
    }
}
