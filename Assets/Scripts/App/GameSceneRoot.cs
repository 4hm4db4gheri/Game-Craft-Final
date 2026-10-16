using Hollow.Core;
using UnityEngine;

namespace Hollow.App
{
    /// <summary>
    /// Root of the Game scene. Owns the fixed-step driver; gameplay systems are attached in later tasks.
    /// </summary>
    public sealed class GameSceneRoot : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;

        GameApp app;
        SimDriver driver;
        readonly SimClock clock = new();

        void Start()
        {
            app = GameApp.Ensure();
            app.EnterGameDirectly();
            app.WorldCamera = worldCamera != null ? worldCamera : Camera.main;
            driver = new SimDriver(app.States, Step);
            app.States.Transition(GameState.Playing);
        }

        void Step()
        {
            app.Router.Consume();
            clock.Step(true);
        }

        void Update()
        {
            driver?.Update(Time.unscaledDeltaTime);
        }
    }
}
