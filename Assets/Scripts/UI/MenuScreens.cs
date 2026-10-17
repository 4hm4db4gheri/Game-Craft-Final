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
        float VoiceVolume { get; set; }
        bool Subtitles { get; set; }
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
            var box = Begin(Loc.T("menu.title"), Loc.T("menu.tagline"), 0.35f);
            float y = -230;
            Btn(box, Loc.T("menu.newGame"), y, newGame); y -= 80;
            Btn(box, Loc.T("menu.continue"), y, continueGame, anySave); y -= 80;
            Btn(box, Loc.T("menu.settings"), y, settings); y -= 80;
            Btn(box, Loc.T("menu.controls"), y, controls); y -= 80;
            Btn(box, Loc.T("menu.quit"), y, quit);
        }

        // ---------------------------------------------------------------- slots

        public void ShowSlots(SlotPickMode mode, Action<int> pick, Action back)
        {
            string title = Loc.T(mode == SlotPickMode.NewGame ? "menu.newGame" : mode == SlotPickMode.Continue ? "menu.continue" : "slots.load");
            string sub = Loc.T(mode == SlotPickMode.NewGame ? "slots.subNew" : "slots.subPick");
            var box = Begin(title, sub, 0.7f, 900, 760);
            float y = -190;
            for (int slot = 1; slot <= SlotStore.SlotCount; slot++)
            {
                int s = slot;
                var summary = SlotStore.TryReadSummary(slot);
                string label = summary == null ? Loc.F("slots.empty", slot) : Loc.F("slots.filled", slot, Describe(summary));
                bool enabled = mode == SlotPickMode.NewGame || summary != null;
                var b = Btn(box, label, y, () => OnSlot(mode, s, summary, pick, back), enabled, 820, 120);
                b.GetComponentInChildren<TextMeshProUGUI>().fontSize = 24;
                y -= 135;
            }
            Btn(box, Loc.T("common.back"), y - 10, back, true, 300);
        }

        void OnSlot(SlotPickMode mode, int slot, SlotSummary summary, Action<int> pick, Action back)
        {
            if (mode == SlotPickMode.NewGame && summary != null)
            {
                ShowConfirm(Loc.F("slots.replaceTitle", slot), Loc.F("slots.replaceBody", Describe(summary)),
                    (Loc.T("slots.replace"), () => pick(slot)), (Loc.T("common.cancel"), () => ShowSlots(mode, pick, back)));
                return;
            }
            pick(slot);
        }

        public static string Describe(SlotSummary s)
        {
            string where = s.Completed ? Loc.T("slots.completed") : s.Encounter == nameof(EncounterKind.BossPrep) || s.Encounter == nameof(EncounterKind.Boss)
                ? Loc.F("slots.whereBoss", s.Phase) : Loc.F("slots.whereWave", s.Phase, s.Wave);
            var t = TimeSpan.FromSeconds(s.PlaytimeSeconds);
            string saved = DateTime.TryParse(s.SavedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
                ? SolarDate(d.ToLocalTime()) : "";
            if (s.Ai != Hollow.Data.AiId.None) where = Loc.T("ai." + s.Ai) + " · " + where;
            return Loc.F("slots.describe", where, $"{(int)t.TotalMinutes}:{t.Seconds:00}", Loc.Weapon(s.Weapon), s.WeaponLevel, saved);
        }

        /// <summary>Save times in the Persian (solar hijri) calendar, as the player reads dates.</summary>
        public static string SolarDate(DateTime local)
        {
            var pc = new PersianCalendar();
            return $"{pc.GetYear(local):0000}/{pc.GetMonth(local):00}/{pc.GetDayOfMonth(local):00} {local:HH:mm}";
        }

        // ---------------------------------------------------------------- AI selection (v0.19 §3)

        /// <summary>Four cards: the AI, its starting bonus and the ranks left to earn. The bonus is fiction.</summary>
        public void ShowAiSelect(Hollow.Data.BalanceProfile profile, Action<Hollow.Data.AiId> pick, Action back)
        {
            var box = Begin(Loc.T("ai.title"), Loc.T("ai.subtitle"), 0.75f, 1500, 700);
            var chars = profile.Characters;
            float width = 330, gap = 22;
            float x0 = (chars.Count - 1) * (width + gap) * 0.5f; // first card on the right: Persian reads right to left
            for (int i = 0; i < chars.Count; i++)
            {
                var c = chars[i];
                var ranks = profile.Skill(c.StartingSkill).Ranks;
                int start = Mathf.Clamp(c.StartingRank, 1, ranks.Count);
                var later = new List<string>();
                for (int r = start; r < ranks.Count; r++) later.Add(XpCardScreen.Format(c.StartingSkill, ranks[r]));
                string dim = ColorUtility.ToHtmlStringRGB(root.Theme.TextDim);
                string accent = ColorUtility.ToHtmlStringRGB(root.Theme.Accent);
                string label =
                    $"<size=40><b>{Loc.T("ai." + c.Id)}</b></size>\n\n" +
                    $"<size=22><color=#{dim}>{Loc.T("ai.startsWith")}</color></size>\n" +
                    $"<size=26>{Loc.Skill(c.StartingSkill)}</size>\n" +
                    $"<size=34><color=#{accent}>{XpCardScreen.Format(c.StartingSkill, ranks[start - 1])}</color></size>\n\n" +
                    $"<size=20><color=#{dim}>{Loc.F("ai.later", string.Join("، ", later))}</color></size>";
                var id = c.Id;
                UIFactory.Button(box, $"Ai{c.Id}", label, root.Theme, new Vector2(0.5f, 0.5f), new Vector2(x0 - i * (width + gap), 20),
                    new Vector2(width, 400), () => pick(id), out _);
            }
            var note = UIFactory.Rect(box, "Note", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(1400, 40));
            UIFactory.Text(note, "T", Loc.T("ai.disclaimer"), root.Theme, 18, TextAlignmentOptions.Center, root.Theme.TextDim);
            UIFactory.Button(box, "Back", Loc.T("common.back"), root.Theme, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(300, 64), back, out _);
        }

        // ---------------------------------------------------------------- confirm

        public void ShowConfirm(string title, string message, params (string label, Action action)[] options)
        {
            var box = Begin(title, message, 0.75f, 760, 420);
            float x0 = -(options.Length - 1) * 120f;
            for (int i = 0; i < options.Length; i++)
            {
                var (label, action) = options[i];
                // Right to left: the first (main) option sits on the right.
                UIFactory.Button(box, label, label, root.Theme, new Vector2(0.5f, 0), new Vector2(-(x0 + i * 240), 70), new Vector2(220, 64), action, out _, 24);
            }
        }

        // ---------------------------------------------------------------- pause

        public void ShowPause(Action resume, Action save, bool canSave, Action load, Action settings, Action controls, Action mainMenu, string status = null)
        {
            var box = Begin(Loc.T("pause.title"), status, 0.6f, 620, 760);
            float y = -190;
            Btn(box, Loc.T("pause.resume"), y, resume); y -= 80;
            Btn(box, Loc.T(canSave ? "pause.save" : "pause.saveDisabled"), y, save, canSave); y -= 80;
            Btn(box, Loc.T("slots.load"), y, load); y -= 80;
            Btn(box, Loc.T("menu.settings"), y, settings); y -= 80;
            Btn(box, Loc.T("menu.controls"), y, controls); y -= 80;
            Btn(box, Loc.T("common.mainMenu"), y, mainMenu);
        }

        // ---------------------------------------------------------------- loading

        public void ShowLoading(string description, Action onContinue)
        {
            var box = Begin(Loc.T("loading.title"), Loc.F("loading.body", description), 0.8f, 760, 420);
            UIFactory.Button(box, "Continue", Loc.T("menu.continue"), root.Theme, new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(300, 64), onContinue, out _);
        }

        // ---------------------------------------------------------------- settings

        public void ShowSettings(ISettingsModel s, Action back)
        {
            var box = Begin(Loc.T("menu.settings"), null, 0.7f, 760, 920);
            float y = -160;
            Slider(box, Loc.T("settings.music"), s.MusicVolume, v => s.MusicVolume = v, ref y);
            Slider(box, Loc.T("settings.effects"), s.EffectsVolume, v => s.EffectsVolume = v, ref y);
            Slider(box, Loc.T("settings.voice"), s.VoiceVolume, v => s.VoiceVolume = v, ref y);
            Slider(box, Loc.T("settings.shake"), s.ShakeIntensity, v => s.ShakeIntensity = v, ref y);
            y -= 24; // sliders are top-anchored rows, buttons are centred: keep them apart
            Toggle(box, Loc.T("settings.subtitles"), s.Subtitles, v => s.Subtitles = v, ref y);
            Toggle(box, Loc.T("settings.fullscreen"), s.Fullscreen, v => s.Fullscreen = v, ref y);
            Toggle(box, Loc.T("settings.damageNumbers"), s.DamageNumbers, v => s.DamageNumbers = v, ref y);
            string[] budgets = { Loc.T("settings.budgetLow"), Loc.T("settings.budgetMedium"), Loc.T("settings.budgetHigh") };
            Btn(box, Loc.F("settings.effectBudget", budgets[s.EffectBudget]), y, () => { s.EffectBudget = (s.EffectBudget + 1) % 3; ShowSettings(s, back); });
            y -= 90;
            Btn(box, Loc.T("common.back"), y, () => { s.Save(); back(); }, true, 300);
        }

        void Slider(RectTransform box, string label, float value, Action<float> set, ref float y)
        {
            var row = UIFactory.Rect(box, label, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(660, 56));
            // Right to left: the label starts at the right edge, the slider sits on the left.
            var text = UIFactory.Text(row, "L", label, root.Theme, 26, TextAlignmentOptions.MidlineRight);
            text.rectTransform.offsetMin = new Vector2(330, 0);
            var sliderRt = UIFactory.Rect(row, "Slider", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(320, 30));
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
            b = Btn(box, Loc.F("settings.toggle", label, Loc.T(current ? "common.on" : "common.off")), y, () =>
            {
                current = !current;
                set(current);
                b.GetComponentInChildren<TextMeshProUGUI>().text = Loc.F("settings.toggle", label, Loc.T(current ? "common.on" : "common.off"));
            });
            y -= 80;
        }

        // ---------------------------------------------------------------- controls

        public void ShowControls(Action back)
        {
            var box = Begin(Loc.T("menu.controls"), null, 0.7f, 900, 820);
            string[] rows = { "move", "aim", "dash", "reload", "weapons", "equipItem", "cycleItem", "cancelItem", "shop", "startWave", "pause" };
            var keys = new System.Text.StringBuilder();
            var actions = new System.Text.StringBuilder();
            foreach (var row in rows)
            {
                keys.Append("<b>").Append(Loc.T("controls.key." + row)).Append("</b>\n");
                actions.Append(Loc.T("controls." + row)).Append('\n');
            }
            // Two columns, one line per row: keys on the right (where Persian starts), actions to their left.
            var area = UIFactory.Rect(box, "Table", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(820, 560));
            var keyText = UIFactory.Text(area, "Keys", keys.ToString(), root.Theme, 26, TextAlignmentOptions.TopRight);
            keyText.textWrappingMode = TextWrappingModes.NoWrap;
            keyText.rectTransform.offsetMin = new Vector2(560, 0);
            var actionText = UIFactory.Text(area, "Actions", actions.ToString(), root.Theme, 26, TextAlignmentOptions.TopRight);
            actionText.textWrappingMode = TextWrappingModes.NoWrap;
            actionText.rectTransform.offsetMax = new Vector2(-280, 0);
            UIFactory.Button(box, "Back", Loc.T("common.back"), root.Theme, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(300, 64), back, out _);
        }
    }
}
