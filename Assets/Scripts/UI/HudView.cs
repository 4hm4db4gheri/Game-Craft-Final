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
        BarView health, companion, xp, reload, bossBar;
        TextMeshProUGUI bossText;
        TextMeshProUGUI healthText, companionText, weaponText, xpText, walletText, topText, statusText;
        UnityEngine.UI.Image companionArrow, guideArrow;
        readonly StringBuilder sb = new(128);
        UnityEngine.UI.Button itemButton;
        TextMeshProUGUI itemText;
        UnityEngine.UI.Image itemFrame;
        BarView itemGap;
        ItemId shownItem;

        /// <summary>Clicking the item slot only equips it (v0.19 §8).</summary>
        public System.Action<ItemId> ItemClicked;

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
            xpText.rectTransform.anchoredPosition = new Vector2(-545, 0); // beside the bar: above it the item buttons would cover it at 1280×720

            var top = UIFactory.Rect(hud, "Top", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(900, 48));
            topText = UIFactory.Text(top, "Text", "", t, 26, TextAlignmentOptions.Center);
            var wallet = UIFactory.Rect(hud, "Wallet", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -16), new Vector2(300, 48));
            walletText = UIFactory.Text(wallet, "Text", "", t, 32, TextAlignmentOptions.MidlineRight, t.Coin);
            var status = UIFactory.Rect(hud, "Status", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -175), new Vector2(1200, 90));
            statusText = UIFactory.Text(status, "Text", "", t, 30, TextAlignmentOptions.Center, t.Accent);

            itemButton = UIFactory.Button(hud, "Item", "", t, new Vector2(0.5f, 0), new Vector2(0, 75), new Vector2(300, 76),
                () => ItemClicked?.Invoke(shownItem), out itemText, 20);
            itemFrame = itemButton.GetComponent<UnityEngine.UI.Image>();
            itemGap = UIFactory.Bar(itemButton.transform, "Gap", t, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(280, 5), t.Accent);

            bossBar = UIFactory.Bar(hud, "Boss", t, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(900, 22), t.Warning);
            bossText = UIFactory.Text(bossBar.transform, "Name", "", t, 22, TextAlignmentOptions.Center);
            bossText.rectTransform.anchoredPosition = new Vector2(0, 26);
            bossBar.gameObject.SetActive(false);

            var arrow = UIFactory.Rect(hud, "CompanionArrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            companionArrow = UIFactory.Triangle(arrow, t.Warning);
            arrow.gameObject.SetActive(false);

            var guide = UIFactory.Rect(hud, "GuideArrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            guideArrow = UIFactory.Triangle(guide, t.Accent);
            guide.gameObject.SetActive(false);
        }

        /// <summary>
        /// Tutorial guide (§6, §15 "Understand the start"): points at a place without words — from the screen edge
        /// when it is off screen, bobbing above it when it is visible. Null hides it.
        /// </summary>
        public void SetGuide(Vector2? worldTarget, Camera cam)
        {
            var rt = (RectTransform)guideArrow.transform.parent;
            rt.gameObject.SetActive(worldTarget.HasValue && cam != null);
            if (!worldTarget.HasValue || cam == null) return;
            var hudRect = root.Hud.rect;
            Vector3 vp = cam.WorldToViewportPoint(worldTarget.Value);
            bool offScreen = vp.x < 0.05f || vp.x > 0.95f || vp.y < 0.05f || vp.y > 0.95f;
            if (offScreen)
            {
                Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                Vector2 edge = dir * (0.42f / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y)));
                rt.anchoredPosition = new Vector2(edge.x * hudRect.width, edge.y * hudRect.height);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            }
            else
            {
                Vector3 above = cam.WorldToViewportPoint(worldTarget.Value + Vector2.up * (2.6f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f)));
                rt.anchoredPosition = new Vector2((above.x - 0.5f) * hudRect.width, (above.y - 0.5f) * hudRect.height);
                rt.localRotation = Quaternion.Euler(0, 0, 180f); // pointing down at the place
            }
        }

        /// <summary>Boss bar during encounters (§10); the human boss bar is body and equipment together.</summary>
        public void SetBoss(string name, float fraction)
        {
            bossBar.gameObject.SetActive(name != null);
            if (name == null) return;
            bossBar.Set(fraction);
            bossText.text = name;
        }

        public void SetStatus(string message)
        {
            if (message == lastStatus) return;
            lastStatus = message;
            statusText.text = message ?? "";
        }

        public void SetTop(string text)
        {
            if (text == lastTop) return;
            lastTop = text;
            topText.text = text ?? "";
        }

        string dimHex;
        string DimHex => dimHex ??= ColorUtility.ToHtmlStringRGB(root.Theme.TextDim);

        // Texts are rebuilt only when what they show changes: no string garbage per frame in combat (G7, T21).
        int lastHealth = int.MinValue, lastMaxHealth, lastXpToward = int.MinValue, lastXpNext, lastXpTotal = int.MinValue, lastWallet = int.MinValue;
        int lastWeaponKey;
        bool lastMaxed;
        string lastTop, lastStatus;

        public void Sync(World w, Camera cam)
        {
            SyncCompanion(w, cam);
            SyncItems(w);
            var p = w.Player;
            float max = PlayerStats.MaxHealth(w);
            health.Set(p.Health / max);
            int hp = Mathf.CeilToInt(p.Health), maxHp = Mathf.RoundToInt(max);
            if (hp != lastHealth || maxHp != lastMaxHealth)
            {
                lastHealth = hp;
                lastMaxHealth = maxHp;
                healthText.text = $"{hp} / {maxHp}";
            }

            var L = p.Loadout;
            if (L.Owned.Count > 0)
            {
                int key = WeaponKey(w);
                if (key != lastWeaponKey)
                {
                    lastWeaponKey = key;
                    sb.Clear();
                    AppendWeapon(w, L.SlotPistol, L.ActiveSlot == 1, 1);
                    if (L.HasPrimary) { sb.Append('\n'); AppendWeapon(w, L.SlotPrimary, L.ActiveSlot == 2, 2); }
                    weaponText.text = sb.ToString();
                }
                reload.gameObject.SetActive(L.Reloading);
                if (L.Reloading)
                    reload.Set(1f - (L.ReloadEndTick - w.Clock.Tick) / (float)Mathf.Max(1, L.ReloadTotalTicks));
            }

            int next = XpSystem.NextThreshold(w);
            bool maxed = XpSystem.AllSkillsMaxed(w);
            xp.Set(maxed ? 1f : w.Progress.XpTowardNext / (float)Mathf.Max(1, next));
            if (maxed != lastMaxed || w.Progress.XpTowardNext != lastXpToward || next != lastXpNext || (maxed && w.Progress.TotalXp != lastXpTotal))
            {
                lastMaxed = maxed;
                lastXpToward = w.Progress.XpTowardNext;
                lastXpNext = next;
                lastXpTotal = w.Progress.TotalXp;
                xpText.text = maxed ? Loc.F("hud.xpTotal", w.Progress.TotalXp) : Loc.F("hud.xp", w.Progress.XpTowardNext, next);
            }
            if (w.Progress.Wallet != lastWallet)
            {
                lastWallet = w.Progress.Wallet;
                walletText.text = Loc.F("common.coins", w.Progress.Wallet);
            }
        }

        /// <summary>Everything the weapon panel shows, folded into one number.</summary>
        static int WeaponKey(World w)
        {
            var L = w.Player.Loadout;
            int h = L.ActiveSlot * 31 + (L.HasPrimary ? 7 : 0);
            foreach (var o in L.Owned) h = h * 397 ^ ((int)o.Id * 1000003 + o.Level * 1009 + o.Ammo);
            return h;
        }

        void AppendWeapon(World w, WeaponId id, bool active, int key)
        {
            var o = w.Player.Loadout.Get(id);
            if (o == null) return;
            var data = w.Profile.Weapon(id);
            var level = WeaponSystem.LevelData(w, o);
            if (!active) sb.Append("<color=#").Append(DimHex).Append('>');
            sb.Append(Loc.F("hud.weapon", key, Loc.Weapon(id), o.Level)).Append("   ");
            if (data.Kind == WeaponKind.Gun) sb.Append(Loc.F("hud.ammo", o.Ammo, level.Magazine));
            else sb.Append(Loc.T("hud.melee"));
            if (!active) sb.Append("</color>");
        }
    
        int itemKey = -1;

        /// <summary>
        /// One slot (v0.19 §8): the selected type, its count, how many other types are carried, the G/F hints and
        /// the shared use gap. Rebuilt only when it changes.
        /// </summary>
        void SyncItems(World w)
        {
            var inv = w.Inventory;
            var sel = ItemSystem.Selection(w);
            int others = 0;
            foreach (var s in inv.Slots) if (s.Count > 0 && (sel == null || s.Id != sel.Value) && ItemSystem.Available(w, s.Id)) others++;
            int count = sel != null ? inv.Count(sel.Value) : 0;
            bool held = inv.Holding && sel != null && inv.Held == sel.Value;
            int key = sel == null ? 0 : ((int)sel.Value + 1) * 100000 + count * 1000 + others * 10 + (held ? 1 : 0);
            if (key != itemKey)
            {
                itemKey = key;
                if (sel == null) itemText.text = $"<color=#{DimHex}>{Loc.T("hud.noItem")}</color>";
                else
                {
                    shownItem = sel.Value;
                    var data = w.Profile.Item(sel.Value);
                    string more = others > 0 ? "   " + Loc.F("hud.itemOthers", others) : "";
                    string hint = Loc.T(held ? "hud.itemHeld" : others > 0 ? "hud.itemKeys" : "hud.itemKeyF");
                    itemText.text = $"<b>{Loc.Item(sel.Value)}</b>  {count}/{data.Carry}{more}\n<size=15><color=#{DimHex}>{hint}</color></size>";
                }
            }
            float gap = Mathf.Clamp01((inv.NextUseTick - w.Clock.Tick) / (float)Mathf.Max(1, w.Ticks(w.Profile.Economy.ItemUseGap)));
            itemGap.gameObject.SetActive(gap > 0);
            if (gap > 0) itemGap.Set(1f - gap);
            itemButton.interactable = sel != null;
            itemFrame.color = held ? root.Theme.Accent : Color.white;
        }

        /// <summary>Companion health bar and the screen-edge danger arrow when he is hurt off-screen (§4).</summary>
        void SyncCompanion(World w, Camera cam)
        {
            var c = w.Companion;
            companion.gameObject.SetActive(c.Present);
            var arrowRt = (RectTransform)companionArrow.transform.parent;
            if (!c.Present) { arrowRt.gameObject.SetActive(false); return; }
            companion.Set(c.Health / Mathf.Max(1f, c.MaxHealth));
            companionText.text = Loc.F("hud.companion", Mathf.CeilToInt(c.Health));

            float since = (w.Clock.Tick - c.LastHitTick) * Hollow.Core.SimClock.Dt;
            bool recentlyHit = !c.Dead && since < w.Profile.Companion.DangerIndicatorTime;
            Vector3 vp = cam != null ? cam.WorldToViewportPoint(c.Position) : new Vector3(0.5f, 0.5f, 0);
            bool offScreen = vp.x < 0.02f || vp.x > 0.98f || vp.y < 0.02f || vp.y > 0.98f;
            arrowRt.gameObject.SetActive(recentlyHit && offScreen);
            if (!(recentlyHit && offScreen)) return;

            Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            float scale = 0.45f / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
            Vector2 edge = dir * scale;
            var hudRect = root.Hud.rect;
            arrowRt.anchoredPosition = new Vector2(edge.x * hudRect.width, edge.y * hudRect.height);
            arrowRt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        }
    }
}