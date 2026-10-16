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
            if (app.States.Current == GameState.Boot) app.States.Transition(GameState.MainMenu);
            menus = ui.gameObject.AddComponent<MenuScreens>();
            menus.Init(ui);
            Main();
        }

        void Main()
        {
            bool anySave = false;
            for (int s = 1; s <= SlotStore.SlotCount; s++) anySave |= SlotStore.Exists(s);
            menus.ShowMain(
                () => menus.ShowSlots(SlotPickMode.NewGame, slot => app.StartNewGame(slot), Main),
                () => menus.ShowSlots(SlotPickMode.Continue, slot => app.LoadGame(slot), Main),
                anySave,
                () => menus.ShowSettings(settings, Main),
                () => menus.ShowControls(Main),
                () => app.Quit());
        }
    }
}
