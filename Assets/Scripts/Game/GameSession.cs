using System;
using Hollow.App;
using Hollow.Core;
using Hollow.Data;
using Hollow.Presentation;
using Hollow.Sim;
using UnityEngine;

namespace Hollow.Game
{
    /// <summary>
    /// Root of the Game scene: owns the simulation, steps it in fixed ticks with routed input,
    /// and hands the result to the views every frame.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] ArenaAuthoring arena;
        [SerializeField] WorldPresenter presenter;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] ReticleView reticle;

        GameApp app;
        SimDriver driver;

        public Simulation Sim { get; private set; }

        void Start()
        {
            app = GameApp.Ensure();
            app.EnterGameDirectly();
            app.WorldCamera = worldCamera;

            var layout = ArenaLayoutStore.LoadFromResources();
            ulong seed = (ulong)DateTime.UtcNow.Ticks;
            Sim = new Simulation(Simulation.NewWorld(app.Balance.Live, layout, seed));
            app.Balance.Applied += OnBalanceApplied;
            arena.ShowPhase(Sim.World.Phase);

            driver = new SimDriver(app.States, StepOnce);
            app.States.Transition(GameState.Playing);
        }

        void OnDestroy()
        {
            if (app != null && app.Balance != null) app.Balance.Applied -= OnBalanceApplied;
        }

        /// <summary>Live tuning: the next step already reads the new numbers.</summary>
        void OnBalanceApplied(BalanceProfile profile) => Sim.World.Profile = profile;

        void StepOnce() => Sim.Step(app.Router.Consume());

        void Update()
        {
            if (Sim == null) return;
            if (!app.ToolFreeze) driver.Update(Time.unscaledDeltaTime);

            var w = Sim.World;
            float alpha = driver.InterpolationAlpha;
            presenter.Sync(w, alpha);
            cameraRig.Sync(w, alpha);
            reticle.Sync(w.Player.Aim, app.Router.WorldAcceptsInput);
            w.Events.Clear();
        }
    }
}
