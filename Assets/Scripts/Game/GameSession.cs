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
    /// Root of the Game scene: owns the simulation, steps it in fixed ticks with routed input, hands the
    /// result to the views every frame, and runs the in-game flow (XP card, shop, pause, save/load,
    /// failure and checkpoint retry).
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] ArenaAuthoring arena;
        [SerializeField] ArenaAtmosphere atmosphere;
        [SerializeField] WorldPresenter presenter;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] ReticleView reticle;
        [SerializeField] UIRoot ui;

        HudView hud;
        XpCardScreen xpScreen;
        FailureScreen failureScreen;
        ShopPanel shopPanel;
        MenuScreens menus;
        DialogueView dialogue;
        EndingScreen ending;
        float phaseCompleteAt = -1f;
        readonly SettingsAdapter settings = new();

        GameApp app;
        SimDriver driver;
        ArenaLayout layout;
        string lastSavedHash;
        string flashMessage;
        float flashUntil;

        public Simulation Sim { get; private set; }
        World W => Sim.World;

        void Start()
        {
            app = GameApp.Ensure();
            app.EnterGameDirectly();
            app.WorldCamera = worldCamera;
            layout = ArenaLayoutStore.LoadFromResources();

            hud = ui.gameObject.AddComponent<HudView>();
            hud.Init(ui);
            hud.ItemClicked = id => { if (app.States.Current == GameState.Playing) ItemSystem.Equip(W, id); };
            xpScreen = ui.gameObject.AddComponent<XpCardScreen>();
            xpScreen.Init(ui);
            shopPanel = ui.gameObject.AddComponent<ShopPanel>();
            shopPanel.Init(ui, OnShopBuy, item => W.Inventory.MoveToFirst(item));
            failureScreen = ui.gameObject.AddComponent<FailureScreen>();
            failureScreen.Init(ui, RetryFromCheckpoint, () => ShowSlotLoad(fromFailure: true), () => app.GoToMainMenu());
            menus = ui.gameObject.AddComponent<MenuScreens>();
            menus.Init(ui);
            dialogue = ui.gameObject.AddComponent<DialogueView>();
            dialogue.Init(ui, DialogueData.Load());
            ending = ui.gameObject.AddComponent<EndingScreen>();
            ending.Init(ui);

            app.Balance.Applied += OnBalanceApplied;
            driver = new SimDriver(app.States, StepOnce);

            if (app.Launch == LaunchMode.LoadSlot) LoadFromSlot(app.ActiveSlot);
            else StartNewGame();
        }

        void OnDestroy()
        {
            if (app != null && app.Balance != null) app.Balance.Applied -= OnBalanceApplied;
        }

        /// <summary>Live tuning: the next step already reads the new numbers.</summary>
        void OnBalanceApplied(BalanceProfile profile)
        {
            if (Sim == null) return;
            W.Profile = profile;
            if (!W.Completed) atmosphere.SetPhase(profile.Atmosphere, W.Phase, instant: false);
        }

        // ---------------------------------------------------------------- start / load

        void StartNewGame()
        {
            SetWorld(Simulation.NewWorld(app.Balance.Live, layout, DevPanel.FixedSeed ?? (ulong)DateTime.UtcNow.Ticks));
            WaveSystem.StartWave(W, 1);
            SaveNow(quiet: true); // claims the chosen slot (replacement was confirmed in the picker)
            app.States.Transition(GameState.Scene);
            PlayScene("Opening", () => app.States.Transition(GameState.Playing));
        }

        void LoadFromSlot(int slot)
        {
            try
            {
                var save = SlotStore.Read(slot);
                app.Balance.ResetLive(save.Profile);
                var world = save.LoadWorld(layout);
                world.Profile = app.Balance.Live;
                SetWorld(world);
                lastSavedHash = WorldSerializer.Hash(W);
                if (W.Completed)
                {
                    // A completed slot shows its results; it never starts an endless mode (§11).
                    app.States.Transition(GameState.Ending);
                    ShowResults(montage: false);
                    return;
                }
                menus.ShowLoading(MenuScreens.Describe(save.Summary), () =>
                {
                    menus.Hide();
                    app.States.Transition(GameState.Playing);
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hollow] Load of slot {slot} failed: {e}");
                menus.ShowConfirm("Load failed", $"Slot {slot} could not be read.\n{e.Message}", ("Main menu", () =>
                {
                    app.States.Transition(GameState.MainMenu);
                    UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.MainMenu);
                }));
            }
        }

        void SetWorld(World world)
        {
            Sim = new Simulation(world);
            presenter.ResetViews();
            arena.ShowPhase(world.Phase);
            atmosphere.SetPhase(world.Profile.Atmosphere, world.Phase, instant: true);
        }

        void StepOnce() => Sim.Step(app.Router.Consume());

        /// <summary>Dev tools changed the phase directly: show the matching obstacle layout.</summary>
        public void OnPhaseChangedByTool()
        {
            arena.ShowPhase(W.Phase);
            atmosphere.SetPhase(W.Profile.Atmosphere, W.Phase, instant: true);
            presenter.ResetViews();
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            if (Sim == null) return;
            HandleKeys();

            var state = app.States.Current;
            if (state == GameState.Playing && W.Failure == FailureReason.None) EncounterFlow(W);
            state = app.States.Current;
            if (state == GameState.Playing && W.Failure != FailureReason.None)
            {
                app.States.Transition(GameState.Failure);
                shopPanel.Hide();
                failureScreen.Show(W.Failure);
            }
            else if (state == GameState.Playing && W.Progress.Pending != null) OpenXpChoice();

            if (!app.ToolFreeze) driver.Update(Time.unscaledDeltaTime);

            var w = W;
            float alpha = driver.InterpolationAlpha;
            presenter.Sync(w, alpha);
            cameraRig.Sync(w, alpha);
            reticle.Sync(w.Player.Aim, app.Router.WorldAcceptsInput);
            hud.Sync(w, worldCamera);
            SyncShop(w);
            SyncEncounter(w);
            w.Events.Clear();
        }

        void HandleKeys()
        {
            var input = app.InputSource;
            var state = app.States.Current;
            if (dialogue.Playing)
            {
                if (input.TakeConfirm()) dialogue.Next();
                if (input.TakePause()) dialogue.Skip();
                return;
            }
            if (input.TakePause())
            {
                if (state == GameState.Playing || state == GameState.XPChoice || state == GameState.BossPrep) OpenPause();
                else if (state == GameState.Paused) ClosePause();
            }
            if (input.TakeConfirm() && state == GameState.Playing) OnConfirm();
        }

        // ---------------------------------------------------------------- waves

        void SyncEncounter(World w)
        {
            var ws = w.Waves;
            string top = ws.Kind switch
            {
                EncounterKind.SafeInterval => $"Phase {w.Phase} · Wave {ws.Wave}/{WaveSystem.WaveCount(w)} cleared — next wave in {Mathf.CeilToInt(WaveSystem.SafeIntervalRemaining(w))} s  (Enter: start now)",
                EncounterKind.BossPrep => $"Phase {w.Phase} · boss encounter ready — shop, then press Enter when ready",
                EncounterKind.Boss => $"Phase {w.Phase} · boss encounter · Enemies {WaveSystem.AliveCount(w)}",
                EncounterKind.PhaseComplete => $"Phase {w.Phase} · encounter won",
                _ => $"Phase {w.Phase} · Wave {ws.Wave}/{WaveSystem.WaveCount(w)} · Enemies {WaveSystem.AliveCount(w)}",
            };
            hud.SetTop(top);
            var b = w.Boss;
            hud.SetBoss(b.Active ? BossName(b.Id) : null, b.Active ? b.Health / b.MaxHealth : 0);
        }

        static string BossName(BossId id) => id switch
        {
            BossId.LargeBeast => "The large beast",
            BossId.IndustrialRobot => "The industrial robot",
            _ => "The Operator (body and equipment)",
        };

        void OnConfirm()
        {
            var w = W;
            if (w.Waves.Kind == EncounterKind.SafeInterval) WaveSystem.SkipSafeInterval(w);
            else if (w.Waves.Kind == EncounterKind.BossPrep && w.Narrative.PreparingAtShop) BeginBossEntrance();
        }

        // ---------------------------------------------------------------- boss preparation, transitions, ending

        /// <summary>Runs the encounter-level flow while playing (§7): boss preparation prompt and phase end.</summary>
        void EncounterFlow(World w)
        {
            if (w.Waves.Kind == EncounterKind.BossPrep && !w.Narrative.PreparingAtShop)
            {
                app.States.Transition(GameState.BossPrep);
                shopPanel.Hide();
                ShowBossPrompt();
                return;
            }

            if (w.Waves.Kind != EncounterKind.PhaseComplete) { phaseCompleteAt = -1f; return; }
            // Let the boss's coins reach the player first (D26), then leave safely.
            if (phaseCompleteAt < 0) phaseCompleteAt = Time.unscaledTime;
            if (w.Coins.Count > 0 && Time.unscaledTime - phaseCompleteAt < 4f) return;
            foreach (var c in w.Coins) CoinSystem.Collect(w, c);
            w.Coins.Clear();
            phaseCompleteAt = -1f;
            if (w.Phase < 3) BeginPhaseTransition(w.Phase);
            else EndGame();
        }

        void ShowBossPrompt() =>
            menus.ShowConfirm("The boss encounter is ready.", "Visit the shop first?",
                ("Visit the shop", VisitShopBeforeBoss),
                ("Start the encounter", () => { menus.Hide(); BeginBossEntrance(); }));

        /// <summary>Places the player and companion safely beside the shop; shopping is calm until Ready (§7).</summary>
        void VisitShopBeforeBoss()
        {
            menus.Hide();
            var w = W;
            Vector2 shop = w.Layout.ShopPosition;
            w.Player.Position = w.Player.PrevPosition = w.Arena.NearestFree(shop + new Vector2(0.8f, 1.6f), w.Profile.Player.BodyRadius);
            if (w.Companion.Present)
                w.Companion.Position = w.Companion.PrevPosition = w.Arena.NearestFree(shop + new Vector2(-1.4f, 1.6f), w.Profile.Companion.BodyRadius);
            w.Narrative.PreparingAtShop = true;
            app.States.Transition(GameState.Playing);
            ShopSystem.SetOpen(w, true);
        }

        void BeginBossEntrance()
        {
            var w = W;
            w.Narrative.PreparingAtShop = false;
            ShopSystem.SetOpen(w, false);
            app.States.Transition(GameState.Scene);
            PlayScene($"Boss{w.Phase}Entrance", () =>
            {
                BossSystem.StartEncounter(W);
                app.States.Transition(GameState.Playing);
            });
        }

        void BeginPhaseTransition(int from)
        {
            app.States.Transition(GameState.PhaseTransition);
            shopPanel.Hide();
            arena.ShowPhase(from + 1); // the environment changes during the scene
            atmosphere.SetPhase(W.Profile.Atmosphere, from + 1, instant: false);
            PlayScene($"Phase{from}To{from + 1}", () =>
            {
                PhaseSystem.EnterPhase(W, from + 1);
                presenter.ResetViews();
                arena.ShowPhase(W.Phase);
                app.States.Transition(GameState.Playing);
                if (W.Phase == 3) dialogue.ShowBark("Phase3Start");
            });
        }

        void EndGame()
        {
            W.Completed = true;
            SaveNow(quiet: true);
            app.States.Transition(GameState.Ending);
            shopPanel.Hide();
            atmosphere.FadeToDark(W.Profile.Atmosphere); // the world goes dark while the last lines play
            PlayScene("Ending", () => ShowResults(montage: true));
        }

        void ShowResults(bool montage)
        {
            ending.Show(montage, Statistics(W), null,
                () => app.GoToMainMenu(),
                () => { app.OpenNewGamePicker = true; app.GoToMainMenu(); });
        }

        static string Statistics(World w)
        {
            var t = TimeSpan.FromSeconds(w.Clock.Seconds);
            var weapons = new System.Text.StringBuilder();
            foreach (var o in w.Player.Loadout.Owned) weapons.Append($"{w.Profile.Weapon(o.Id).DisplayName} Lv{o.Level}  ");
            return $"Playtime {(int)t.TotalMinutes}:{t.Seconds:00}   ·   Enemies removed {w.Progress.Kills}   ·   Retries {w.Retries}\n" +
                   $"Coins generated {w.Progress.CoinsGenerated}, collected {w.Progress.CoinsCollected}   ·   XP {w.Progress.TotalXp}   ·   Items used {w.Progress.ItemsUsed}\n" +
                   $"Skills: {string.Join(", ", w.Progress.ChosenSkills)}\nWeapons: {weapons}";
        }

        void PlayScene(string id, Action done)
        {
            var seen = W.Narrative.SeenScenes;
            bool canSkip = seen.Contains(id);
            if (!canSkip) seen.Add(id);
            dialogue.PlayScene(id, canSkip, done);
        }
        static string TutorialPrompt(World w) => w.Tutorial switch
        {
            TutorialStage.FirstGroup => "WASD to move · aim with the mouse · hold left click to fire · Space to dash",
            TutorialStage.Purchase => w.Coins.Count > 0 ? "Collect the coins they dropped" : "Go to the shop (south) and buy the SMG",
            _ => null,
        };

        // ---------------------------------------------------------------- shop (live, never pauses)

        void SyncShop(World w)
        {
            bool playing = app.States.Current == GameState.Playing;
            if (w.Shop.Open && playing) { shopPanel.Bind(w); shopPanel.Show(w); }
            else if (shopPanel.Visible) shopPanel.Hide();

            string status = TutorialPrompt(w);
            if (!w.Shop.Open && ShopSystem.InRange(w) && playing) status = "Press E to open the shop";
            if (Time.unscaledTime < flashUntil) status = flashMessage;
            hud.SetStatus(status);
        }

        void OnShopBuy(ShopOffer offer)
        {
            if (app.States.Current != GameState.Playing) return;
            ShopSystem.Purchase(W, offer.Kind, offer.Weapon, offer.Item);
        }

        // ---------------------------------------------------------------- XP choice (world fully paused)

        void OpenXpChoice()
        {
            app.States.Transition(GameState.XPChoice);
            xpScreen.Show(W, W.Progress.Pending, OnSkillChosen);
        }

        void OnSkillChosen(SkillId id)
        {
            var w = W;
            if (app.States.Current != GameState.XPChoice || !XpSystem.Choose(w, id)) return;
            if (w.Progress.Pending != null)
            {
                xpScreen.Show(w, w.Progress.Pending, OnSkillChosen);
                return;
            }
            xpScreen.Hide();
            app.States.Transition(GameState.Playing);
        }

        // ---------------------------------------------------------------- pause, save, load

        void OpenPause()
        {
            app.States.Transition(GameState.Paused);
            xpScreen.Hide();
            ShowPauseMenu();
        }

        void ShowPauseMenu(string status = null) =>
            menus.ShowPause(ClosePause, () => { SaveNow(); ShowPauseMenu(flashMessage); }, CanSave(), () => ShowSlotLoad(fromFailure: false),
                () => menus.ShowSettings(settings, () => ShowPauseMenu()), () => menus.ShowControls(() => ShowPauseMenu()), ExitToMenu, status);

        void ClosePause()
        {
            menus.Hide();
            app.States.Resume();
            if (app.States.Current == GameState.BossPrep) ShowBossPrompt();
            if (app.States.Current == GameState.XPChoice && W.Progress.Pending != null)
                xpScreen.Show(W, W.Progress.Pending, OnSkillChosen);
        }

        /// <summary>Saving is refused during short scenes and transitions (§11); everything else is exact.</summary>
        bool CanSave() => app.States.ResumeState != GameState.Scene && app.States.ResumeState != GameState.PhaseTransition;

        void SaveNow(bool quiet = false)
        {
            var result = SlotStore.Write(app.ActiveSlot, SaveFile.Create(W, W.Clock.Seconds));
            if (result.Ok)
            {
                lastSavedHash = WorldSerializer.Hash(W);
                if (!quiet) Flash($"Saved to slot {app.ActiveSlot}.");
            }
            else Flash($"SAVE FAILED: {result.Error}");
        }

        void Flash(string message)
        {
            flashMessage = message;
            flashUntil = Time.unscaledTime + 3f;
        }

        void ShowSlotLoad(bool fromFailure)
        {
            menus.ShowSlots(SlotPickMode.Load, slot => app.LoadGame(slot),
                () => { if (fromFailure) { menus.Hide(); failureScreen.Show(W.Failure); } else ShowPauseMenu(); });
            if (fromFailure) failureScreen.Hide();
        }

        void ExitToMenu()
        {
            bool unsaved = lastSavedHash != WorldSerializer.Hash(W);
            if (!unsaved) { app.GoToMainMenu(); return; }
            menus.ShowConfirm("Unsaved progress", "Leave the game?",
                ("Save and exit", () => { SaveNow(); app.GoToMainMenu(); }),
                ("Exit without saving", () => app.GoToMainMenu()),
                ("Cancel", () => ShowPauseMenu()));
        }

        // ---------------------------------------------------------------- failure

        /// <summary>Death (or the companion's) restores the wave/boss start checkpoint (§12).</summary>
        void RetryFromCheckpoint()
        {
            var restored = Checkpoints.Restore(W);
            restored.Profile = app.Balance.Live;
            failureScreen.Hide();
            SetWorld(restored);
            app.States.Transition(GameState.Loading);
            app.States.Transition(GameState.Playing);
        }
    }
}
