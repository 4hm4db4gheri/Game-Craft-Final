using System;
using System.Collections;
using System.IO;
using Hollow.App;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Dev;
using Hollow.Sim;
using UnityEngine;

namespace Hollow.Game
{
    /// <summary>
    /// Worst-case stress test (T21): phase 3 wave 5 (164 enemies, up to 42 alive) with every effect on, a bot
    /// that keeps firing the SMG at the nearest enemy, the player invulnerable. Measures frame time, GC
    /// allocations and memory with <see cref="FrameProbe"/>, writes the report to persistentDataPath/perf and
    /// quits a player build. Start a development build with -hollowStress, or set <see cref="Requested"/> in the Editor.
    /// Never available in release builds.
    /// </summary>
    public sealed class StressRun : MonoBehaviour
    {
        public static bool Requested => GameApp.StressRequested;

        public static string LastReport { get; private set; }

        const float WarmupSeconds = 25f, MeasureSeconds = 30f;

        GameSession session;
        int aliveSamples;
        long aliveSum;
        int aliveMax;

        public void Init(GameSession s) => session = s;

        IEnumerator Start()
        {
            yield return null;
            yield return null;
            session.FinishSceneNow();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-uncapped") >= 0)
            {
                // Measure headroom: no VSync, no frame cap.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }
            var w = session.Sim.World;
            SetUp(w);
            session.CommandOverride = Bot;

            float until = Time.unscaledTime + WarmupSeconds;
            while (Time.unscaledTime < until && WaveSystem.AliveCount(session.Sim.World) < 38) yield return null;
            var probe = FrameProbe.Run(MeasureSeconds, "Phase 3 wave 5 worst case");
            while (!probe.Done)
            {
                int alive = WaveSystem.AliveCount(session.Sim.World);
                aliveSum += alive;
                aliveSamples++;
                aliveMax = Mathf.Max(aliveMax, alive);
                yield return null;
            }

            LastReport = $"{probe.Report}; vsync {QualitySettings.vSyncCount}, frame cap {Application.targetFrameRate}; enemies alive avg {(double)aliveSum / Mathf.Max(1, aliveSamples):F1}, max {aliveMax}; effect budget {GameSettings.EffectBudget}, damage numbers {GameSettings.DamageNumbers}";
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "perf");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, $"stress_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt"), LastReport + Environment.NewLine);
            }
            catch (Exception e) { Debug.LogWarning($"[Hollow perf] report not written: {e.Message}"); }
            Debug.Log("[Hollow perf] " + LastReport);
            session.CommandOverride = null;
            if (!Application.isEditor) Application.Quit();
        }

        void SetUp(World w)
        {
            foreach (var e in w.Enemies) e.Alive = false;
            w.Enemies.Clear();
            w.Tutorial = TutorialStage.None;
            if (w.Phase != 3) PhaseSystem.EnterPhase(w, 3);
            foreach (var e in w.Enemies) e.Alive = false;
            w.Enemies.Clear();
            WaveSystem.StartWave(w, 5);
            WeaponSystem.Give(w, WeaponId.LightSmg);
            session.OnPhaseChangedByTool();
        }

        void Update()
        {
            if (session == null || session.Sim == null) return;
            var w = session.Sim.World;
            w.Player.InvulnerableUntilTick = w.Clock.Tick + 2;
            w.Progress.ChoicesBlocked = true; // no XP card pauses during the measurement
            if (w.Waves.Kind == EncounterKind.SafeInterval) WaveSystem.StartWave(w, 5); // keep the worst case going
        }

        static PlayerCommand Bot(World w)
        {
            Vector2 me = w.Player.Position, aim = me + Vector2.right;
            float best = float.MaxValue;
            foreach (var e in w.Enemies)
            {
                if (!e.IsActive) continue;
                float d = (e.Position - me).sqrMagnitude;
                if (d < best) { best = d; aim = e.Position; }
            }
            float a = w.Clock.Tick * 0.02f;
            int slot = w.Player.Loadout.ActiveId == WeaponId.LightSmg ? 0 : 2;
            return new PlayerCommand { Move = new Vector2(Mathf.Cos(a), Mathf.Sin(a * 1.3f)) * 0.5f, AimWorld = aim, FireHeld = true, SelectWeaponSlot = slot };
        }
    }
}
