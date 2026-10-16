using Hollow.Core;
using UnityEngine;

namespace Hollow.App
{
    /// <summary>Temporary main menu until the real screens exist (T15).</summary>
    public sealed class MainMenuPlaceholder : MonoBehaviour
    {
        void Start()
        {
            var app = GameApp.Ensure();
            if (app.States.Current == GameState.Boot) app.States.Transition(GameState.MainMenu);
        }

        void OnGUI()
        {
            var app = GameApp.Instance;
            if (app == null) return;
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 100, Screen.height / 2f - 60, 200, 120));
            GUILayout.Label("HOLLOW (placeholder menu)");
            if (GUILayout.Button("New game")) app.StartNewGame();
            if (GUILayout.Button("Quit")) app.Quit();
            GUILayout.EndArea();
        }
    }
}
