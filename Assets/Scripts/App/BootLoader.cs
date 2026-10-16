using UnityEngine;

namespace Hollow.App
{
    /// <summary>Lives in the Boot scene: creates the app root and moves on to the main menu.</summary>
    public sealed class BootLoader : MonoBehaviour
    {
        void Start()
        {
            var app = GameApp.Ensure();
            if (GameApp.StressRequested) UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Game); // straight to the stress test
            else app.GoToMainMenu();
        }
    }
}
