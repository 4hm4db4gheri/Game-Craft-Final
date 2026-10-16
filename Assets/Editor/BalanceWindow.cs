using System.IO;
using Hollow.App;
using Hollow.Data;
using Hollow.Dev;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Editor window over the balance profile. Outside Play mode it edits Baseline.json; in Play mode it edits the
    /// running game's live profile (same panel as the in-game F9 overlay), and "Save" writes it back to Baseline.json.
    /// </summary>
    public sealed class BalanceWindow : EditorWindow, IBalancePanelHost
    {
        BalanceService editService;
        BalanceService boundService;
        BalancePanelGUI gui;
        string status = "";

        [MenuItem("Hollow/Balance Panel %#b")]
        public static void Open() => GetWindow<BalanceWindow>("Hollow Balance");

        void OnGUI()
        {
            var service = CurrentService();
            if (service == null) { EditorGUILayout.HelpBox("Baseline.json not found. Run Hollow/Balance/Write Baseline From GDD Seed.", MessageType.Error); return; }
            if (service != boundService) { boundService = service; gui = new BalancePanelGUI(service, this); }
            EditorGUILayout.LabelField(Application.isPlaying ? "PLAY MODE: editing the running game's profile" : "EDIT MODE: editing Baseline.json", EditorStyles.boldLabel);
            gui.Draw();
        }

        void OnInspectorUpdate() => Repaint();

        BalanceService CurrentService()
        {
            if (Application.isPlaying && GameApp.Instance != null) return GameApp.Instance.Balance;
            if (editService == null && File.Exists(BaselineSeed.BaselinePath))
            {
                var p = BalanceJson.FromJson(File.ReadAllText(BaselineSeed.BaselinePath));
                editService = new BalanceService(p);
            }
            return editService;
        }

        // ---------------------------------------------------------------- IBalancePanelHost

        public string SaveLabel => "Save live → Baseline.json";
        public string Status => status;

        public void Save(BalanceService s)
        {
            File.WriteAllText(BaselineSeed.BaselinePath, BalanceJson.ToJson(s.Live));
            AssetDatabase.ImportAsset(BaselineSeed.BaselinePath);
            status = $"Saved rev {s.Live.Revision} to {BaselineSeed.BaselinePath}";
            if (!Application.isPlaying) editService = null; // reload so the baseline used for "Reset group" matches the file
        }

        public void ExportJson(BalanceService s)
        {
            string path = EditorUtility.SaveFilePanel("Export balance profile", "", "balance_profile.json", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, BalanceJson.ToJson(s.Draft));
            status = "Exported " + path;
        }

        public void ImportJson(BalanceService s)
        {
            string path = EditorUtility.OpenFilePanel("Import balance profile", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            try { s.LoadIntoDraft(BalanceJson.FromJson(File.ReadAllText(path))); status = "Imported into draft (press Apply)"; }
            catch (System.Exception e) { status = "Import failed: " + e.Message; }
        }

        public void ExportCsv(BalanceService s)
        {
            string path = EditorUtility.SaveFilePanel("Export weapon levels", "", "weapon_levels.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, WeaponLevelCsv.Export(s.Draft));
            status = "Exported " + path;
        }

        public void ImportCsv(BalanceService s)
        {
            string path = EditorUtility.OpenFilePanel("Import weapon levels", "", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var errors = WeaponLevelCsv.Import(s.Draft, File.ReadAllText(path));
            s.NotifyDraftEdited();
            status = errors.Count == 0 ? "Imported weapon levels into draft (press Apply)" : "CSV rejected: " + string.Join("; ", errors);
        }
    }
}
