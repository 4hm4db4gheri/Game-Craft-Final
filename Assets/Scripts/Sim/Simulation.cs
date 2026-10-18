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

        public static World NewWorld(BalanceProfile profile, ArenaLayout layout, ulong seed, AiId ai = AiId.None)
        {
            var w = new World();
            w.Rng.Reseed(seed);
            w.Seed = seed;
            w.Bind(profile, layout);
            PlayerSystem.Spawn(w, layout.PlayerStart);
            XpSystem.ApplyCharacter(w, ai);
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
            if (w.Tutorial == TutorialStage.Grenade) ShopSystem.EnsureGrenadeTarget(w);
            w.Crowd.Rebuild(w.Enemies);
            ItemSystem.Step(w, cmd);
            if (w.Tutorial == TutorialStage.Grenade || w.Tutorial == TutorialStage.GrenadeThrown)
            {
                // The training encounter waits for the deliberate throw; only its fuse keeps ticking.
                HazardSystem.Step(w);
                w.Clock.Step(activeTimeRunning: false);
                return;
            }
            WeaponSystem.Step(w, cmd);
            WaveSystem.Step(w);
            CompanionSystem.Step(w);
            EnemySystem.Step(w);
            BossSystem.Step(w);
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
