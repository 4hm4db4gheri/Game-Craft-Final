using System;
using System.Collections.Generic;
using System.Globalization;
using Hollow.Data;
using Hollow.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollow.UI
{
    /// <summary>
    /// XP choice (§9, §11): the world is paused while it shows; each card shows the skill, its rank and the
    /// value before and after so the player can explain the change (§15 "Understand upgrades").
    /// </summary>
    public sealed class XpCardScreen : MonoBehaviour
    {
        UIRoot root;
        RectTransform panel;
        TextMeshProUGUI title;
        readonly List<GameObject> cards = new();

        public bool Visible => panel != null && panel.gameObject.activeSelf;

        public void Init(UIRoot uiRoot)
        {
            root = uiRoot;
            var theme = root.Theme;
            panel = UIFactory.Stretch(root.Modal, "XpChoice");
            UIFactory.Panel(panel, theme, new Color(0, 0, 0, 0.55f), raycast: true);
            title = UIFactory.Text(panel, "Title", "", theme, 44, TextAlignmentOptions.Top);
            title.rectTransform.offsetMax = new Vector2(0, -170);
            panel.gameObject.SetActive(false);
        }

        public void Show(World w, PendingChoice card, Action<SkillId> onChoose)
        {
            foreach (var c in cards) Destroy(c);
            cards.Clear();
            var theme = root.Theme;
            title.text = $"<b>UPGRADE</b>\n<size=26><color=#{ColorUtility.ToHtmlStringRGB(theme.TextDim)}>The world is paused. Choose one.</color></size>";

            int n = card.Options.Count;
            float width = 420, gap = 40;
            float start = -(n - 1) * (width + gap) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var id = card.Options[i];
                var skill = w.Profile.Skill(id);
                int rank = w.Player.SkillRank(id);
                var (before, after) = XpSystem.Preview(w, id);
                string label =
                    $"<size=34><b>{skill.DisplayName}</b></size>\n" +
                    $"<size=24><color=#{ColorUtility.ToHtmlStringRGB(theme.TextDim)}>Rank {rank} → {rank + 1} of {skill.Ranks.Count}</color></size>\n\n" +
                    $"<size=40>{Format(id, before)}  <color=#{ColorUtility.ToHtmlStringRGB(theme.Accent)}>→  {Format(id, after)}</color></size>";
                var b = UIFactory.Button(panel, $"Card{i}", label, theme, new Vector2(0.5f, 0.5f), new Vector2(start + i * (width + gap), -40),
                    new Vector2(width, 360), () => onChoose(id), out _);
                cards.Add(b.gameObject);
            }
            panel.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Format(SkillId id, float v) => id switch
        {
            SkillId.WeaponDamage => $"{v * 100f:0}%",
            SkillId.ReloadMultiplier => $"×{v.ToString("0.00", Inv)} reload time",
            SkillId.DashCooldown => $"{v.ToString("0.##", Inv)} s",
            SkillId.MoveSpeed => $"{v.ToString("0.0", Inv)} u/s",
            SkillId.MagnetRadius => $"{v.ToString("0.0", Inv)} u",
            _ => v.ToString("0", Inv),
        };
    }
}
