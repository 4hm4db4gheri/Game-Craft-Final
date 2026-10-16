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
    /// Narrow live shop panel (§11): the world keeps running and WASD still moves. Phase pages are buttons
    /// because the mouse wheel swaps weapons. One click buys; no confirmation; disabled rows say why.
    /// </summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        UIRoot root;
        RectTransform panel, list;
        TextMeshProUGUI header;
        readonly List<Button> pageButtons = new();
        readonly List<(Button button, TextMeshProUGUI text, ShopOffer offer)> rows = new();
        Action<ShopOffer> onBuy;
        Action<ItemId> onFirstSlot;
        int page = 1;
        int shownPage = -1;
        int shownSignature;

        public bool Visible => panel.gameObject.activeSelf;

        public void Init(UIRoot uiRoot, Action<ShopOffer> buy, Action<ItemId> firstSlot)
        {
            root = uiRoot;
            onBuy = buy;
            onFirstSlot = firstSlot;
            var t = root.Theme;
            panel = UIFactory.Rect(root.Modal, "Shop", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 70), new Vector2(560, 800));
            UIFactory.Panel(panel, t, null, raycast: true);
            var head = UIFactory.Rect(panel, "Header", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(520, 70));
            header = UIFactory.Text(head, "Text", "", t, 26, TextAlignmentOptions.Top);
            for (int i = 1; i <= 3; i++)
            {
                int p = i;
                var b = UIFactory.Button(panel, $"Page{i}", $"Phase {i}", t, new Vector2(0.5f, 1), new Vector2(-170 + (i - 1) * 170, -105),
                    new Vector2(160, 44), () => { page = p; shownPage = -1; }, out _, 22);
                pageButtons.Add(b);
            }
            list = UIFactory.Rect(panel, "List", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -135), new Vector2(530, 650));
            panel.gameObject.SetActive(false);
        }

        public void Show(World w)
        {
            if (!Visible) { page = Mathf.Clamp(w.Phase, 1, 3); shownPage = -1; }
            panel.gameObject.SetActive(true);
            Refresh(w);
        }

        public void Hide() => panel.gameObject.SetActive(false);

        void Refresh(World w)
        {
            var offers = ShopSystem.Offers(w);
            int signature = Signature(w, offers);
            // Rebuild only when the set of rows changes; prices/locks update in place so a click is never lost.
            if (page != shownPage || signature != shownSignature) Rebuild(w, offers);
            else UpdateRows(w, offers);
            shownPage = page;
            shownSignature = signature;

            header.text = $"<b>SHOP</b>   <color=#{ColorUtility.ToHtmlStringRGB(root.Theme.Coin)}>{w.Progress.Wallet} ¢</color>\n" +
                          $"<size=18><color=#{ColorUtility.ToHtmlStringRGB(root.Theme.TextDim)}>The world keeps moving. E, dash or walking away closes.</color></size>";
            for (int i = 0; i < pageButtons.Count; i++)
                pageButtons[i].GetComponentInChildren<TextMeshProUGUI>().fontStyle = i + 1 == page ? FontStyles.Bold | FontStyles.Underline : FontStyles.Normal;
        }

        static int Signature(World w, List<ShopOffer> offers)
        {
            int h = 17;
            foreach (var o in offers)
                h = h * 31 + (int)ShopSystem.Key(o) + (o.Kind == OfferKind.BuyItem && w.Inventory.Count(o.Item) > 0 ? 1 : 0);
            return h;
        }

        void UpdateRows(World w, List<ShopOffer> offers)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var (button, text, old) = rows[i];
                long key = ShopSystem.Key(old);
                foreach (var o in offers)
                {
                    if (ShopSystem.Key(o) != key) continue;
                    button.interactable = o.Enabled;
                    text.text = Label(w, o);
                    rows[i] = (button, text, o);
                    break;
                }
            }
        }

        string Label(World w, ShopOffer o) => o.Kind switch
        {
            OfferKind.BuyWeapon or OfferKind.EquipWeapon => WeaponLabel(w, o),
            OfferKind.UpgradeWeapon => UpgradeLabel(w, o),
            OfferKind.BuyItem => ItemLabel(w, o),
            _ => HealLabel(w, o),
        };

        void Rebuild(World w, List<ShopOffer> offers)
        {
            foreach (var r in rows) Destroy(r.button.gameObject);
            rows.Clear();
            foreach (Transform child in list) Destroy(child.gameObject);
            var t = root.Theme;
            float y = 0;

            void Section(string title)
            {
                var rt = UIFactory.Rect(list, title, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(520, 26));
                UIFactory.Text(rt, "T", $"<b>{title}</b>", t, 19, TextAlignmentOptions.MidlineLeft, t.Accent);
                y += 28;
            }

            void Row(ShopOffer o, string label)
            {
                var b = UIFactory.Button(list, "Row", label, t, new Vector2(0.5f, 1), new Vector2(0, -y - 27), new Vector2(520, 52),
                    () => onBuy?.Invoke(o), out var text, 17);
                text.alignment = TextAlignmentOptions.MidlineLeft;
                b.interactable = o.Enabled;
                rows.Add((b, text, o));
                y += 56;
            }

            Section("Weapons");
            foreach (var o in offers)
                if (o.Page == page && (o.Kind == OfferKind.BuyWeapon || o.Kind == OfferKind.EquipWeapon))
                    Row(o, WeaponLabel(w, o));
            Section("Upgrades");
            foreach (var o in offers)
                if (o.Page == page && o.Kind == OfferKind.UpgradeWeapon) Row(o, UpgradeLabel(w, o));
            Section("Equipment and healing");
            foreach (var o in offers)
            {
                if (o.Kind == OfferKind.BuyItem && o.Page == page)
                {
                    Row(o, ItemLabel(w, o));
                    if (w.Inventory.Count(o.Item) > 0)
                    {
                        var item = o.Item;
                        UIFactory.Button(list, "FirstSlot", "1st", t, new Vector2(1, 1), new Vector2(-34, -y + 30), new Vector2(52, 26),
                            () => onFirstSlot?.Invoke(item), out _, 16);
                    }
                }
                if (o.Kind == OfferKind.HealPlayer || (o.Kind == OfferKind.HealCompanion && o.Lock != LockReason.NoCompanion))
                    Row(o, HealLabel(w, o));
            }
        }

        // ---------------------------------------------------------------- labels

        string Tail(ShopOffer o)
        {
            string price = o.Price > 0 ? $"<color=#{ColorUtility.ToHtmlStringRGB(root.Theme.Coin)}>{o.Price} ¢</color>" : "free";
            return o.Lock == LockReason.None ? price : $"{price}  <color=#{ColorUtility.ToHtmlStringRGB(root.Theme.Warning)}>{LockText(o.Lock)}</color>";
        }

        string WeaponLabel(World w, ShopOffer o)
        {
            var wd = w.Profile.Weapon(o.Weapon);
            var l = wd.Levels[0];
            string verb = o.Kind == OfferKind.EquipWeapon ? "Equip" : "Buy";
            return $"<b>{verb} {wd.DisplayName}</b>   {Tail(o)}\n<size=16>{Stats(wd, l)}</size>";
        }

        string UpgradeLabel(World w, ShopOffer o)
        {
            var wd = w.Profile.Weapon(o.Weapon);
            if (o.Lock == LockReason.MaxLevel) return $"<b>{wd.DisplayName}</b>  Lv{o.Level} (max)";
            var cur = wd.Levels[o.Level - 2];
            var next = wd.Levels[o.Level - 1];
            string tag = next.Approved ? "" : " <size=16>(placeholder values)</size>";
            return $"<b>{wd.DisplayName} Lv{o.Level - 1} → {o.Level}</b>{tag}   {Tail(o)}\n<size=16>{Diff(wd, cur, next)}</size>";
        }

        string ItemLabel(World w, ShopOffer o)
        {
            var item = w.Profile.Item(o.Item);
            return $"<b>{item.DisplayName}</b>  ({w.Inventory.Count(o.Item)}/{item.Carry}, stock {o.StockLeft})   {Tail(o)}\n<size=16>{ItemEffect(item)}</size>";
        }

        string HealLabel(World w, ShopOffer o)
        {
            var eco = w.Profile.Economy;
            bool player = o.Kind == OfferKind.HealPlayer;
            float amount = player ? eco.PlayerHealAmount : eco.CompanionHealAmount;
            return $"<b>Repair {(player ? "yourself" : "the Operator")}</b>  +{amount:0}   {Tail(o)}\n<size=16>{Mathf.Max(0, o.StockLeft)} left this encounter</size>";
        }

        static string Stats(WeaponData wd, WeaponLevel l) => wd.Kind == WeaponKind.Melee
            ? $"Dmg {F(l.Damage)} · {F(l.Rate)}/s · reach {F(l.Range)} · arc {F(l.ArcDeg)}°"
            : $"Dmg {F(l.Damage)}{(l.ProjectileCount > 1 ? $"×{l.ProjectileCount}" : "")} · {F(l.Rate)}/s · mag {l.Magazine} · reload {F(l.ReloadTime)}s · range {F(l.Range)}";

        static string Diff(WeaponData wd, WeaponLevel a, WeaponLevel b)
        {
            var sb = new StringBuilder();
            void C(string name, float x, float y) { if (!Mathf.Approximately(x, y)) sb.Append($"{name} {F(x)}→{F(y)}  "); }
            C("Dmg", a.Damage, b.Damage); C("Rate", a.Rate, b.Rate);
            if (wd.Kind == WeaponKind.Gun)
            {
                C("Pellets", a.ProjectileCount, b.ProjectileCount); C("Mag", a.Magazine, b.Magazine); C("Reload", a.ReloadTime, b.ReloadTime);
                C("Range", a.Range, b.Range); C("Spread", a.SpreadDeg, b.SpreadDeg); C("Targets", a.Targets, b.Targets);
            }
            else { C("Reach", a.Range, b.Range); C("Arc", a.ArcDeg, b.ArcDeg); C("Knockback", a.Knockback, b.Knockback); }
            return sb.Length > 0 ? sb.ToString() : "no stat change";
        }

        static string ItemEffect(ItemData i) => i.Id switch
        {
            ItemId.Grenade => $"{F(i.Damage)} dmg · radius {F(i.Radius)} · fuse {F(i.FuseTime)}s",
            ItemId.PushPulse => $"push {F(i.Knockback)} · radius {F(i.Radius)} · no damage",
            ItemId.Mine => $"{F(i.Damage)} dmg · radius {F(i.Radius)} · max {i.MaxActivePerPhase} active",
            ItemId.EmpPulse => $"stun machines {F(i.StunRegular)}s · radius {F(i.Radius)}",
            ItemId.PersonalShield => $"{F(i.Duration)}s invulnerable",
            _ => "",
        };

        static string F(float v) => v.ToString("0.##", Inv);

        public static string LockText(LockReason r) => r switch
        {
            LockReason.NotEnoughCoins => "not enough coins",
            LockReason.OutOfStock => "out of stock",
            LockReason.CarryFull => "carrying the maximum",
            LockReason.MaxLevel => "max level",
            LockReason.LevelNotDefined => "level not defined yet",
            LockReason.FullHealth => "health is full",
            LockReason.NoUsesLeft => "no uses left this encounter",
            LockReason.TutorialPurchaseFirst => "buy the SMG first",
            LockReason.NotUnlocked => "unlocks in a later phase",
            LockReason.NoCompanion => "no companion",
            LockReason.AlreadyEquipped => "equipped",
            _ => "",
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
