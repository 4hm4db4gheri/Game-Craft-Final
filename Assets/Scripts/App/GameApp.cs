using Hollow.App.Input;
using Hollow.Core;
using Hollow.Core.Input;
using Hollow.Data;
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

    public enum LaunchMode { NewGame, LoadSlot }

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
        public BalanceService Balance { get; private set; }

        /// <summary>Set by developer tools (balance panel) to hold the simulation while they are open.</summary>
        public bool ToolFreeze { get; set; }

        /// <summary>Camera used to turn the mouse position into a world aim point.</summary>
        public Camera WorldCamera { get; set; }

        int modalCount;

        /// <summary>What the Game scene should do when it starts.</summary>
        public LaunchMode Launch { get; private set; } = LaunchMode.NewGame;
        /// <summary>The slot this game saves into (1..3).</summary>
        public int ActiveSlot { get; set; } = 1;

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
            Balance = BalanceProfileStore.CreateService();
            GameSettings.ApplyWindow();
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
            if (States.Current != GameState.MainMenu) States.Transition(GameState.MainMenu);
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        public void StartNewGame(int slot)
        {
            Launch = LaunchMode.NewGame;
            ActiveSlot = slot;
            EnterGameScene();
        }

        public void LoadGame(int slot)
        {
            Launch = LaunchMode.LoadSlot;
            ActiveSlot = slot;
            EnterGameScene();
        }

        void EnterGameScene()
        {
            if (States.Current != GameState.Loading) States.Transition(GameState.Loading);
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
