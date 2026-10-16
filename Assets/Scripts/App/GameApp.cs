using Hollow.App.Input;
using Hollow.Core;
using Hollow.Core.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollow.App
{
    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string MainMenu = "MainMenu";
        public const string Game = "Game";
    }

    /// <summary>
    /// Composition root that lives across scenes: game-state machine, input source and router.
    /// Created by the Boot scene, or on demand when a scene is played directly in the Editor.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }

        public GameStateMachine States { get; private set; }
        public InputRouter Router { get; private set; }
        public UnityInputSource InputSource { get; private set; }

        /// <summary>Camera used to turn the mouse position into a world aim point.</summary>
        public Camera WorldCamera { get; set; }

        int modalCount;

        public static GameApp Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(GameApp));
            DontDestroyOnLoad(go);
            return go.AddComponent<GameApp>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
#if UNITY_EDITOR
            // Lets automated play-mode checks run while the Editor window is not focused; builds keep the default.
            Application.runInBackground = true;
#endif
            States = new GameStateMachine();
            Router = new InputRouter();
            InputSource = new UnityInputSource();
            States.Changed += (_, _) => RefreshWorldInput();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            InputSource?.Dispose();
            Instance = null;
        }

        void Update()
        {
            Router.Sample(InputSource.Read(WorldCamera));
        }

        /// <summary>Open/close a modal panel that owns the mouse while the world keeps running (none yet use it).</summary>
        public void PushModal() { modalCount++; RefreshWorldInput(); }
        public void PopModal() { modalCount = Mathf.Max(0, modalCount - 1); RefreshWorldInput(); }

        void RefreshWorldInput()
        {
            Router.SetWorldAcceptsInput(States.Current == GameState.Playing && modalCount == 0);
        }

        public void GoToMainMenu()
        {
            States.Transition(GameState.MainMenu);
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        public void StartNewGame()
        {
            States.Transition(GameState.Loading);
            SceneManager.LoadScene(SceneNames.Game);
        }

        /// <summary>Editor convenience: the Game scene was played directly, skip the menus.</summary>
        public void EnterGameDirectly()
        {
            if (States.Current == GameState.Boot) States.Transition(GameState.MainMenu);
            if (States.Current == GameState.MainMenu) States.Transition(GameState.Loading);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
