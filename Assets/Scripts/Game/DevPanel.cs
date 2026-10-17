using System;
using System.Linq;
using Hollow.App;
using Hollow.Data;
using Hollow.Sim;
using UnityEngine;

namespace Hollow.Game
{
    /// <summary>
    /// Development-only panel (F10, §13): start any wave or boss directly, a starting state (weapons, items and
    /// coins of a phase), spawn any enemy, grant coins or XP, kill everything, god mode, fixed random seed for
    /// the next new game. Inert outside the Editor and development builds, and removed from the scene when a
    /// release build is made (ReleaseBuildStripper).
    /// </summary>
    public sealed class DevPanel : MonoBehaviour
    {
        [SerializeField] GameSession session;

        public static ulong? FixedSeed;

        /// <summary>Enter Play Mode without Domain Reload keeps statics between sessions: start every session clean.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => FixedSeed = null;

        bool open;
        bool god;
        int phase = 1, wave = 1;
        EnemyId spawnType;
        string seedText = "";
        Vector2 scroll;
        GUIStyle labelStyle, buttonStyle, toggleStyle, textFieldStyle;

        static bool Allowed => Application.isEditor || Debug.isDebugBuild;

        void Update()
        {
            if (!Allowed || GameApp.Instance == null) return;
            if (GameApp.Instance.InputSource.TakeDevPanel())
            {
                open = !open;
                if (open) GameApp.Instance.PushModal(); else GameApp.Instance.PopModal();
            }
            var sim = session != null ? session.Sim : null;
            if (god && sim != null) sim.World.Player.InvulnerableUntilTick = sim.World.Clock.Tick + 2;
        }

        void OnGUI()
        {
            if (!open || !Allowed || session == null || session.Sim == null) return;
            var w = session.Sim.World;
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 23, wordWrap = true };
            buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 23, wordWrap = true };
            toggleStyle ??= new GUIStyle(GUI.skin.toggle) { fontSize = 24, wordWrap = true };
            textFieldStyle ??= new GUIStyle(GUI.skin.textField) { fontSize = 23 };

            var previousMatrix = GUI.matrix;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            var area = new Rect(12, 12, Mathf.Min(760, width - 24), height - 24);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12, area.y + 12, area.width - 24, area.height - 24));
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("DEV PANEL (F10)", labelStyle, GUILayout.Height(42));
            god = GUILayout.Toggle(god, "God mode (player invulnerable)", toggleStyle, GUILayout.Height(58));

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Phase {phase}", labelStyle, GUILayout.Width(115));
            if (Button("-", GUILayout.Width(64))) phase = Mathf.Max(1, phase - 1);
            if (Button("+", GUILayout.Width(64))) phase = Mathf.Min(3, phase + 1);
            GUILayout.Label($"Wave {wave}", labelStyle, GUILayout.Width(110));
            if (Button("-", GUILayout.Width(64))) wave = Mathf.Max(1, wave - 1);
            if (Button("+", GUILayout.Width(64))) wave = Mathf.Min(5, wave + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Button("Start this wave now")) StartWave(w);
            if (Button("Start this phase's boss")) StartBoss(w);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Starting state: every weapon, item and 300 coins of this phase", buttonStyle, GUILayout.Height(74))) GiveStartingState(w);

            GUILayout.BeginHorizontal();
            if (Button("◀", GUILayout.Width(64))) spawnType = Cycle(spawnType, -1);
            GUILayout.Label(spawnType.ToString(), labelStyle, GUILayout.MinWidth(280));
            if (Button("▶", GUILayout.Width(64))) spawnType = Cycle(spawnType, 1);
            GUILayout.EndHorizontal();
            if (Button("Spawn it near me")) EnemySystem.Spawn(w, spawnType, w.Arena.NearestFree(w.Player.Position + new Vector2(5, 2), 0.5f));

            if (Button("+100 coins")) w.Progress.Wallet += 100;
            if (Button("+100 XP")) Rewards.GrantXp(w, 100, w.Player.Position);
            if (Button("Kill all enemies"))
                foreach (var e in w.Enemies.ToList()) Combat.DamageEnemy(w, e, 1e7f, DamageSource.Weapon, Vector2.right);
            GUILayout.Label($"Seed for the next new game (empty = random){(FixedSeed.HasValue ? $": {FixedSeed}" : "")}", labelStyle);
            GUILayout.BeginHorizontal();
            seedText = GUILayout.TextField(seedText, textFieldStyle, GUILayout.Height(54));
            if (Button("Set", GUILayout.Width(100))) FixedSeed = ulong.TryParse(seedText, out var s) ? s : null;
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        bool Button(string text, params GUILayoutOption[] options) =>
            GUILayout.Button(text, buttonStyle,
                options.Length == 0 ? new[] { GUILayout.Height(54) } : options.Concat(new[] { GUILayout.Height(54) }).ToArray());

        void StartWave(World w)
        {
            foreach (var e in w.Enemies) e.Alive = false;
            w.Enemies.Clear();
            w.Projectiles.Clear();
            w.Hazards.Clear();
            w.Tutorial = TutorialStage.None;
            w.Progress.ChoicesBlocked = false;
            if (w.Phase != phase) PhaseSystem.EnterPhase(w, phase);
            WaveSystem.StartWave(w, wave);
            w.Tutorial = TutorialStage.None;
            w.Progress.ChoicesBlocked = false;
            session.OnPhaseChangedByTool();
        }

        /// <summary>Clears the field and goes to the boss preparation of the chosen phase (shop, then entrance).</summary>
        void StartBoss(World w)
        {
            ClearField(w);
            if (w.Phase != phase) PhaseSystem.EnterPhase(w, phase);
            ClearField(w);
            w.Waves.Wave = WaveSystem.WaveCount(w);
            WaveSystem.StartNextEncounter(w);
            session.OnPhaseChangedByTool();
        }

        void GiveStartingState(World w)
        {
            foreach (var d in w.Profile.Weapons)
                if (d.UnlockPhase <= phase && !w.Player.Loadout.Owns(d.Id)) WeaponSystem.Give(w, d.Id, 1, equip: false);
            foreach (var item in w.Profile.Items)
                if (item.UnlockPhase <= phase) w.Inventory.Add(item.Id, Mathf.Max(0, item.Carry - w.Inventory.Count(item.Id)));
            w.Progress.Wallet += 300;
        }

        static void ClearField(World w)
        {
            foreach (var e in w.Enemies) e.Alive = false;
            w.Enemies.Clear();
            w.Projectiles.Clear();
            w.Hazards.Clear();
            w.Tutorial = TutorialStage.None;
            w.Progress.ChoicesBlocked = false;
        }

        static EnemyId Cycle(EnemyId id, int d)
        {
            var values = (EnemyId[])Enum.GetValues(typeof(EnemyId));
            int i = (Array.IndexOf(values, id) + d + values.Length) % values.Length;
            return values[i];
        }
    }
}
