using System;
using System.IO;
using Hollow.Sim;
using UnityEngine;

namespace Hollow.Game
{
    /// <summary>Writes the run telemetry (§13) to persistentDataPath/telemetry, one JSON file per run.</summary>
    public static class TelemetryFile
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "telemetry");

        public static string Build =>
            $"{Application.version} {(Application.isEditor ? "editor" : Debug.isDebugBuild ? "development" : "release")} {Application.unityVersion}";

        public static string PathOf(RunTelemetry t)
        {
            var started = DateTime.Parse(t.Run.StartedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);
            return Path.Combine(Folder, $"run_{started:yyyyMMdd_HHmmss}.json");
        }

        public static void Write(RunTelemetry t)
        {
            t.Dirty = false;
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(PathOf(t), t.ToJson());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Hollow] Telemetry could not be written: {e.Message}"); // never stops the game
            }
        }
    }
}
