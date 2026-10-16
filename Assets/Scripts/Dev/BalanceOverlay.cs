using System;
using System.IO;
using Hollow.App;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Dev
{
    /// <summary>
    /// In-game balance panel (F9). Available in the Editor and in development builds (and in any build that
    /// defines HOLLOW_BALANCE_PANEL). Edits apply live when Auto-apply is on; the world can be frozen while open.
    /// </summary>
    public sealed class BalanceOverlay : MonoBehaviour, IBalancePanelHost
    {
        BalancePanelGUI gui;
        bool open;
        bool freezeWhileOpen = true;
        string status = "";

#if HOLLOW_BALANCE_PANEL
        const bool ForcedOn = true;
#else
        const bool ForcedOn = false;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Application.isEditor && !Debug.isDebugBuild && !ForcedOn) return;
            if (FindAnyObjectByType<BalanceOverlay>() != null) return;
            var go = new GameObject(nameof(BalanceOverlay));
            DontDestroyOnLoad(go);
            go.AddComponent<BalanceOverlay>();
        }

        static string Folder => Path.Combine(Application.persistentDataPath, "balance");

        void Update()
        {
            var app = GameApp.Instance;
            if (app == null) return;
            if (app.InputSource.BalancePanel.WasPressedThisFrame()) SetOpen(!open);
        }

        void SetOpen(bool value)
        {
            var app = GameApp.Instance;
            if (open == value || app == null) return;
            open = value;
            if (open)
            {
                gui ??= new BalancePanelGUI(app.Balance, this);
                app.PushModal();
            }
            else
            {
                app.PopModal();
            }
            app.ToolFreeze = open && freezeWhileOpen;
        }

        void OnGUI()
        {
            if (!open || gui == null) return;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float w = Screen.width / scale, h = Screen.height / scale;
            var area = new Rect(w * 0.3f, 0, w * 0.7f, h);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none); // double box = darker background
            GUILayout.BeginArea(new Rect(area.x + 6, area.y + 6, area.width - 12, area.height - 12));
            GUILayout.BeginHorizontal();
            GUILayout.Label("BALANCE PANEL (F9)");
            bool freeze = GUILayout.Toggle(freezeWhileOpen, "Freeze game while open");
            if (freeze != freezeWhileOpen) { freezeWhileOpen = freeze; GameApp.Instance.ToolFreeze = open && freezeWhileOpen; }
            if (GUILayout.Button("Close", GUILayout.Width(60))) SetOpen(false);
            GUILayout.EndHorizontal();
            gui.Draw();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- IBalancePanelHost (files under persistentDataPath/balance)

        public string SaveLabel => "Save as start profile";
        public string Status => status;

        public void Save(BalanceService s)
        {
            Write(BalanceProfileStore.ActiveProfileFileName, BalanceJson.ToJson(s.Live), "Saved live profile; it loads at next start of this build");
        }

        public void ExportJson(BalanceService s) =>
            Write($"profile_{DateTime.Now:yyyyMMdd_HHmmss}.json", BalanceJson.ToJson(s.Draft), "Exported draft");

        public void ImportJson(BalanceService s)
        {
            string path = Path.Combine(Folder, "import.json");
            if (!File.Exists(path)) { status = $"Put a profile at {path}"; return; }
            try { s.LoadIntoDraft(BalanceJson.FromJson(File.ReadAllText(path))); status = "Imported into draft (press Apply)"; }
            catch (Exception e) { status = "Import failed: " + e.Message; }
        }

        public void ExportCsv(BalanceService s) =>
            Write($"weapon_levels_{DateTime.Now:yyyyMMdd_HHmmss}.csv", WeaponLevelCsv.Export(s.Draft), "Exported weapon levels");

        public void ImportCsv(BalanceService s)
        {
            string path = Path.Combine(Folder, "import_weapon_levels.csv");
            if (!File.Exists(path)) { status = $"Put a CSV at {path}"; return; }
            var errors = WeaponLevelCsv.Import(s.Draft, File.ReadAllText(path));
            s.NotifyDraftEdited();
            status = errors.Count == 0 ? "Imported weapon levels into draft" : "CSV rejected: " + errors[0];
        }

        void Write(string file, string content, string message)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, file);
                File.WriteAllText(path, content);
                status = $"{message}: {path}";
            }
            catch (Exception e) { status = "Write failed: " + e.Message; }
        }

        void OnDisable()
        {
            if (open) SetOpen(false);
        }
    }
}
