using System.IO;
using Hollow.App;
using Hollow.Data;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Every balance or visual change made in the Editor is written to Baseline.json at once, so it survives
    /// leaving Play mode and every later run (D96). Inspector edits to the tuning assets (music mix, operator
    /// lines) are flushed to disk when Play mode ends.
    /// </summary>
    [InitializeOnLoad]
    public static class BalanceAutoSave
    {
        static BalanceService watched;

        static BalanceAutoSave()
        {
            EditorApplication.update += WatchRunningGame;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredEditMode) AssetDatabase.SaveAssets();
            };
        }

        /// <summary>Follows the running game's balance service; each Apply or Undo is saved.</summary>
        static void WatchRunningGame()
        {
            var service = Application.isPlaying && GameApp.Instance != null ? GameApp.Instance.Balance : null;
            if (service == watched) return;
            if (watched != null) watched.Applied -= OnApplied;
            watched = service;
            if (watched != null) watched.Applied += OnApplied;
        }

        /// <summary>Edit-mode services (the Balance window outside Play) are saved the same way.</summary>
        public static void Track(BalanceService service)
        {
            service.Applied -= OnApplied;
            service.Applied += OnApplied;
        }

        static void OnApplied(BalanceProfile live) => Write(live, out _);

        /// <summary>Writes the profile to Baseline.json, keeping the file's schema revision. Returns false when invalid.</summary>
        public static bool Write(BalanceProfile profile, out string error)
        {
            error = null;
            var errors = BalanceValidator.Validate(profile).FindAll(i => i.Severity == IssueSeverity.Error);
            if (errors.Count > 0) { error = errors[0].ToString(); return false; }
            var copy = BalanceJson.Clone(profile);
            // Apply bumps the live revision; the file's revision is the schema version save migrations compare against.
            if (File.Exists(BaselineSeed.BaselinePath))
                copy.Revision = BalanceJson.FromJson(File.ReadAllText(BaselineSeed.BaselinePath)).Revision;
            string json = BalanceJson.ToJson(copy);
            if (File.Exists(BaselineSeed.BaselinePath) && File.ReadAllText(BaselineSeed.BaselinePath) == json) return true;
            File.WriteAllText(BaselineSeed.BaselinePath, json);
            AssetDatabase.ImportAsset(BaselineSeed.BaselinePath);
            return true;
        }
    }
}
