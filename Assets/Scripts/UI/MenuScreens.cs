using System;
using System.Collections.Generic;
using System.Globalization;
using Hollow.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollow.UI
{
    public enum SlotPickMode { NewGame, Continue, Load }

    /// <summary>Settings shown and edited by the settings screen; the App layer persists them.</summary>
    public interface ISettingsModel
    {
        float MusicVolume { get; set; }
        float EffectsVolume { get; set; }
        float ShakeIntensity { get; set; }
        bool Fullscreen { get; set; }
        bool DamageNumbers { get; set; }
        int EffectBudget { get; set; }
        void Save();
    }

    /// <summary>
    /// All menu-type screens of §11 (main menu, slot pickers, confirmations, pause, loading, settings,
    /// controls). One container; each Show* replaces its content. Every screen has a way back (no dead ends).
    /// </summary>
    public sealed class MenuScreens : MonoBehaviour
    {
        UIRoot root;
        RectTransform layer;
        RectTransform content;

        public bool Visible => content != null;

        public void Init(UIRoot uiRoot)
        {
            root = uiRoot;
            layer = UIFactory.Stretch(root.Modal, "Menus");
        }

        public void Hide()
        {
            if (content != null) Destroy(content.gameObject);
            content = null;
        }

        RectTransform Begin(string title, string subtitle = null, float dim = 0.7f, float width = 720, float height = 760)
        {
            Hide();
            content = UIFactory.Stretch(layer, "Screen");
            UIFactory.Panel(content, root.Theme, new Color(0, 0, 0, dim), raycast: true);
            var box = UIFactory.Rect(content, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            UIFactory.Panel(box, root.Theme, null, raycast: true);
            var head = UIFactory.Rect(box, "Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(width - 40, 130));
            string sub = subtitle != null ? $"\n<size=24><color=#{ColorUtility.ToHtmlStringRGB(root.Theme.TextDim)}>{subtitle}</color></size>" : "";
            UIFactory.Text(head, "T", $"<b>{title}</b>{sub}", root.Theme, 48, TextAlignmentOptions.Top);
            return box;
        }

        Button Btn(RectTransform box, string label, float y, Action onClick, bool enabled = true, float width = 460, float height = 64)
        {
            var b = UIFactory.Button(box, label, label, root.Theme, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(width, height), onClick, out _);
            b.interactable = enabled;
            return b;
        }

        // ---------------------------------------------------------------- main menu

        public void ShowMain(Action newGame, Action continueGame, bool anySave, Action settings, Action controls, Action quit)
        {
            var box = Begin("HOLLOW", "A small helpful AI. A very productive facility.", 0.35f);
            float y = -230;
            Btn(box, "New game", y, newGame); y -= 80;
            Btn(box, "Continue", y, continueGame, anySave); y -= 80;
            Btn(box, "Settings", y, settings); y -= 80;
            Btn(box, "Controls", y, controls); y -= 80;
            Btn(box, "Quit", y, quit);
        }

        // ---------------------------------------------------------------- slots

        public void ShowSlots(SlotPickMode mode, Action<int> pick, Action back)
        {
            string title = mode == SlotPickMode.NewGame ? "New game" : mode == SlotPickMode.Continue ? "Continue" : "Load";
            string sub = mode == SlotPickMode.NewGame ? "Choose a slot. Each slot is its own game." : "Choose a saved game.";
            var box = Begin(title, sub, 0.7f, 900, 760);
            float y = -190;
            for (int slot = 1; slot <= SlotStore.SlotCount; slot++)
            {
                int s = slot;
                var summary = SlotStore.TryReadSummary(slot);
                string label = summary == null ? $"Slot {slot} — empty" : $"Slot {slot} — {Describe(summary)}";
                bool enabled = mode == SlotPickMode.NewGame || summary != null;
                var b = Btn(box, label, y, () => OnSlot(mode, s, summary, pick, back), enabled, 820, 120);
                b.GetComponentInChildren<TextMeshProUGUI>().fontSize = 24;
                y -= 135;
            }
            Btn(box, "Back", y - 10, back, true, 300);
        }

        void OnSlot(SlotPickMode mode, int slot, SlotSummary summary, Action<int> pick, Action back)
        {
            if (mode == SlotPickMode.NewGame && summary != null)
            {
                ShowConfirm($"Replace slot {slot}?", $"{Describe(summary)}\nThis game will be lost.",
                    ("Replace", () => pick(slot)), ("Cancel", () => ShowSlots(mode, pick, back)));
                return;
            }
            pick(slot);
        }

        public static string Describe(SlotSummary s)
        {
            string where = s.Completed ? "completed" : s.Encounter == nameof(EncounterKind.BossPrep) || s.Encounter == nameof(EncounterKind.Boss)
                ? $"Phase {s.Phase} · boss" : $"Phase {s.Phase} · wave {s.Wave}";
            var t = TimeSpan.FromSeconds(s.PlaytimeSeconds);
            string saved = DateTime.TryParse(s.SavedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
                ? d.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "";
            return $"{where} · {(int)t.TotalMinutes}:{t.Seconds:00} played · {s.Weapon} Lv{s.WeaponLevel}\n<size=18>saved {saved}</size>";
        }

        // ---------------------------------------------------------------- confirm

        public void ShowConfirm(string title, string message, params (string label, Action action)[] options)
        {
            var box = Begin(title, message, 0.75f, 760, 420);
            float x0 = -(options.Length - 1) * 120f;
            for (int i = 0; i < options.Length; i++)
            {
                var (label, action) = options[i];
                UIFactory.Button(box, label, label, root.Theme, new Vector2(0.5f, 0), new Vector2(x0 + i * 240, 70), new Vector2(220, 64), action, out _, 24);
            }
        }

        // ---------------------------------------------------------------- pause

        public void ShowPause(Action resume, Action save, bool canSave, Action load, Action settings, Action controls, Action mainMenu, string status = null)
        {
            var box = Begin("Paused", status, 0.6f, 620, 760);
            float y = -190;
            Btn(box, "Resume", y, resume); y -= 80;
            Btn(box, canSave ? "Save" : "Save (not now)", y, save, canSave); y -= 80;
            Btn(box, "Load", y, load); y -= 80;
            Btn(box, "Settings", y, settings); y -= 80;
            Btn(box, "Controls", y, controls); y -= 80;
            Btn(box, "Main menu", y, mainMenu);
        }

        // ---------------------------------------------------------------- loading

        public void ShowLoading(string description, Action onContinue)
        {
            var box = Begin("Game loaded", description + "\n\nTime is frozen until you continue.", 0.8f, 760, 420);
            UIFactory.Button(box, "Continue", "Continue", root.Theme, new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(300, 64), onContinue, out _);
        }

        // ---------------------------------------------------------------- settings

        public void ShowSettings(ISettingsModel s, Action back)
        {
            var box = Begin("Settings", null, 0.7f, 760, 760);
            float y = -170;
            Slider(box, "Music volume", s.MusicVolume, v => s.MusicVolume = v, ref y);
            Slider(box, "Effects volume", s.EffectsVolume, v => s.EffectsVolume = v, ref y);
            Slider(box, "Camera shake", s.ShakeIntensity, v => s.ShakeIntensity = v, ref y);
            Toggle(box, "Fullscreen", s.Fullscreen, v => s.Fullscreen = v, ref y);
            Toggle(box, "Damage numbers", s.DamageNumbers, v => s.DamageNumbers = v, ref y);
            string[] budgets = { "Low", "Medium", "High" };
            Btn(box, $"Effects: {budgets[s.EffectBudget]}", y, () => { s.EffectBudget = (s.EffectBudget + 1) % 3; ShowSettings(s, back); });
            y -= 90;
            Btn(box, "Back", y, () => { s.Save(); back(); }, true, 300);
        }

        void Slider(RectTransform box, string label, float value, Action<float> set, ref float y)
        {
            var row = UIFactory.Rect(box, label, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(660, 56));
            var text = UIFactory.Text(row, "L", label, root.Theme, 26, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.offsetMax = new Vector2(-330, 0);
            var sliderRt = UIFactory.Rect(row, "Slider", new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(320, 30));
            var bg = sliderRt.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.5f);
            var fillArea = UIFactory.Stretch(sliderRt, "FillArea");
            var fill = UIFactory.Stretch(fillArea, "Fill");
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = root.Theme.Accent;
            var slider = sliderRt.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill;
            slider.targetGraphic = bg;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = value;
            slider.onValueChanged.AddListener(v => set(v));
            y -= 76;
        }

        void Toggle(RectTransform box, string label, bool value, Action<bool> set, ref float y)
        {
            bool current = value;
            Button b = null;
            b = Btn(box, $"{label}: {(current ? "on" : "off")}", y, () =>
            {
                current = !current;
                set(current);
                b.GetComponentInChildren<TextMeshProUGUI>().text = $"{label}: {(current ? "on" : "off")}";
            });
            y -= 80;
        }

        // ---------------------------------------------------------------- controls

        public void ShowControls(Action back)
        {
            var box = Begin("Controls", null, 0.7f, 900, 820);
            var rows = new List<(string key, string action)>
            {
                ("WASD", "Move (independent of aim)"),
                ("Mouse / left click", "Aim and attack; use the held item"),
                ("Space", "Dash in the movement direction"),
                ("R", "Reload (empty magazines reload automatically)"),
                ("1 / 2 / mouse wheel", "Pistol / primary weapon"),
                ("F", "Equip the first item (does not use it)"),
                ("G", "Cycle items"),
                ("Right click", "Cancel the held item"),
                ("E", "Open or close the shop when near it"),
                ("Enter", "Start the next wave early"),
                ("Esc", "Pause"),
            };
            var sb = new System.Text.StringBuilder();
            foreach (var (key, action) in rows) sb.Append($"<b>{key}</b><pos=40%>{action}\n");
            var area = UIFactory.Rect(box, "Table", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(820, 560));
            UIFactory.Text(area, "T", sb.ToString(), root.Theme, 26, TextAlignmentOptions.TopLeft);
            UIFactory.Button(box, "Back", "Back", root.Theme, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(300, 64), back, out _);
        }
    }
}
