using System.Text;
using Hollow.Data;
using Hollow.Sim;
using TMPro;
using UnityEngine;

namespace Hollow.UI
{
    /// <summary>
    /// HUD per GDD §10: lower left health (and companion), lower right weapons with level, magazine,
    /// unlimited reserve and reload progress, slim XP bar, top wallet / phase / wave / enemies.
    /// Never a raycast target, so it can never swallow a world click.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        UIRoot root;
        BarView health, companion, xp, reload;
        TextMeshProUGUI healthText, companionText, weaponText, xpText, walletText, topText, statusText;
        readonly StringBuilder sb = new(128);

        public void Init(UIRoot uiRoot)
        {
            root = uiRoot;
            var t = root.Theme;
            var hud = root.Hud;

            health = UIFactory.Bar(hud, "Health", t, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 70), new Vector2(420, 26), t.Health);
            healthText = UIFactory.Text(health.transform, "Text", "", t, 20, TextAlignmentOptions.Center);
            companion = UIFactory.Bar(hud, "Companion", t, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 106), new Vector2(300, 18), t.CompanionHealth);
            companionText = UIFactory.Text(companion.transform, "Text", "", t, 16, TextAlignmentOptions.Center);
            companion.gameObject.SetActive(false);

            var weapon = UIFactory.Rect(hud, "Weapon", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 60), new Vector2(460, 110));
            UIFactory.Panel(weapon, t);
            weaponText = UIFactory.Text(weapon, "Text", "", t, 24, TextAlignmentOptions.MidlineRight);
            weaponText.margin = new Vector4(16, 8, 16, 8);
            reload = UIFactory.Bar(weapon, "Reload", t, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(428, 8), t.Accent);

            xp = UIFactory.Bar(hud, "Xp", t, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(900, 10), t.Xp);
            xpText = UIFactory.Text(xp.transform, "Text", "", t, 16, TextAlignmentOptions.Center, t.TextDim);
            xpText.rectTransform.anchoredPosition = new Vector2(0, 16);

            var top = UIFactory.Rect(hud, "Top", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(900, 48));
            topText = UIFactory.Text(top, "Text", "", t, 26, TextAlignmentOptions.Center);
            var wallet = UIFactory.Rect(hud, "Wallet", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -16), new Vector2(300, 48));
            walletText = UIFactory.Text(wallet, "Text", "", t, 32, TextAlignmentOptions.MidlineRight, t.Coin);
            var status = UIFactory.Rect(hud, "Status", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1200, 90));
            statusText = UIFactory.Text(status, "Text", "", t, 30, TextAlignmentOptions.Center, t.Accent);
        }

        public void SetStatus(string message) => statusText.text = message ?? "";
        public void SetTop(string text) => topText.text = text ?? "";

        public void Sync(World w)
        {
            var p = w.Player;
            float max = PlayerStats.MaxHealth(w);
            health.Set(p.Health / max);
            healthText.text = $"{Mathf.CeilToInt(p.Health)} / {max:0}";

            var L = p.Loadout;
            if (L.Owned.Count > 0)
            {
                sb.Clear();
                AppendWeapon(w, L.SlotPistol, L.ActiveSlot == 1, 1);
                if (L.HasPrimary) { sb.Append('\n'); AppendWeapon(w, L.SlotPrimary, L.ActiveSlot == 2, 2); }
                weaponText.text = sb.ToString();
                reload.gameObject.SetActive(L.Reloading);
                if (L.Reloading)
                    reload.Set(1f - (L.ReloadEndTick - w.Clock.Tick) / (float)Mathf.Max(1, L.ReloadTotalTicks));
            }

            int next = XpSystem.NextThreshold(w);
            bool maxed = XpSystem.AllSkillsMaxed(w);
            xp.Set(maxed ? 1f : w.Progress.XpTowardNext / (float)Mathf.Max(1, next));
            xpText.text = maxed ? $"XP {w.Progress.TotalXp}" : $"XP {w.Progress.XpTowardNext} / {next}";
            walletText.text = $"{w.Progress.Wallet} ¢";
        }

        void AppendWeapon(World w, WeaponId id, bool active, int key)
        {
            var o = w.Player.Loadout.Get(id);
            if (o == null) return;
            var data = w.Profile.Weapon(id);
            var level = WeaponSystem.LevelData(w, o);
            string dim = ColorUtility.ToHtmlStringRGB(root.Theme.TextDim);
            if (!active) sb.Append($"<color=#{dim}>");
            sb.Append($"[{key}] {data.DisplayName} Lv{o.Level}   ");
            if (data.Kind == WeaponKind.Gun) sb.Append($"{o.Ammo}/{level.Magazine} | ∞");
            else sb.Append("melee");
            if (!active) sb.Append("</color>");
        }
    }
}
