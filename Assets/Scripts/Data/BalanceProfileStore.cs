using System.IO;
using UnityEngine;

namespace Hollow.Data
{
    /// <summary>Where the game gets its starting profile: the shipped baseline, or a profile saved from the in-game panel.</summary>
    public static class BalanceProfileStore
    {
        public const string ActiveProfileFileName = "active.json";

        public static string ActiveProfilePath => Path.Combine(Application.persistentDataPath, "balance", ActiveProfileFileName);

        /// <summary>
        /// In the Editor the baseline asset is always used (the Editor window saves straight into it).
        /// In builds a profile saved from the F9 panel takes precedence when present and valid.
        /// </summary>
        public static BalanceService CreateService()
        {
            var baseline = BalanceService.LoadBaselineFromResources();
#if !UNITY_EDITOR
            if (File.Exists(ActiveProfilePath))
            {
                try { return new BalanceService(baseline, BalanceJson.FromJson(File.ReadAllText(ActiveProfilePath))); }
                catch (System.Exception e) { Debug.LogWarning($"[Hollow] Ignoring saved profile: {e.Message}"); }
            }
#endif
            return new BalanceService(baseline);
        }
    }
}
