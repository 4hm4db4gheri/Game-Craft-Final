using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>Windows x64 build into Builds/Windows (D36). Writes a one-line result file for automation.</summary>
    public static class BuildTool
    {
        public const string OutputDir = "Builds/Windows";
        public const string ResultFile = "Builds/last_build_result.txt";

        [MenuItem("Hollow/Build/Windows x64")]
        public static void BuildWindows() => BuildWindows(development: false);

        [MenuItem("Hollow/Build/Windows x64 (Development)")]
        public static void BuildWindowsDev() => BuildWindows(development: true);

        public static void BuildWindows(bool development)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputDir, "Hollow.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            string line = $"{s.result} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} time={s.totalTime.TotalSeconds:0.0}s dev={development}";
            Directory.CreateDirectory("Builds");
            File.WriteAllText(ResultFile, line);
            Debug.Log($"[Hollow] Build {line}");
        }
    }
}
