using System;
using Hollow.App;
using Hollow.Core;
using Hollow.Data;
using Hollow.Presentation;
using Hollow.Sim;
using Hollow.UI;
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
        [SerializeField] UIRoot ui;

        HudView hud;
        XpCardScreen xpScreen;
        FailureScreen failureScreen;
        ShopPanel shopPanel;

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

            hud = ui.gameObject.AddComponent<HudView>();
            hud.Init(ui);
            xpScreen = ui.gameObject.AddComponent<XpCardScreen>();
            xpScreen.Init(ui);
            shopPanel = ui.gameObject.AddComponent<ShopPanel>();
            shopPanel.Init(ui, OnShopBuy, item => Sim.World.Inventory.MoveToFirst(item));
            failureScreen = ui.gameObject.AddComponent<FailureScreen>();
            failureScreen.Init(ui, RetryAfterFailure, RetryAfterFailure, () => app.GoToMainMenu());

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
            hud.Sync(w, worldCamera);
            SyncShop(w);
            w.Events.Clear();

            if (app.States.Current == GameState.Playing && w.Failure != FailureReason.None) { app.States.Transition(GameState.Failure); failureScreen.Show(w.Failure); }
            else if (app.States.Current == GameState.Playing && w.Progress.Pending != null) OpenXpChoice();
        }
    
        /// <summary>Until death checkpoints exist (T17) a retry restarts the run.</summary>
        void RetryAfterFailure()
        {
            app.States.Transition(GameState.Loading);
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Game);
        }

        // ---------------------------------------------------------------- shop (live, never pauses)

        void SyncShop(World w)
        {
            bool show = w.Shop.Open && (app.States.Current == GameState.Playing);
            if (show) { shopPanel.Bind(w); shopPanel.Show(w); }
            else if (shopPanel.Visible) shopPanel.Hide();
            hud.SetStatus(!w.Shop.Open && ShopSystem.InRange(w) && app.States.Current == GameState.Playing ? "Press E to open the shop" : null);
        }

        void OnShopBuy(ShopOffer offer)
        {
            if (app.States.Current != GameState.Playing) return;
            ShopSystem.Purchase(Sim.World, offer.Kind, offer.Weapon, offer.Item);
        }

        // ---------------------------------------------------------------- XP choice (world fully paused)

        void OpenXpChoice()
        {
            app.States.Transition(GameState.XPChoice);
            xpScreen.Show(Sim.World, Sim.World.Progress.Pending, OnSkillChosen);
        }

        void OnSkillChosen(Hollow.Data.SkillId id)
        {
            var w = Sim.World;
            if (app.States.Current != GameState.XPChoice || !XpSystem.Choose(w, id)) return;
            if (w.Progress.Pending != null)
            {
                xpScreen.Show(w, w.Progress.Pending, OnSkillChosen);
                return;
            }
            xpScreen.Hide();
            app.States.Transition(GameState.Playing);
        }
    }
}