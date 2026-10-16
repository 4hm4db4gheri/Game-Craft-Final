using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Dev
{
    /// <summary>
    /// Reflection-driven IMGUI editor over the balance draft. The same class draws the Editor window and the
    /// in-game F9 panel, so both always show exactly the same fields (GDD §13 groups, apply/undo/reset,
    /// import/export, calculated totals and validation).
    /// </summary>
    public sealed class BalancePanelGUI
    {
        const string CalculatedTab = "Calculated";
        const string IssuesTab = "Issues";
        static readonly string[] Tabs = PanelGroups.All.Concat(new[] { CalculatedTab, IssuesTab }).ToArray();
        static readonly string[] TabLabels = { "Player & companion", "Waves & bosses", "Weapons", "Economy & items", "Skills & feedback", CalculatedTab, IssuesTab };
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        readonly BalanceService service;
        readonly IBalancePanelHost host;
        readonly HashSet<string> expanded = new();
        readonly Dictionary<string, string> buffers = new();

        int tab;
        string search = "";
        Vector2 scroll;
        bool edited;
        GUIStyle tagGdd, tagPlaceholder, changedLabel, errorLabel, warnLabel, header;

        public BalancePanelGUI(BalanceService service, IBalancePanelHost host)
        {
            this.service = service;
            this.host = host;
        }

        public void Draw()
        {
            EnsureStyles();
            edited = false;

            DrawToolbar();
            tab = GUILayout.Toolbar(tab, TabLabels);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(50));
            search = GUILayout.TextField(search ?? "");
            if (GUILayout.Button("Collapse all", GUILayout.Width(90))) expanded.Clear();
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll);
            string current = Tabs[tab];
            if (current == CalculatedTab) DrawCalculated();
            else if (current == IssuesTab) DrawIssues();
            else DrawGroup(current);
            GUILayout.EndScrollView();

            if (edited) service.NotifyDraftEdited();
        }

        void DrawToolbar()
        {
            var live = service.Live;
            GUILayout.Label($"Live: {live.Name}  rev {live.Revision}   |   undo steps: {service.UndoCount}   |   {host.Status}", header);
            int errors = service.LastIssues.Count(i => i.Severity == IssueSeverity.Error);
            int warnings = service.LastIssues.Count(i => i.Severity == IssueSeverity.Warning);
            GUILayout.Label($"Draft issues: {errors} errors, {warnings} warnings" + (errors > 0 ? "  (Apply is blocked until errors are fixed — see Issues tab)" : ""),
                errors > 0 ? errorLabel : GUI.skin.label);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply", GUILayout.Width(70))) { service.Apply(); buffers.Clear(); }
            if (GUILayout.Button("Revert draft", GUILayout.Width(95))) { service.RevertDraft(); buffers.Clear(); }
            GUI.enabled = service.UndoCount > 0;
            if (GUILayout.Button("Undo", GUILayout.Width(60))) { service.Undo(); buffers.Clear(); }
            GUI.enabled = PanelGroups.All.Contains(Tabs[tab]);
            if (GUILayout.Button("Reset group", GUILayout.Width(95))) { service.ResetGroupToBaseline(Tabs[tab]); buffers.Clear(); }
            GUI.enabled = true;
            service.AutoApply = GUILayout.Toggle(service.AutoApply, "Auto-apply (live)", GUILayout.Width(130));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(host.SaveLabel, GUILayout.Width(150))) host.Save(service);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Export JSON")) host.ExportJson(service);
            if (GUILayout.Button("Import JSON")) { host.ImportJson(service); buffers.Clear(); }
            if (GUILayout.Button("Export weapon CSV")) host.ExportCsv(service);
            if (GUILayout.Button("Import weapon CSV")) { host.ImportCsv(service); buffers.Clear(); }
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- groups

        void DrawGroup(string group)
        {
            GUILayout.Label("Tags: GDD = provisional value from the GDD   PH = placeholder (no GDD value)   yellow = draft differs from live", GUI.skin.box);
            foreach (var f in typeof(BalanceProfile).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var g = f.GetCustomAttribute<PanelGroupAttribute>();
                if (g == null || g.Group != group) continue;
                DrawField(f, service.Draft, service.Live, f.Name, 0);
            }
        }

        void DrawField(FieldInfo f, object draftOwner, object liveOwner, string path, int depth)
        {
            object value = f.GetValue(draftOwner);
            object liveValue = liveOwner != null ? f.GetValue(liveOwner) : null;
            Type t = f.FieldType;

            if (IsLeaf(t))
            {
                if (!MatchesSearch(path)) return;
                object newValue = DrawLeaf(Label(f), value, liveValue, t, path, depth, f);
                if (!Equals(newValue, value)) { f.SetValue(draftOwner, newValue); edited = true; }
                return;
            }

            if (value is IList list)
            {
                DrawList(f.Name, list, liveValue as IList, ListElementType(t), path, depth);
                return;
            }

            if (value == null) return;
            if (!Foldout(path, f.Name, depth)) return;
            DrawObject(value, liveValue, path, depth + 1);
            if (t.IsValueType) f.SetValue(draftOwner, value);
        }

        void DrawObject(object draft, object live, string path, int depth)
        {
            foreach (var f in draft.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                DrawField(f, draft, live, $"{path}.{f.Name}", depth);
        }

        void DrawList(string name, IList list, IList liveList, Type elementType, string path, int depth)
        {
            bool open = Foldout(path, $"{name}  ({list.Count})", depth);
            if (!open) return;

            int removeAt = -1;
            for (int i = 0; i < list.Count; i++)
            {
                object item = list[i];
                object liveItem = liveList != null && i < liveList.Count ? liveList[i] : null;
                string itemPath = $"{path}[{i}]";

                if (IsLeaf(elementType))
                {
                    GUILayout.BeginHorizontal();
                    object nv = DrawLeafInline($"[{i}]", item, liveItem, elementType, itemPath, depth + 1);
                    if (!Equals(nv, item)) { list[i] = nv; edited = true; }
                    if (GUILayout.Button("−", GUILayout.Width(22))) removeAt = i;
                    GUILayout.EndHorizontal();
                    continue;
                }

                string label = RecordLabel(item, i);
                GUILayout.BeginHorizontal();
                bool itemOpen = Foldout(itemPath, label, depth + 1);
                if (IsEditableCollection(elementType) && GUILayout.Button("−", GUILayout.Width(22))) removeAt = i;
                GUILayout.EndHorizontal();
                if (!itemOpen) continue;
                DrawObject(item, liveItem, itemPath, depth + 2);
                if (elementType.IsValueType) list[i] = item; // write back boxed struct edits
            }

            if (removeAt >= 0) { list.RemoveAt(removeAt); edited = true; buffers.Clear(); }

            if (IsEditableCollection(elementType) || IsLeaf(elementType))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Indent(depth + 1));
                if (GUILayout.Button($"+ add {elementType.Name}", GUILayout.Width(180)))
                {
                    list.Add(NewElement(list, elementType));
                    edited = true;
                }
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>Only small record lists are edited structurally from the panel; the big catalogues are fixed by ID.</summary>
        static bool IsEditableCollection(Type t) => t == typeof(EnemyCount) || t == typeof(WeaponLevel) || t == typeof(BossThreshold);

        static object NewElement(IList list, Type t)
        {
            if (t == typeof(WeaponLevel) && list.Count > 0)
            {
                var last = (WeaponLevel)list[list.Count - 1];
                var copy = BalanceJsonCopy(last);
                copy.Level = last.Level + 1;
                copy.Approved = false;
                return copy;
            }
            if (t == typeof(float)) return list.Count > 0 ? list[list.Count - 1] : 0f;
            return Activator.CreateInstance(t);
        }

        static WeaponLevel BalanceJsonCopy(WeaponLevel l) => (WeaponLevel)typeof(WeaponLevel)
            .GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(l, null);

        object DrawLeaf(string label, object value, object live, Type t, string path, int depth, FieldInfo f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(Indent(depth));
            bool changed = live != null && !Equals(value, live);
            GUILayout.Label(label, changed ? changedLabel : GUI.skin.label, GUILayout.Width(220));
            object result = DrawValueControl(value, t, path);
            if (f != null)
            {
                var gdd = f.GetCustomAttribute<GddAttribute>();
                if (gdd != null) GUILayout.Label("GDD" + gdd.Section, tagGdd, GUILayout.Width(55));
                else if (f.GetCustomAttribute<PlaceholderAttribute>() != null) GUILayout.Label("PH", tagPlaceholder, GUILayout.Width(55));
                else GUILayout.Space(59);
                var unit = f.GetCustomAttribute<UnitAttribute>();
                GUILayout.Label(unit != null ? unit.Unit : "", GUILayout.Width(60));
            }
            if (changed) GUILayout.Label($"live: {Format(live)}", changedLabel, GUILayout.Width(120));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return result;
        }

        object DrawLeafInline(string label, object value, object live, Type t, string path, int depth)
        {
            GUILayout.Space(Indent(depth));
            bool changed = live != null && !Equals(value, live);
            GUILayout.Label(label, changed ? changedLabel : GUI.skin.label, GUILayout.Width(60));
            return DrawValueControl(value, t, path);
        }

        object DrawValueControl(object value, Type t, string path)
        {
            if (t == typeof(bool)) return GUILayout.Toggle((bool)value, "", GUILayout.Width(110));
            if (t.IsEnum) return DrawEnum(value, t);
            if (t == typeof(string))
                return GUILayout.TextField((string)value ?? "", GUILayout.Width(220));

            string shown = buffers.TryGetValue(path, out var b) ? b : Format(value);
            string typed = GUILayout.TextField(shown, GUILayout.Width(110));
            if (typed == shown) return value;
            buffers[path] = typed;
            if (t == typeof(float) && float.TryParse(typed, NumberStyles.Float, Inv, out float fv)) return fv;
            if (t == typeof(int) && int.TryParse(typed, NumberStyles.Integer, Inv, out int iv)) return iv;
            return value;
        }

        static object DrawEnum(object value, Type t)
        {
            var values = Enum.GetValues(t);
            int idx = Array.IndexOf(values, value);
            GUILayout.BeginHorizontal(GUILayout.Width(220));
            if (GUILayout.Button("◀", GUILayout.Width(22))) idx = (idx - 1 + values.Length) % values.Length;
            GUILayout.Label(value.ToString(), GUILayout.Width(150));
            if (GUILayout.Button("▶", GUILayout.Width(22))) idx = (idx + 1) % values.Length;
            GUILayout.EndHorizontal();
            return values.GetValue(idx);
        }

        // ---------------------------------------------------------------- calculated

        void DrawCalculated()
        {
            var p = service.Draft;
            GUILayout.Label("CALCULATED from the draft profile (theory, not playtest data). Observed telemetry is recorded separately (T18).", GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Test profile:", GUILayout.Width(90));
            foreach (var tp in p.TestProfiles)
            {
                bool on = p.ActiveTestProfile == tp.Name;
                if (GUILayout.Toggle(on, tp.Name, GUI.skin.button) && !on) { p.ActiveTestProfile = tp.Name; edited = true; }
            }
            GUILayout.EndHorizontal();

            var test = p.ActiveProfile();
            GUILayout.Label("Phases", header);
            Row("Phase", "Base reg.", "Max reg.", "Boss+adds", "Max phase", "Phase XP", "Pre-boss coins", "Coins gen.", "Cum. XP", "Choices", "Boss own XP/coins");
            foreach (var t in BalanceCalculator.Phases(p, test))
                Row(t.Phase, t.BaseRegular, t.MaxRegular, t.BossAndAdds, t.MaxPhase, t.PhaseXp, t.PreBossCoins, t.CoinsGenerated, t.CumulativeXp, t.CumulativeChoices, $"{t.BossOwnXp}/{t.BossOwnCoins}");
            var all = BalanceCalculator.Phases(p, test);
            Row("Total", all.Sum(x => x.BaseRegular), all.Sum(x => x.MaxRegular), all.Sum(x => x.BossAndAdds), all.Sum(x => x.MaxPhase), all.Sum(x => x.PhaseXp), all.Sum(x => x.PreBossCoins), all.Sum(x => x.CoinsGenerated), "", "", "");

            GUILayout.Space(8);
            GUILayout.Label("Waves", header);
            Row("Wave", "Base", "Max", "Cap", "Ranged", "Gap", "Batches", "XP", "Kill coins", "Bonus", "");
            foreach (var w in BalanceCalculator.Waves(p, test))
                Row($"{w.Phase}-{w.Wave}{(w.IsTutorial ? " (tut)" : "")}", w.BaseCount, w.MaxTotal, w.ActiveCap, w.RangedCap, w.SpawnGap.ToString("0.###", Inv),
                    string.Join("/", w.Batches), w.Xp, w.KillCoins, w.BonusCoins, "");

            GUILayout.Space(8);
            GUILayout.Label("Weapons: sustained DPS (incl. reload, before skills/armour) and time to kill", header);
            var targets = new[] { EnemyId.A1_RunnerWolf, EnemyId.R1_Crawler, EnemyId.H1_BatonGuard, EnemyId.H4_ShieldGuard };
            Row(new object[] { "Weapon", "Lvl", "Cost", "DPS" }.Concat(targets.Select(x => (object)("TTK " + x.ToString().Split('_')[1]))).Concat(new object[] { "Status" }).ToArray());
            foreach (var w in p.Weapons)
            foreach (var l in w.Levels)
            {
                float dps = BalanceCalculator.SustainedDps(w, l);
                string status = !BalanceValidator.IsLevelComplete(w, l) ? "INCOMPLETE" : l.Approved ? "approved" : "placeholder";
                Row(new object[] { w.Id, l.Level, l.Cost, dps.ToString("0.0", Inv) }
                    .Concat(targets.Select(x => (object)BalanceCalculator.TimeToKill(p.Enemy(x).Health, dps).ToString("0.00", Inv) + "s"))
                    .Concat(new object[] { status }).ToArray());
            }
        }

        static void Row(params object[] cells)
        {
            GUILayout.BeginHorizontal();
            foreach (var c in cells) GUILayout.Label(c?.ToString() ?? "", GUILayout.Width(92));
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- issues

        void DrawIssues()
        {
            if (service.LastIssues.Count == 0) { GUILayout.Label("No issues."); return; }
            foreach (var i in service.LastIssues.OrderByDescending(x => x.Severity))
                GUILayout.Label(i.ToString(), i.Severity == IssueSeverity.Error ? errorLabel : i.Severity == IssueSeverity.Warning ? warnLabel : GUI.skin.label);
        }

        // ---------------------------------------------------------------- helpers

        bool Foldout(string path, string label, int depth)
        {
            bool searching = !string.IsNullOrEmpty(search);
            bool open = searching || expanded.Contains(path);
            GUILayout.BeginHorizontal();
            GUILayout.Space(Indent(depth));
            if (GUILayout.Button((open ? "▼ " : "► ") + label, GUI.skin.label))
            {
                if (expanded.Contains(path)) expanded.Remove(path); else expanded.Add(path);
                open = !open || searching;
            }
            GUILayout.EndHorizontal();
            return open;
        }

        bool MatchesSearch(string path) =>
            string.IsNullOrEmpty(search) || path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        static string Label(FieldInfo f) => f.Name;

        static string RecordLabel(object item, int index)
        {
            if (item == null) return $"[{index}] null";
            var t = item.GetType();
            object Get(string n) => t.GetField(n)?.GetValue(item);
            if (Get("Id") is { } id && t != typeof(EnemyCount)) return $"[{index}] {id}";
            if (t == typeof(EnemyCount)) { var c = (EnemyCount)item; return $"[{index}] {c.Id} × {c.Count}"; }
            if (Get("Wave") is int wave && Get("Phase") is int phase) return $"[{index}] Phase {phase} wave {wave}";
            if (Get("Level") is int lvl) return $"[{index}] Level {lvl}" + (Get("Approved") is false ? " (placeholder)" : "");
            if (Get("Name") is string name) return $"[{index}] {name}";
            if (Get("Phase") is int ph) return $"[{index}] Phase {ph}";
            if (Get("HealthFraction") is float hf) return $"[{index}] at {hf:P0} health";
            return $"[{index}] {t.Name}";
        }

        static bool IsLeaf(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string);

        static Type ListElementType(Type listType) =>
            listType.IsGenericType ? listType.GetGenericArguments()[0] : listType.GetElementType();

        static float Indent(int depth) => depth * 14f;

        static string Format(object v) => v switch
        {
            float f => f.ToString("0.####", Inv),
            null => "",
            _ => v.ToString(),
        };

        void EnsureStyles()
        {
            if (header != null) return;
            header = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            tagGdd = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.6f, 0.7f, 0.85f) }, fontSize = 10 };
            tagPlaceholder = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.6f, 0.2f) }, fontSize = 10, fontStyle = FontStyle.Bold };
            changedLabel = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.9f, 0.3f) } };
            errorLabel = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.4f, 0.4f) }, wordWrap = true };
            warnLabel = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.75f, 0.3f) }, wordWrap = true };
        }
    }
}
