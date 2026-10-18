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
    /// <summary>Pictures for the shop rows; any of them may be missing (the row then shows a short name).</summary>
    public sealed class ShopArt
    {
        public Func<WeaponId, Sprite> Weapon = _ => null;
        public Func<ItemId, Sprite> Item = _ => null;
        /// <summary>The Operator's portrait for the companion heal, by phase (injured from phase two).</summary>
        public Func<int, Sprite> Companion = _ => null;
        public Sprite Coin, Repair;
    }

    /// <summary>
    /// Shop panel: a framed column on the right with the two carried weapons on top and one scrolling list of
    /// pictured rows below (weapons to buy, other owned weapons, upgrades, equipment and healing). It opens
    /// automatically between waves and with E at a station; one click buys and disabled rows say why.
    /// </summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const float Width = 640, Height = 800, Pad = 22;
        const float Inner = Width - 2 * Pad;
        const float ListTop = 262, ListBottom = 92, ScrollbarWidth = 8;
        const float RowWidth = Inner - ScrollbarWidth - 12, RowHeight = 96, IconWidth = 150;

        static readonly Color Back = new(0.035f, 0.055f, 0.08f, 0.98f);
        static readonly Color Edge = new(0.33f, 0.58f, 0.66f, 0.55f);
        static readonly Color RowBack = new(0.15f, 0.2f, 0.26f, 1f);
        static readonly Color CardBack = new(0.06f, 0.11f, 0.15f, 1f);
        static readonly Color Unaffordable = new(1f, 0.42f, 0.26f);

        UIRoot root;
        ShopArt art = new();
        RectTransform panel, content, cards;
        ScrollRect scroll;
        TextMeshProUGUI title, wallet, hint, jumpLabel;
        Image coinIcon;
        float equipmentY;

        /// <summary>A clickable offer; its texts are redrawn from the offer when prices or locks change.</summary>
        sealed class Row
        {
            public Button Button;
            public TextMeshProUGUI Title, Sub;
            public CanvasGroup Look;
            public ShopOffer Offer;
            public Func<World, ShopOffer, (string title, string sub)> Label;
            public string Shown;
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

        public void Init(UIRoot uiRoot, Action<ShopOffer> buy, ShopArt pictures = null)
        {
            root = uiRoot;
            onBuy = buy;
            if (pictures != null) art = pictures;
            var t = root.Theme;
            // Between the coin counter and the weapon panel, on the right where Persian reading starts.
            panel = UIFactory.Rect(root.Modal, "Shop", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 20), new Vector2(Width, Height));
            var bg = panel.gameObject.AddComponent<Image>();
            bg.sprite = UIFactory.WhiteSprite();
            bg.color = Back;
            Frame(panel, Edge, 2);
            Brackets(panel, t.Accent);

            // Header: the title on the right, the wallet on the left, then a rule and the closing hint.
            var head = UIFactory.Rect(panel, "Title", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Pad, -14), new Vector2(Inner * 0.6f, 62));
            title = UIFactory.Text(head, "T", "", t, 40, TextAlignmentOptions.MidlineRight);
            title.fontStyle = FontStyles.Bold;
            var coin = UIFactory.Rect(panel, "CoinIcon", new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(Pad + 4, -45), new Vector2(36, 36));
            coinIcon = coin.gameObject.AddComponent<Image>();
            coinIcon.preserveAspect = true;
            coinIcon.raycastTarget = false;
            var walletRt = UIFactory.Rect(panel, "Wallet", new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(Pad + 48, -45), new Vector2(Inner * 0.4f, 44));
            wallet = UIFactory.Text(walletRt, "T", "", t, 28, TextAlignmentOptions.Center, t.Coin);
            wallet.alignment = TextAlignmentOptions.MidlineLeft; // next to the coin, not mirrored
            wallet.fontStyle = FontStyles.Bold;
            Line(panel, new Vector2(0, -84), Inner, new Color(Edge.r, Edge.g, Edge.b, 0.45f));
            var hintRt = UIFactory.Rect(panel, "Hint", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(Inner, 40));
            hint = UIFactory.Text(hintRt, "T", "", t, 18, TextAlignmentOptions.MidlineRight, t.TextDim);
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 13;
            hint.fontSizeMax = 18;

            cards = UIFactory.Rect(panel, "Loadout", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -138), new Vector2(Inner, 110));

            // The list: a masked viewport, its content and a thin scrollbar on the left edge.
            var view = UIFactory.Rect(panel, "List", new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);
            view.anchorMin = new Vector2(0, 0);
            view.anchorMax = new Vector2(1, 1);
            view.offsetMin = new Vector2(Pad, ListBottom);
            view.offsetMax = new Vector2(-Pad, -ListTop);
            var viewport = UIFactory.Stretch(view, "Viewport");
            viewport.offsetMin = new Vector2(ScrollbarWidth + 12, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>(); // the wheel scrolls between rows too
            catcher.color = Color.clear;
            content = UIFactory.Rect(viewport, "Content", new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(RowWidth, 0));
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45;
            scroll.inertia = true;
            scroll.decelerationRate = 0.08f;
            scroll.verticalScrollbar = MakeScrollbar(view, t);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // One wide button at the bottom jumps between the weapons and the equipment & healing part of the list.
            var jump = UIFactory.Button(panel, "Jump", "", t, new Vector2(0.5f, 0), new Vector2(0, 16 + 30), new Vector2(Inner, 60), Jump, out jumpLabel, 24);
            jump.GetComponent<Image>().sprite = UIFactory.WhiteSprite();
            Tint(jump, new Color(0.09f, 0.16f, 0.2f), 0.75f, 1f);
            Frame((RectTransform)jump.transform, new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.8f), 2);
            jumpLabel.fontStyle = FontStyles.Bold;

            panel.gameObject.SetActive(false);
        }

        public void Show(World w)
        {
            if (!Visible) { shownSignature = int.MinValue; content.anchoredPosition = Vector2.zero; }
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

            var t = root.Theme;
            title.text = Loc.T("shop.title");
            wallet.text = Loc.F("common.coins", w.Progress.Wallet);
            coinIcon.sprite = art.Coin;
            coinIcon.enabled = art.Coin != null;
            bool timed = w.Waves.Kind == EncounterKind.SafeInterval;
            hint.text = timed ? Loc.F("shop.safeHint", Mathf.CeilToInt(WaveSystem.SafeIntervalRemaining(w))) : Loc.T("shop.hint");
            hint.color = timed ? t.Accent : t.TextDim; // the timed shop closes only with E: say so plainly
            jumpLabel.text = AtEquipment ? Loc.T("shop.weaponsTab") : Loc.T("shop.equipment");
        }

        /// <summary>Scrolled down to the equipment part (never when the whole list fits).</summary>
        bool AtEquipment => MaxScroll > 1f && content.anchoredPosition.y >= Mathf.Min(equipmentY, MaxScroll) - 2f;
        float MaxScroll => Mathf.Max(0f, content.sizeDelta.y - ((RectTransform)content.parent).rect.height);

        void Jump()
        {
            float target = AtEquipment ? 0f : Mathf.Min(equipmentY, MaxScroll);
            scroll.StopMovement();
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, target);
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
                    row.Offer = o;
                    Draw(w, row);
                    break;
                }
            }
        }

        void Draw(World w, Row row)
        {
            var o = row.Offer;
            row.Button.interactable = o.Enabled;
            // Short of coins only shows a red price; any other lock greys the row out.
            if (row.Look != null) row.Look.alpha = o.Enabled || o.Lock == LockReason.NotEnoughCoins ? 1f : 0.5f;
            var (head, sub) = row.Label(w, o);
            string shown = head + "\n" + sub;
            if (shown == row.Shown) return; // RTL shaping every frame is not free
            row.Shown = shown;
            if (row.Title != null) row.Title.text = head;
            if (row.Sub != null) row.Sub.text = sub;
        }

        (string, string) Label(World w, ShopOffer o) => o.Kind switch
        {
            OfferKind.BuyWeapon => WeaponLabel(w, o),
            OfferKind.UpgradeWeapon => UpgradeLabel(w, o),
            OfferKind.BuyItem => ItemLabel(w, o),
            _ => HealLabel(w, o),
        };

        Sprite Picture(World w, ShopOffer o) => o.Kind switch
        {
            OfferKind.BuyWeapon or OfferKind.UpgradeWeapon or OfferKind.EquipWeapon => art.Weapon(o.Weapon),
            OfferKind.BuyItem => art.Item(o.Item),
            OfferKind.HealPlayer => art.Repair,
            OfferKind.HealCompanion => art.Companion(w.Phase),
            _ => null,
        };

        string ShortName(World w, ShopOffer o) => o.Kind switch
        {
            OfferKind.BuyWeapon or OfferKind.UpgradeWeapon or OfferKind.EquipWeapon => Loc.Weapon(o.Weapon),
            OfferKind.BuyItem => Loc.Item(o.Item),
            OfferKind.HealPlayer => Loc.T("shop.healSelf"),
            _ => Loc.T("shop.healCompanion"),
        };

        void Rebuild(World w, List<ShopOffer> offers)
        {
            float kept = content.anchoredPosition.y;
            rows.Clear();
            foreach (Transform child in content) Destroy(child.gameObject);
            foreach (Transform child in cards) Destroy(child.gameObject);
            var t = root.Theme;
            float y = 0;
            equipmentY = 0;

            void Section(string text)
            {
                if (y > 0) y += 6;
                var rt = UIFactory.Rect(content, text, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(RowWidth, 30));
                UIFactory.Text(rt, "T", text, t, 17, TextAlignmentOptions.MidlineRight, new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.8f));
                Line(rt, new Vector2(0, -28), RowWidth, new Color(Edge.r, Edge.g, Edge.b, 0.3f));
                y += 34;
            }

            // A pictured row: the picture on the left, the name and price lines on the right.
            RectTransform Card(string name, float height, Sprite picture, string fallback, out Image back, out TextMeshProUGUI head, out TextMeshProUGUI sub, float textInset = 0)
            {
                var rt = UIFactory.Rect(content, name, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -y - height * 0.5f), new Vector2(RowWidth, height));
                back = rt.gameObject.AddComponent<Image>();
                back.sprite = UIFactory.WhiteSprite();
                back.color = RowBack;
                Frame(rt, new Color(1, 1, 1, 0.07f), 1);
                var look = UIFactory.Stretch(rt, "Look");
                look.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
                Picture(look, picture, fallback, height);
                var headRt = UIFactory.Rect(look, "Title", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -8), new Vector2(RowWidth - IconWidth - 30 - textInset, 34));
                head = UIFactory.Text(headRt, "T", "", t, 22, TextAlignmentOptions.MidlineRight);
                head.fontStyle = FontStyles.Bold;
                head.enableAutoSizing = true;
                head.fontSizeMin = 15;
                head.fontSizeMax = 22;
                head.textWrappingMode = TextWrappingModes.NoWrap;
                var subRt = UIFactory.Rect(look, "Sub", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -42), new Vector2(RowWidth - IconWidth - 30 - textInset, height - 48));
                sub = UIFactory.Text(subRt, "T", "", t, 18, TextAlignmentOptions.TopRight);
                sub.enableAutoSizing = true;
                sub.fontSizeMin = 11;
                sub.fontSizeMax = 18;
                y += height + 6;
                return rt;
            }

            void Offer(ShopOffer o)
            {
                float height = o.Kind == OfferKind.UpgradeWeapon ? RowHeight + 26 : RowHeight; // room for the change list
                var rt = Card("Row", height, Picture(w, o), ShortName(w, o), out var back, out var head, out var sub);
                var b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = back;
                Tint(b, RowBack, 0.62f, 1f);
                b.onClick.AddListener(() => { UIFactory.Click(); onBuy?.Invoke(o); });
                var row = new Row { Button = b, Title = head, Sub = sub, Look = rt.Find("Look").GetComponent<CanvasGroup>(), Offer = o, Label = Label };
                rows.Add(row);
                Draw(w, row);
            }

            var L = w.Player.Loadout;

            // What the player carries (D73): slot 1 on the right, slot 2 on the left, a swap button in the gap between them.
            void Loadout()
            {
                const float gap = 14;
                float cardWidth = (Inner - gap) * 0.5f, height = cards.sizeDelta.y;
                for (int slot = 1; slot <= 2; slot++)
                {
                    bool filled = slot == 1 || L.HasPrimary;
                    var id = slot == 1 ? L.Slot1 : L.SlotPrimary;
                    bool active = L.ActiveSlot == slot && filled;
                    float x = slot == 1 ? Inner - cardWidth * 0.5f : cardWidth * 0.5f;
                    var card = UIFactory.Rect(cards, $"Slot{slot}", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(cardWidth, height));
                    var img = card.gameObject.AddComponent<Image>();
                    img.sprite = UIFactory.WhiteSprite();
                    img.color = active ? new Color(0.07f, 0.17f, 0.21f) : CardBack;
                    img.raycastTarget = false;
                    Frame(card, active ? t.Accent : new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.4f), 2);
                    var name = UIFactory.Rect(card, "Name", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -8), new Vector2(cardWidth - 24, 30));
                    var label = UIFactory.Text(name, "T", filled ? $"{Loc.Weapon(id)} {Loc.F("shop.level", L.Get(id)?.Level ?? 1)}" : Loc.T("shop.emptySlot"),
                        t, 19, TextAlignmentOptions.MidlineRight, filled ? t.Text : t.TextDim);
                    label.fontStyle = FontStyles.Bold;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 13;
                    label.fontSizeMax = 19;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    var key = UIFactory.Rect(card, "Key", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -38), new Vector2(90, 24));
                    UIFactory.Text(key, "T", Loc.F("shop.slotKey", slot), t, 15, TextAlignmentOptions.MidlineRight, active ? t.Accent : t.TextDim);
                    if (filled && art.Weapon(id) is { } sprite)
                    {
                        var pic = UIFactory.Rect(card, "Picture", new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 10), new Vector2(cardWidth - 120, height - 22));
                        Fit(pic, sprite);
                    }
                }
                foreach (var o in offers)
                    if (o.Kind == OfferKind.EquipWeapon && L.HasPrimary && o.Weapon == L.SlotPrimary && o.Slot == 1)
                    {
                        var b = UIFactory.Button(cards, "Swap", Loc.T("shop.swap"), t, new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(70, 30), () => onBuy?.Invoke(o), out var text, 15);
                        b.GetComponent<Image>().sprite = UIFactory.WhiteSprite();
                        Tint(b, new Color(0.1f, 0.2f, 0.25f), 0.8f, 1.15f);
                        Frame((RectTransform)b.transform, t.Accent, 1);
                        rows.Add(new Row { Button = b, Title = text, Offer = o, Label = (_, _) => (Loc.T("shop.swap"), "") });
                    }
            }

            // Every other owned weapon (from any phase page): the picture, its level and a button per slot.
            void Stored(WeaponId weapon)
            {
                const float slotWidth = 96, slotHeight = 34;
                var owned = L.Get(weapon);
                var wd = w.Profile.Weapon(weapon);
                var level = wd.Levels[Mathf.Clamp(owned.Level - 1, 0, wd.Levels.Count - 1)];
                var rt = Card("Stored", RowHeight, art.Weapon(weapon), Loc.Weapon(weapon), out _, out var head, out var sub, slotWidth + 12);
                head.text = $"{Loc.Weapon(weapon)} {Loc.F("shop.level", owned.Level)}";
                sub.text = $"<color=#{Hex(t.TextDim)}>{ShortStats(level)}</color>";
                foreach (var o in offers)
                {
                    if (o.Kind != OfferKind.EquipWeapon || o.Weapon != weapon) continue;
                    float by = o.Slot == 1 ? slotHeight * 0.5f + 4 : -slotHeight * 0.5f - 4;
                    var b = UIFactory.Button(rt, "Slot", Loc.F("shop.toSlot", o.Slot), t, new Vector2(0, 0.5f), new Vector2(IconWidth + 20 + slotWidth * 0.5f, by),
                        new Vector2(slotWidth, slotHeight), () => onBuy?.Invoke(o), out var text, 15);
                    b.GetComponent<Image>().sprite = UIFactory.WhiteSprite();
                    Tint(b, new Color(0.1f, 0.2f, 0.25f), 0.8f, 1.15f);
                    Frame((RectTransform)b.transform, new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.7f), 1);
                    var row = new Row { Button = b, Title = text, Offer = o, Label = (_, of) => (Loc.F("shop.toSlot", of.Slot), "") };
                    rows.Add(row);
                    b.interactable = o.Enabled;
                }
            }

            Loadout();
            bool header = false;
            foreach (var o in offers)
            {
                if (o.Page > w.Phase || o.Kind != OfferKind.BuyWeapon) continue;
                if (!header) { Section(Loc.T("shop.weapons")); header = true; }
                Offer(o);
            }
            header = false;
            foreach (var wd in w.Profile.Weapons)
            {
                if (!L.Owns(wd.Id) || L.SlotOf(wd.Id) != 0) continue;
                if (!header) { Section(Loc.T("shop.stored")); header = true; }
                Stored(wd.Id);
            }
            Section(Loc.T("shop.upgrades"));
            foreach (var o in offers)
                if (o.Page <= w.Phase && o.Kind == OfferKind.UpgradeWeapon) Offer(o);
            equipmentY = y;
            Section(Loc.T("shop.equipment"));
            foreach (var o in offers)
            {
                if (o.Kind == OfferKind.BuyItem && o.Page <= w.Phase) Offer(o);
                if (o.Kind == OfferKind.HealPlayer || (o.Kind == OfferKind.HealCompanion && o.Lock != LockReason.NoCompanion))
                    Offer(o);
            }
            content.sizeDelta = new Vector2(RowWidth, y);
            content.anchoredPosition = new Vector2(0, Mathf.Clamp(kept, 0, MaxScroll));
        }

        // ---------------------------------------------------------------- building blocks

        void Picture(RectTransform row, Sprite sprite, string fallback, float height)
        {
            var box = UIFactory.Rect(row, "Picture", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(IconWidth, height - 16));
            var bg = box.gameObject.AddComponent<Image>();
            bg.sprite = UIFactory.WhiteSprite();
            bg.color = new Color(0, 0, 0, 0.22f);
            bg.raycastTarget = false;
            if (sprite == null)
            {
                UIFactory.Text(box, "T", fallback, root.Theme, 15, TextAlignmentOptions.Center, root.Theme.TextDim);
                return;
            }
            var pic = UIFactory.Rect(box, "Fit", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, box.sizeDelta - new Vector2(22, 14));
            Fit(pic, sprite);
        }

        /// <summary>
        /// Draws the sprite so its visible pixels (the tight mesh, not the padded canvas) fill <paramref name="box"/>,
        /// centred: icons and held art leave wide transparent margins.
        /// </summary>
        static void Fit(RectTransform box, Sprite sprite)
        {
            var rect = sprite.rect;
            Vector2 min = Vector2.one, max = Vector2.zero;
            foreach (var v in sprite.vertices)
            {
                var uv = new Vector2((v.x * sprite.pixelsPerUnit + sprite.pivot.x) / rect.width, (v.y * sprite.pixelsPerUnit + sprite.pivot.y) / rect.height);
                min = Vector2.Min(min, uv);
                max = Vector2.Max(max, uv);
            }
            if (max.x <= min.x || max.y <= min.y) { min = Vector2.zero; max = Vector2.one; }
            Vector2 seen = (max - min) * rect.size;
            float k = Mathf.Min(box.sizeDelta.x / seen.x, box.sizeDelta.y / seen.y);
            Vector2 size = rect.size * k;
            var rt = UIFactory.Rect(box, "Image", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), (Vector2.one * 0.5f - (min + max) * 0.5f) * size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.useSpriteMesh = true;
            img.raycastTarget = false;
        }

        /// <summary>A rectangle outline of the given thickness just inside the edges.</summary>
        static void Frame(RectTransform rt, Color color, float thickness)
        {
            void Side(string name, Vector2 min, Vector2 max, Vector2 size)
            {
                var s = UIFactory.Rect(rt, name, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                s.anchorMin = min;
                s.anchorMax = max;
                s.sizeDelta = size;
                s.anchoredPosition = new Vector2(min.x == max.x ? (min.x == 0 ? thickness : -thickness) * 0.5f : 0, min.y == max.y ? (min.y == 0 ? thickness : -thickness) * 0.5f : 0);
                var img = s.gameObject.AddComponent<Image>();
                img.sprite = UIFactory.WhiteSprite();
                img.color = color;
                img.raycastTarget = false;
            }
            Side("Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, thickness));
            Side("Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, thickness));
            Side("Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(thickness, 0));
            Side("Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(thickness, 0));
        }

        /// <summary>Bright L-shaped corner marks just outside the frame.</summary>
        static void Brackets(RectTransform rt, Color color)
        {
            const float arm = 34, thick = 4, out_ = 7;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(i % 2, i / 2);
                var dir = new Vector2(corner.x == 0 ? 1 : -1, corner.y == 0 ? 1 : -1); // into the panel
                Vector2 tip = -dir * out_;
                void Bar(Vector2 size, Vector2 offset)
                {
                    var b = UIFactory.Rect(rt, "Bracket", corner, new Vector2(0.5f, 0.5f), tip + offset, size);
                    var img = b.gameObject.AddComponent<Image>();
                    img.sprite = UIFactory.WhiteSprite();
                    img.color = color;
                    img.raycastTarget = false;
                }
                Bar(new Vector2(arm, thick), new Vector2(dir.x * (arm * 0.5f - thick * 0.5f), 0));
                Bar(new Vector2(thick, arm), new Vector2(0, dir.y * (arm * 0.5f - thick * 0.5f)));
            }
        }

        static void Line(RectTransform parent, Vector2 topCentre, float width, Color color)
        {
            var rt = UIFactory.Rect(parent, "Rule", new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), topCentre, new Vector2(width, 1.5f));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIFactory.WhiteSprite();
            img.color = color;
            img.raycastTarget = false;
        }

        /// <summary>The graphic keeps its colour; the button only darkens it at rest and shows it in full under the pointer.</summary>
        static void Tint(Button b, Color baseColor, float rest, float hover)
        {
            b.targetGraphic.color = baseColor;
            var c = b.colors;
            c.normalColor = new Color(rest, rest, rest, 1);
            c.highlightedColor = c.selectedColor = new Color(hover, hover, hover, 1);
            c.pressedColor = new Color(hover * 1.2f, hover * 1.2f, hover * 1.2f, 1);
            c.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            c.colorMultiplier = 1.4f;
            c.fadeDuration = 0.06f;
            b.colors = c;
        }

        static Scrollbar MakeScrollbar(RectTransform view, UITheme t)
        {
            var track = UIFactory.Rect(view, "Scrollbar", new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, Vector2.zero);
            track.anchorMin = new Vector2(0, 0);
            track.anchorMax = new Vector2(0, 1);
            track.pivot = new Vector2(0, 0.5f);
            track.sizeDelta = new Vector2(ScrollbarWidth, 0);
            var bg = track.gameObject.AddComponent<Image>();
            bg.sprite = UIFactory.WhiteSprite();
            bg.color = new Color(1, 1, 1, 0.06f);
            var area = UIFactory.Stretch(track, "Area");
            var handle = UIFactory.Stretch(area, "Handle");
            var img = handle.gameObject.AddComponent<Image>();
            img.sprite = UIFactory.WhiteSprite();
            img.color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.7f);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = img;
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            return bar;
        }

        // ---------------------------------------------------------------- labels

        /// <summary>The price (orange-red when the wallet is short) and, for any other lock, why the row is closed.</summary>
        string Price(ShopOffer o)
        {
            var t = root.Theme;
            if (o.Price <= 0) return $"<color=#{Hex(t.Accent)}>{Loc.T("shop.free")}</color>";
            string price = $"<b><color=#{Hex(o.Lock == LockReason.NotEnoughCoins ? Unaffordable : t.Coin)}>{Loc.F("common.coins", o.Price)}</color></b>";
            return o.Lock is LockReason.None or LockReason.NotEnoughCoins ? price : $"{price}  <size=82%><color=#{Hex(t.Warning)}>{LockText(o.Lock)}</color></size>";
        }

        string Dim(string s) => $"<size=76%><color=#{Hex(root.Theme.TextDim)}>{s}</color></size>";

        (string, string) WeaponLabel(World w, ShopOffer o)
        {
            var wd = w.Profile.Weapon(o.Weapon);
            return (Loc.Weapon(wd.Id), $"{Price(o)}\n{Dim(Stats(wd, wd.Levels[0]))}");
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        static string ShortStats(WeaponLevel l) =>
            Loc.F("stats.short", l.ProjectileCount > 1 ? $"{l.ProjectileCount}×{F(l.Damage)}" : F(l.Damage), F(l.Rate));

        (string, string) UpgradeLabel(World w, ShopOffer o)
        {
            var wd = w.Profile.Weapon(o.Weapon);
            if (o.Lock == LockReason.MaxLevel) return (Loc.Weapon(wd.Id), Dim(Loc.F("shop.maxLevel", o.Level)));
            var cur = wd.Levels[o.Level - 2];
            var next = wd.Levels[o.Level - 1];
            string tag = next.Approved ? "" : $" {Dim(Loc.T("shop.placeholder"))}";
            return (Loc.F("shop.upgrade", Loc.Weapon(wd.Id), o.Level - 1, o.Level) + tag, $"{Price(o)}\n{Dim(Diff(wd, cur, next))}");
        }

        (string, string) ItemLabel(World w, ShopOffer o)
        {
            var item = w.Profile.Item(o.Item);
            return (Loc.Item(item.Id), $"{Price(o)}   {Dim(Loc.F("shop.carry", w.Inventory.Count(o.Item), item.Carry, o.StockLeft))}\n{Dim(ItemEffect(item))}");
        }

        (string, string) HealLabel(World w, ShopOffer o)
        {
            var eco = w.Profile.Economy;
            bool player = o.Kind == OfferKind.HealPlayer;
            float amount = player ? eco.PlayerHealAmount : eco.CompanionHealAmount;
            return ($"{Loc.T(player ? "shop.healSelf" : "shop.healCompanion")}  +{amount:0}", $"{Price(o)}\n{Dim(Loc.F("shop.usesLeft", Mathf.Max(0, o.StockLeft)))}");
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
