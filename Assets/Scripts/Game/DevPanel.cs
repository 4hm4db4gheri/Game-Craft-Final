using System;
using System.Linq;
using Hollow.App;
using Hollow.Data;
using Hollow.Sim;
using UnityEngine;

namespace Hollow.Game
{
    /// <summary>
    /// Development-only panel (F10, §13): start any wave directly, spawn any enemy, grant coins or XP,
    /// kill everything, god mode, fixed random seed for the next new game. Never active in release builds.
    /// </summary>
    public sealed class DevPanel : MonoBehaviour
    {
        [SerializeField] GameSession session;

        public static ulong? FixedSeed;

        bool open;
        bool god;
        int phase = 1, wave = 1;
        EnemyId spawnType;
        string seedText = "";

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
            GUILayout.BeginArea(new Rect(10, 10, 360, 520), GUI.skin.box);
            GUILayout.Label("DEV PANEL (F10) — development builds only");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Phase {phase}", GUILayout.Width(70));
            if (GUILayout.Button("-")) phase = Mathf.Max(1, phase - 1);
            if (GUILayout.Button("+")) phase = Mathf.Min(3, phase + 1);
            GUILayout.Label($"Wave {wave}", GUILayout.Width(60));
            if (GUILayout.Button("-")) wave = Mathf.Max(1, wave - 1);
            if (GUILayout.Button("+")) wave = Mathf.Min(5, wave + 1);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Start this wave now")) StartWave(w);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(30))) spawnType = Cycle(spawnType, -1);
            GUILayout.Label(spawnType.ToString(), GUILayout.Width(200));
            if (GUILayout.Button("▶", GUILayout.Width(30))) spawnType = Cycle(spawnType, 1);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Spawn it near me")) EnemySystem.Spawn(w, spawnType, w.Arena.NearestFree(w.Player.Position + new Vector2(5, 2), 0.5f));

            if (GUILayout.Button("+100 coins")) w.Progress.Wallet += 100;
            if (GUILayout.Button("+100 XP")) Rewards.GrantXp(w, 100, w.Player.Position);
            if (GUILayout.Button("Kill all enemies"))
                foreach (var e in w.Enemies.ToList()) Combat.DamageEnemy(w, e, 1e7f, DamageSource.Weapon, Vector2.right);
            god = GUILayout.Toggle(god, "God mode (player invulnerable)");

            GUILayout.Label($"Seed for the next new game (empty = random){(FixedSeed.HasValue ? $": {FixedSeed}" : "")}");
            GUILayout.BeginHorizontal();
            seedText = GUILayout.TextField(seedText, GUILayout.Width(200));
            if (GUILayout.Button("Set")) FixedSeed = ulong.TryParse(seedText, out var s) ? s : null;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

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

        static EnemyId Cycle(EnemyId id, int d)
        {
            var values = (EnemyId[])Enum.GetValues(typeof(EnemyId));
            int i = (Array.IndexOf(values, id) + d + values.Length) % values.Length;
            return values[i];
        }
    }
}
