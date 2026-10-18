# پرامپت آرت رابط کاربری برای GPT Image

رابط بازی الان با کد ساخته می‌شود (`UIFactory`، `UITheme`): مستطیل‌های تخت سرمه‌ای، دکمه‌های ساده و نوارهای رنگی بدون قاب. این سند پرامپت‌هایی است که به GPT Image داده می‌شود تا جای این ظاهر پیش‌فرض آرت ساخته شود.

**روش کار:**
1. اول «پرامپت ۰: برگه‌ی سبک» را بده. یک تصویر می‌دهد که همه‌ی اجزا را کنار هم با یک سبک نشان می‌دهد. اگر خوب بود، همان را نگه دار.
2. برای هر جزء، «پیش‌درآمد مشترک» + پرامپت همان جزء را بده و **تصویر برگه‌ی سبک را ضمیمه کن** تا همه یک‌شکل شوند. هر جزء را جدا بساز (یک تصویر = یک فایل).
3. خروجی را با نام گفته‌شده در `Assets/Art/Incoming/` بگذار.
4. متن‌ها را به انگلیسی بده؛ GPT Image با انگلیسی دقیق‌تر است.

**قانون‌ها برای همه:** هیچ متن یا حرفی داخل تصویر نباشد (متن‌ها فارسی و راست‌به‌چپ‌اند و کد آن‌ها را می‌نویسد)؛ تزیین قرینه‌ی چپ و راست باشد (چون رابط راست‌به‌چپ است)؛ پس‌زمینه شفاف.

---

## پیش‌درآمد مشترک (اول هر پرامپت جزء بچسبان)

```
Game UI asset for "Hollow", a dark top-down 3/4-view 2D shooter (style close to Kill The Crows, hand-painted, NOT pixel art).
The player is a small floating AI drone with a cyan-white glowing core, fighting through a dead forest, an abandoned factory and a cold human military base.

UI style: "salvaged drone HUD". Dark gunmetal and charcoal painted metal (#14181D to #232A31), slightly worn edges with tiny scratches, thin bevel highlight on the top edge, a few small rivets or screws only at the corners. Accent = thin cyan glow lines (#7FEBFF) like the drone's core, used sparingly (corner brackets, one edge line). Painted, soft, slightly gritty, matching a hand-painted game — not glossy, not flat vector, not sci-fi neon overload, no hexagon patterns.

Rules:
- Transparent background (PNG with alpha). Nothing outside the asset.
- NO text, NO letters, NO numbers, NO icons unless asked.
- Left-right symmetrical decoration (the UI is right-to-left Persian).
- Centered, straight-on view (no perspective, no tilt), crisp edges, readable at small size.
- Use the attached image as the style reference.
```

---

## پرامپت ۰ — برگه‌ی سبک (یک تصویر، 1536×1024)

```
[shared preamble]

Make ONE UI kit style sheet on a plain dark gray (#2B2B2B) background, all elements laid out in a neat grid with space between them:
- a large window panel with cyan corner brackets
- four buttons in a row: normal, hover (cyan rim glow), pressed (darker, pushed in), disabled (desaturated, dim)
- an empty horizontal health bar frame, and the same frame filled red; a thin XP bar frame filled light blue; a wide ornate boss bar frame filled orange-red
- a square inventory slot frame, empty
- a tall upgrade card
- a wide dialogue box with a square portrait frame on its right side
- a speech bubble with a small tail pointing down
- a slider (track, fill and round knob) and a checkbox (off and on)
- a small rounded "pill" plate for a coin counter
- a guide arrow pointing up
No text anywhere.
```

---

## اجزا

اندازه‌ها اندازه‌ی خروجی GPT Image است. «9-slice» یعنی کد گوشه‌ها را ثابت نگه می‌دارد و وسط و لبه‌ها را می‌کشد؛ پس وسط باید ساده و یکنواخت باشد و تزیین فقط در گوشه‌ها.

### ۱. پنل اصلی — `UI_GEN_Panel_ui.png` (1024×1024)
همه‌جا استفاده می‌شود: ستون منوی اصلی، پنل سلاح HUD، فروشگاه، تنظیمات، جایگاه‌ها، pause.
```
[shared preamble]
A square 9-slice window panel. Border exactly 12.5% of the width on every side; corners carry all the decoration (small cyan L-shaped corner brackets, one tiny rivet each); straight edges are plain and uniform so they can stretch; the center is a plain, flat, very dark semi-opaque gunmetal fill (about 90% opaque) with no texture variation, no gradient, no vignette.
```

### ۲. دکمه — چهار حالت (1536×1024، هر کدام جدا)
`UI_GEN_Button_ui_normal.png`، `_hover.png`، `_pressed.png`، `_disabled.png`
```
[shared preamble]
A wide horizontal 9-slice button, 3:1 proportion, centered on the canvas. Rounded-rectangle metal plate, border thickness about 12% of its height, flat center for a text label, decoration only at the four corners.
State: NORMAL — dark gunmetal plate, faint top highlight, thin dim cyan line along the bottom edge.
```
برای بقیه فقط خط آخر را عوض کن و تصویر NORMAL را هم ضمیمه کن («exactly the same shape and size as the attached normal button»):
- `HOVER — same plate slightly lighter, bright cyan glowing rim all around, soft outer glow.`
- `PRESSED — same plate darker and pushed in, inner shadow on the top edge, cyan rim dimmer.`
- `DISABLED — same plate desaturated gray, low contrast, no cyan at all.`

### ۳. نوارها
- **قاب نوار جان** — `UI_GEN_BarFrame_ui.png` (1536×1024)
```
[shared preamble]
An EMPTY horizontal bar frame, 8:1 proportion, centered. Metal frame with a slightly thicker left and right cap (symmetrical), the inside is a fully transparent hole where the fill will go. Frame thickness about 15% of its height. 9-slice friendly: caps at the ends, plain stretchable middle.
```
- **پر شدن نوار** — `UI_GEN_BarFill_ui.png` (1024×1024)
```
[shared preamble]
A horizontal bar FILL texture in pure light gray / white (it will be tinted red, green or blue in code). Soft top highlight, slightly darker bottom, a few faint diagonal energy stripes. Fills the whole canvas edge to edge, no frame, no rounded ends.
```
- **قاب نوار باس** — `UI_GEN_BossBar_ui.png` (1536×1024)
```
[shared preamble]
An EMPTY, more ornate wide horizontal boss health bar frame, 10:1 proportion. Heavier dark metal, sharp angular end caps with a small warning-orange (#FF4A1C) light on each cap, symmetrical. Inside is a transparent hole for the fill.
```
- **نشان گلوله‌ی خشاب** — `UI_GEN_RoundPip_ui.png` (1024×1024، دو نسخه کنار هم یا دو فایل `_full` و `_empty`)
```
[shared preamble]
A single small vertical ammo pip, like a bullet cell in a magazine indicator. FULL version: brass-yellow glowing cell in a dark metal socket. EMPTY version: same socket, dark and hollow.
```

### ۴. خانه‌ی آیتم و اسلات سلاح — `UI_GEN_Slot_ui.png` (1024×1024) و `UI_GEN_Slot_ui_selected.png`
خانه‌ی آیتم پایین HUD و کارت اسلات ۱ و ۲ فروشگاه.
```
[shared preamble]
A square 9-slice inventory slot frame. Recessed dark socket, beveled metal rim, border 12.5% of the width, empty transparent-dark center where an icon will sit.
```
نسخه‌ی انتخاب‌شده: `SELECTED — same frame with a bright cyan glowing inner rim and cyan corner brackets.`

### ۵. کارت مهارت (level up) — `UI_GEN_Card_ui.png` (1024×1536)
سه کارت کنار هم وقتی بازیکن level up می‌کند.
```
[shared preamble]
A tall upgrade card, 2:3 proportion, 9-slice friendly. Dark metal card with a cyan-glowing top band (empty, for the title), a thin divider line in the upper third, plain dark center for text. Small cyan corner brackets. Feels like a chip or module the drone installs.
```
نسخه‌ی hover: `UI_GEN_Card_ui_hover.png` با `the whole rim glows cyan and the card looks slightly lifted`.

### ۶. کادر دیالوگ — `UI_GEN_DialogueBox_ui.png` (1536×1024) و `UI_GEN_PortraitFrame_ui.png` (1024×1024)
```
[shared preamble]
A wide dialogue box, 7:1 proportion, 9-slice friendly, semi-opaque very dark center for subtitles, thin cyan line along the top edge, decoration only at the corners.
```
```
[shared preamble]
A square portrait frame, heavier metal rim with four corner screws, transparent center hole, a small cyan light at the bottom center.
```

### ۷. کارت راهنما — `UI_GEN_HintCard_ui.png` (1536×1024)
```
[shared preamble]
A wide notification card, 4.5:1 proportion, 9-slice friendly, dark plate with a bright cyan vertical accent strip on the RIGHT edge (right-to-left UI), plain center.
```

### ۸. حباب گفت‌وگوی اپراتور — `UI_GEN_Bubble_ui.png` (1536×1024)
```
[shared preamble]
A horizontal speech bubble, 6:1 proportion, rounded dark plate with a thin light rim and a small tail at the bottom center pointing down. Plain center for one line of text.
```

### ۹. نوار بالا و کیف پول — `UI_GEN_TopPlate_ui.png` و `UI_GEN_Pill_ui.png` (1536×1024)
```
[shared preamble]
A wide top-of-screen status plate, 11:1 proportion, shaped like a shallow trapezoid hanging from the top edge (wider at the top), dark metal, thin cyan line along the bottom, symmetrical.
```
```
[shared preamble]
A small rounded pill-shaped plate, 6:1 proportion, dark metal with a warm gold (#FFC73D) thin rim, for a coin counter. Plain center.
```

### ۱۰. تنظیمات: اسلایدر و چک‌باکس — (1024×1024 هر کدام)
`UI_GEN_SliderTrack_ui.png`، `UI_GEN_SliderKnob_ui.png`، `UI_GEN_Toggle_ui_off.png`، `UI_GEN_Toggle_ui_on.png`
```
[shared preamble]
A thin horizontal slider TRACK, 16:1 proportion, recessed dark groove with rounded ends, centered.
```
```
[shared preamble]
A round slider KNOB, metal with a glowing cyan center dot, centered, filling about 70% of the canvas.
```
```
[shared preamble]
A square checkbox. OFF: empty recessed dark metal square. (ON: same square with a glowing cyan check mark inside.)
```

### ۱۱. اسکرول‌بار فروشگاه — `UI_GEN_Scrollbar_ui.png` (1024×1536)
```
[shared preamble]
A vertical scrollbar: thin recessed dark track and a separate rounded metal handle with a short cyan line in the middle, shown side by side.
```

### ۱۲. فلش راهنما — `UI_GEN_Arrow_ui.png` (1024×1024)
فلش لبه‌ی صفحه (سمت اپراتور، سکه، فروشگاه).
```
[shared preamble]
A chunky guide arrow pointing straight UP, cyan-white glowing core with a soft dark outline so it reads on any ground, centered, filling about 70% of the canvas.
```

### ۱۳. (اختیاری) لوگوی HOLLOW — `KEY_GEN_Logo_ui.png` (1536×1024)
این تنها تصویری است که متن دارد.
```
[shared preamble, but text IS allowed here]
The game logo "HOLLOW" in wide-spaced bold capital letters, worn painted metal, a cyan glowing crack running through the letter O like the drone's core, transparent background, centered.
```

---

## بعد از ساختن

- الان `UITheme` فقط سه sprite می‌گیرد (`Panel`، `Button`، `Bar`)؛ بقیه‌ی اجزا (کارت، دیالوگ، حباب، خانه‌ی آیتم، اسلایدر، قاب نوار، باس‌بار و غیره) هنوز در کد جا ندارند و بعد از رسیدن تصویرها باید به `UITheme`، `UIFactory` و `ApplyIncomingArt` وصل شوند.
- چهار حالت دکمه با رنگ کد جایگزین می‌شوند (الان hover فقط رنگ است)؛ برای استفاده از sprite جدا باید `Button.transition` به `SpriteSwap` برود.
- اگر GPT Image حاشیه‌ی 9-slice را یکنواخت نداد، لبه‌ها را در Sprite Editor یونیتی دستی تنظیم کن.
