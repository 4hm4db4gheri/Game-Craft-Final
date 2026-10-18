using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Hollow.Data;
using Hollow.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollow.UI
{
    /// <summary>
    /// Shop panel: shows everything unlocked through the current phase.
    /// It opens automatically between waves; one click buys and disabled rows say why.
    /// </summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const float RowWidth = 660;

        UIRoot root;
        RectTransform panel, list;
        TextMeshProUGUI header;
        /// <summary>A clickable offer; its text is redrawn from the offer when prices or locks change.</summary>
        sealed class Row
        {
            public Button Button;
            public TextMeshProUGUI Text;
            public ShopOffer Offer;
            public Func<World, ShopOffer, string> Label;
        }

        readonly List<Row> rows = new();
        Action<ShopOffer> onBuy;
        int shownSignature;

        public bool Visible => panel.gameObject.activeSelf;

        public RectTransform TutorialBuyButton(World w)
        {
            foreach (var row in rows)
                if (row.Offer.Kind == OfferKind.BuyWeapon && row.Offer.Weapon == w.Profile.Economy.TutorialWeapon)
                    return (RectTransform)row.Button.transform;
            return null;
        }

        public void Init(UIRoot uiRoot, Action<ShopOffer> buy)
        {
            root = uiRoot;
            onBuy = buy;
            var t = root.Theme;
            panel = UIFactory.Rect(root.Modal, "Shop", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 60), new Vector2(RowWidth + 40, 860)); // between the coin counter and the weapon panel; wide enough for one-line rows
            UIFactory.Panel(panel, t, null, raycast: true);
            var head = UIFactory.Rect(panel, "Header", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(RowWidth, 70));
            header = UIFactory.Text(head, "Text", "", t, 26, TextAlignmentOptions.Top);
            list = UIFactory.Rect(panel, "List", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(RowWidth + 10, 770));
            panel.gameObject.SetActive(false);
        }

        public void Show(World w)
        {
            if (!Visible) shownSignature = int.MinValue;
            panel.gameObject.SetActive(true);
            Refresh(w);
        }

        public void Hide() => panel.gameObject.SetActive(false);

        void Refresh(World w)
        {
            var offers = ShopSystem.Offers(w);
            int signature = Signature(w, offers);
            // Rebuild only when the set of rows changes; prices/locks update in place so a click is never lost.
            if (signature != shownSignature) Rebuild(w, offers);
            else UpdateRows(w, offers);
            shownSignature = signature;

            string hint = w.Waves.Kind == EncounterKind.SafeInterval
                ? Loc.F("shop.safeHint", Mathf.CeilToInt(WaveSystem.SafeIntervalRemaining(w)))
                : Loc.T("shop.hint");
            header.text = $"<b>{Loc.T("shop.title")}</b>   <color=#{ColorUtility.ToHtmlStringRGB(root.Theme.Coin)}>{Loc.F("common.coins", w.Progress.Wallet)}</color>\n" +
                          $"<size=18><color=#{ColorUtility.ToHtmlStringRGB(root.Theme.TextDim)}>{hint}</color></size>";
        }

        static int Signature(World w, List<ShopOffer> offers)
        {
            var L = w.Player.Loadout;
            // Equipping changes locks, not keys: the carried weapons and the active slot are part of the layout too.
            int h = 17 + w.Phase * 97 + (int)L.Slot1 * 7919 + (L.HasPrimary ? 1 + (int)L.SlotPrimary * 131 : 0) + L.ActiveSlot * 3;
            foreach (var o in offers)
                h = h * 31 + (int)ShopSystem.Key(o) + (o.Kind == OfferKind.BuyItem && w.Inventory.Count(o.Item) > 0 ? 1 : 0);
            return h;
        }

        void UpdateRows(World w, List<ShopOffer> offers)
        {
            foreach (var row in rows)
            {
                long key = ShopSystem.Key(row.Offer);
                foreach (var o in offers)
                {
                    if (ShopSystem.Key(o) != key) continue;
                    row.Button.interactable = o.Enabled;
                    row.Text.text = row.Label(w, o);
                    row.Offer = o;
                    break;
                }
            }
        }

        string Label(World w, ShopOffer o) => o.Kind switch
        {
            OfferKind.BuyWeapon => WeaponLabel(w, o),
            OfferKind.UpgradeWeapon => UpgradeLabel(w, o),
            OfferKind.BuyItem => ItemLabel(w, o),
            _ => HealLabel(w, o),
        };

        void Rebuild(World w, List<ShopOffer> offers)
        {
            foreach (var r in rows) Destroy(r.Button.gameObject);
            rows.Clear();
            foreach (Transform child in list) Destroy(child.gameObject);
            var t = root.Theme;
            float y = 0;

            void Section(string title)
            {
                var rt = UIFactory.Rect(list, title, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(RowWidth, 26));
                UIFactory.Text(rt, "T", $"<b>{title}</b>", t, 19, TextAlignmentOptions.MidlineRight, t.Accent);
                y += 28;
            }

            Button Add(Transform parent, ShopOffer o, Vector2 anchor, Vector2 pos, Vector2 size, float font, Func<World, ShopOffer, string> label)
            {
                var b = UIFactory.Button(parent, "Row", label(w, o), t, anchor, pos, size, () => onBuy?.Invoke(o), out var text, font);
                b.interactable = o.Enabled;
                rows.Add(new Row { Button = b, Text = text, Offer = o, Label = label });
                return b;
            }

            void Row(ShopOffer o, float height = 52)
            {
                Add(list, o, new Vector2(0.5f, 1), new Vector2(0, -y - height * 0.5f - 1), new Vector2(RowWidth, height), 17, Label)
                    .GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineRight;
                y += height + 4;
            }

            var L = w.Player.Loadout;

            // What the player carries (D73): slot 1 on the right, slot 2 on the left, a swap button between them.
            void Loadout()
            {
                const float height = 76, swapWidth = 96, gap = 8;
                float cardWidth = (RowWidth - swapWidth - 2 * gap) * 0.5f;
                var rt = UIFactory.Rect(list, "Loadout", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y - 1), new Vector2(RowWidth, height));
                for (int slot = 1; slot <= 2; slot++)
                {
                    bool filled = slot == 1 || L.HasPrimary;
                    var id = slot == 1 ? L.Slot1 : L.SlotPrimary;
                    float x = slot == 1 ? RowWidth - cardWidth * 0.5f : cardWidth * 0.5f;
                    var card = UIFactory.Rect(rt, $"Slot{slot}", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(cardWidth, height));
                    bool active = L.ActiveSlot == slot && filled;
                    UIFactory.Panel(card, t, active ? new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.22f) : new Color(1, 1, 1, 0.06f));
                    string head = $"<size=16><color=#{Hex(active ? t.Accent : t.TextDim)}>{Loc.F("shop.slotCard", slot)}</color></size>";
                    string body = filled ? $"<b>{Loc.Weapon(id)}</b>  <size=15>{Loc.F("shop.level", L.Get(id)?.Level ?? 1)}</size>"
                        : $"<color=#{Hex(t.TextDim)}>{Loc.T("shop.emptySlot")}</color>";
                    UIFactory.Text(card, "T", $"{head}\n{body}", t, 22, TextAlignmentOptions.Center);
                }
                foreach (var o in offers)
                    if (o.Kind == OfferKind.EquipWeapon && L.HasPrimary && o.Weapon == L.SlotPrimary && o.Slot == 1)
                        Add(rt, o, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(swapWidth, 46), 16, (_, _) => Loc.T("shop.swap"));
                y += height + 6;
            }

            // Every other owned weapon (from any phase page): one short line and a button per slot.
            void Stored(WeaponId weapon)
            {
                const float height = 50, slotWidth = 86, gap = 6;
                var rt = UIFactory.Rect(list, "Stored", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y - 1), new Vector2(RowWidth, height));
                UIFactory.Panel(rt, t, new Color(1, 1, 1, 0.04f));
                var labelRect = UIFactory.Rect(rt, "Label", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(RowWidth - 2 * (slotWidth + gap) - 16, height));
                var owned = L.Get(weapon);
                var wd = w.Profile.Weapon(weapon);
                var level = wd.Levels[Mathf.Clamp(owned.Level - 1, 0, wd.Levels.Count - 1)];
                UIFactory.Text(labelRect, "T", $"<b>{Loc.Weapon(weapon)}</b>  <size=15>{Loc.F("shop.level", owned.Level)}</size>\n<size=14><color=#{Hex(t.TextDim)}>{ShortStats(level)}</color></size>",
                    t, 17, TextAlignmentOptions.MidlineRight);
                foreach (var o in offers)
                {
                    if (o.Kind != OfferKind.EquipWeapon || o.Weapon != weapon) continue;
                    float x = gap + (2 - o.Slot) * (slotWidth + gap) + slotWidth * 0.5f; // slot 1 nearer the name
                    Add(rt, o, new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(slotWidth, height - 10), 16, (_, of) => Loc.F("shop.toSlot", of.Slot));
                }
                y += height + 4;
            }

            Section(Loc.T("shop.loadout"));
            Loadout();
            bool storedHeader = false;
            foreach (var wd in w.Profile.Weapons)
            {
                if (!L.Owns(wd.Id) || L.SlotOf(wd.Id) != 0) continue;
                if (!storedHeader) { Section(Loc.T("shop.stored")); storedHeader = true; }
                Stored(wd.Id);
            }
            bool buyHeader = false;
            foreach (var o in offers)
            {
                if (o.Page > w.Phase || o.Kind != OfferKind.BuyWeapon) continue;
                if (!buyHeader) { Section(Loc.T("shop.weapons")); buyHeader = true; }
                Row(o);
            }
            Section(Loc.T("shop.upgrades"));
            foreach (var o in offers)
                if (o.Page <= w.Phase && o.Kind == OfferKind.UpgradeWeapon) Row(o, 62); // name and price, then the change list
            Section(Loc.T("shop.equipment"));
            foreach (var o in offers)
            {
                if (o.Kind == OfferKind.BuyItem && o.Page <= w.Phase) Row(o);
                if (o.Kind == OfferKind.HealPlayer || (o.Kind == OfferKind.HealCompanion && o.Lock != LockReason.NoCompanion))
                    Row(o);
            }
            // Many owned weapons make a long page; shrink it to the panel rather than spill over the HUD.
            float fit = Mathf.Min(1f, list.sizeDelta.y / Mathf.Max(1f, y));
            list.localScale = new Vector3(fit, fit, 1f);
        }

        // ---------------------------------------------------------------- labels

        string Tail(ShopOffer o)
        {
            string price = o.Price > 0 ? $"<color=#{ColorUtility.ToHtmlStringRGB(root.Theme.Coin)}>{Loc.F("common.coins", o.Price)}</color>" : Loc.T("shop.free");
            return o.Lock == LockReason.None ? price : $"{price}  <color=#{ColorUtility.ToHtmlStringRGB(root.Theme.Warning)}>{LockText(o.Lock)}</color>";
        }

        string WeaponLabel(World w, ShopOffer o)
        {
            var wd = w.Profile.Weapon(o.Weapon);
            var l = wd.Levels[0];
            return $"<b>{Loc.F("shop.buy", Loc.Weapon(wd.Id))}</b>   {Tail(o)}\n<size=16>{Stats(wd, l)}</size>";
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        static string ShortStats(WeaponLevel l) =>
            Loc.F("stats.short", l.ProjectileCount > 1 ? $"{l.ProjectileCount}×{F(l.Damage)}" : F(l.Damage), F(l.Rate));

        string UpgradeLabel(World w, ShopOffer o)
        {
            var wd = w.Profile.Weapon(o.Weapon);
            if (o.Lock == LockReason.MaxLevel) return $"<b>{Loc.Weapon(wd.Id)}</b>  {Loc.F("shop.maxLevel", o.Level)}";
            var cur = wd.Levels[o.Level - 2];
            var next = wd.Levels[o.Level - 1];
            string tag = next.Approved ? "" : $" <size=16>{Loc.T("shop.placeholder")}</size>";
            return $"<b>{Loc.F("shop.upgrade", Loc.Weapon(wd.Id), o.Level - 1, o.Level)}</b>{tag}   {Tail(o)}\n<size=15>{Diff(wd, cur, next)}</size>";
        }

        string ItemLabel(World w, ShopOffer o)
        {
            var item = w.Profile.Item(o.Item);
            return $"<b>{Loc.Item(item.Id)}</b>  · {Loc.F("shop.carry", w.Inventory.Count(o.Item), item.Carry, o.StockLeft)}   {Tail(o)}\n<size=16>{ItemEffect(item)}</size>";
        }

        string HealLabel(World w, ShopOffer o)
        {
            var eco = w.Profile.Economy;
            bool player = o.Kind == OfferKind.HealPlayer;
            float amount = player ? eco.PlayerHealAmount : eco.CompanionHealAmount;
            return $"<b>{Loc.T(player ? "shop.healSelf" : "shop.healCompanion")}</b>  +{amount:0}   {Tail(o)}\n<size=16>{Loc.F("shop.usesLeft", Mathf.Max(0, o.StockLeft))}</size>";
        }

        static string Stats(WeaponData wd, WeaponLevel l) => wd.Kind == WeaponKind.Melee
            ? Loc.F("stats.melee", F(l.Damage), F(l.Rate), F(l.Range), F(l.ArcDeg))
            : Loc.F("stats.gun", l.ProjectileCount > 1 ? $"{l.ProjectileCount}×{F(l.Damage)}" : F(l.Damage), F(l.Rate), l.Magazine, F(l.ReloadTime), F(l.Range));

        static string Diff(WeaponData wd, WeaponLevel a, WeaponLevel b)
        {
            var sb = new StringBuilder();
            void C(string key, float x, float y) { if (!Mathf.Approximately(x, y)) sb.Append(Loc.F("stats.change", Loc.T("stat." + key), F(x), F(y))).Append("   "); }
            C("damage", a.Damage, b.Damage); C("rate", a.Rate, b.Rate);
            if (wd.Kind == WeaponKind.Gun)
            {
                C("pellets", a.ProjectileCount, b.ProjectileCount); C("magazine", a.Magazine, b.Magazine); C("reload", a.ReloadTime, b.ReloadTime);
                C("range", a.Range, b.Range); C("spread", a.SpreadDeg, b.SpreadDeg); C("targets", a.Targets, b.Targets);
            }
            else { C("reach", a.Range, b.Range); C("arc", a.ArcDeg, b.ArcDeg); C("knockback", a.Knockback, b.Knockback); }
            return sb.Length > 0 ? sb.ToString() : Loc.T("stats.noChange");
        }

        static string ItemEffect(ItemData i) => i.Id switch
        {
            ItemId.Grenade => Loc.F("itemFx.grenade", F(i.Damage), F(i.Radius), F(i.FuseTime)),
            ItemId.PushPulse => Loc.F("itemFx.push", F(i.Knockback), F(i.Radius)),
            ItemId.Mine => Loc.F("itemFx.mine", F(i.Damage), F(i.Radius), i.MaxActivePerPhase),
            ItemId.EmpPulse => Loc.F("itemFx.emp", F(i.StunRegular), F(i.Radius)),
            ItemId.PersonalShield => Loc.F("itemFx.shield", F(i.Duration)),
            _ => "",
        };

        static string F(float v) => v.ToString("0.##", Inv);

        public static string LockText(LockReason r) => r switch
        {
            LockReason.None => "",
            _ => Loc.T("lock." + r),
        };

        void Update()
        {
            // Lock states depend on the wallet and health, which change while the panel is open.
            if (Visible && currentWorld != null) Refresh(currentWorld);
        }

        World currentWorld;
        public void Bind(World w) => currentWorld = w;
    }
}
