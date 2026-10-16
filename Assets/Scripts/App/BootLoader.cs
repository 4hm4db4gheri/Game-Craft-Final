using UnityEngine;

namespace Hollow.App
{
    /// <summary>Lives in the Boot scene: creates the app root and moves on to the main menu.</summary>
    public sealed class BootLoader : MonoBehaviour
    {
        void Start()
        {
            GameApp.Ensure().GoToMainMenu();
        }
    }
}
