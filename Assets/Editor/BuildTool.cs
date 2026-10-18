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
        public const string AssetsFile = "Builds/last_build_assets.txt";

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
                locationPathName = Path.Combine(OutputDir, "As Instructed.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            // Start empty: files of an older build (e.g. Hollow_Data from before the rename) would ship too.
            if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            foreach (var dir in Directory.Exists(OutputDir) ? Directory.GetDirectories(OutputDir, "*_DontShipItWithYourGame") : new string[0])
                Directory.Delete(dir, true);
            var s = report.summary;
            string line = $"{s.result} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} time={s.totalTime.TotalSeconds:0.0}s dev={development}";
            Directory.CreateDirectory("Builds");
            File.WriteAllText(ResultFile, line);
            // The 60 largest assets in the build, to see what to shrink next.
            var largest = report.packedAssets.SelectMany(p => p.contents)
                .GroupBy(c => c.sourceAssetPath).Select(g => (Path: g.Key, Size: g.Sum(c => (long)c.packedSize)))
                .OrderByDescending(a => a.Size).Take(60).Select(a => $"{a.Size / 1024,8} KB  {a.Path}");
            File.WriteAllLines(AssetsFile, largest);
            Debug.Log($"[Hollow] Build {line}");
        }
    }
}
