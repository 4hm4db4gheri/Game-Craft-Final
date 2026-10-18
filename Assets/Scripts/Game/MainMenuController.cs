using Hollow.App;
using Hollow.Core;
using Hollow.Sim;
using Hollow.UI;
using UnityEngine;

namespace Hollow.Game
{
    /// <summary>Main menu (§11): new game, continue, settings, controls, quit; slot pickers with replace confirmation.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] UIRoot ui;

        GameApp app;
        MenuScreens menus;
        readonly SettingsAdapter settings = new();

        void Start()
        {
            app = GameApp.Ensure();
            SlotStore.RootOverride = null; // returning from a direct Game-scene test restores the real slots
            var audio = AudioBridge.Attach();
            audio.SetPaused(false);
            audio.PlayMenuMusic();
            if (app.States.Current == GameState.Boot) app.States.Transition(GameState.MainMenu);
            menus = ui.gameObject.AddComponent<MenuScreens>();
            menus.Init(ui);
            Main();
            if (app.OpenNewGamePicker)
            {
                app.OpenNewGamePicker = false;
                NewGame();
            }
        }

        /// <summary>New game (v0.19 §3): choose a slot, then an AI, then the opening.</summary>
        void NewGame() =>
            menus.ShowSlots(SlotPickMode.NewGame,
                slot => menus.ShowAiSelect(app.Balance.Live, ai => app.StartNewGame(slot, ai), NewGame), Main);

        void Main()
        {
            bool anySave = false;
            for (int s = 1; s <= SlotStore.SlotCount; s++) anySave |= SlotStore.Exists(s);
            menus.ShowMain(
                NewGame,
                () => menus.ShowSlots(SlotPickMode.Continue, slot => app.LoadGame(slot), Main),
                anySave,
                Settings,
                () => menus.ShowControls(Main),
                ConfirmResetProgress,
                () => app.Quit());
        }

        void Settings() => menus.ShowSettings(settings, Main, () => menus.ShowControls(Settings), ConfirmResetProgress);

        void ConfirmResetProgress() =>
            menus.ShowConfirm(Loc.T("reset.title"), Loc.T("reset.body"),
                (Loc.T("reset.confirm"), ResetProgress), (Loc.T("common.cancel"), Settings));

        void ResetProgress()
        {
            var result = SlotStore.DeleteAll();
            if (!result.Ok)
            {
                Debug.LogWarning($"Could not clear saved progress: {result.Error}");
                menus.ShowConfirm(Loc.T("reset.failedTitle"), Loc.T("reset.failedBody"),
                    (Loc.T("reset.retry"), ResetProgress), (Loc.T("common.back"), Settings));
                return;
            }
            NewGame();
        }
    }
}
