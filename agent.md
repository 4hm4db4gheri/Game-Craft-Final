# agent.md — حافظه‌ی پروژه برای ایجنت

> **قانون اصلی (اجباری):**
> 1. در شروع هر کار جدید، **اول همین فایل را بخوان**. هدف این است که لازم نباشد کل پروژه دوباره خوانده شود.
> 2. **پس از اتمام هر تسک**، کارهای انجام‌شده را در بخش «تاریخچه‌ی کارها» اضافه کن و بخش‌های «وضعیت فعلی» و «ساختار پروژه» را هم اگر تغییر کرده‌اند به‌روز کن. این فایل باید **همیشه به‌روز** باشد.
> 3. اگر چیزی در این فایل با واقعیت پروژه نمی‌خواند، پروژه را مرجع بدان و این فایل را اصلاح کن.

---

## خلاصه‌ی پروژه

- **نام:** Game Craft Final — بازی **Hollow** (جم دو روزه). شوتر دوبعدی با دید 3/4 از بالا مثل Kill The Crows (نه پیکسل‌آرت)، سه فاز (حیوانات، ربات‌ها، انسان‌ها)، ۵ موج + ۱ باس در هر فاز.
- **مرجع طراحی:** GDD جدید **v0.19** است: `docs/GDD_v0.19.md` (تبدیل از `Assets/GDD/Hollow_Game_Design_v0.19_EN.docx`). کد هنوز بر اساس v0.12 است (`docs/GDD.md`). فهرست تفاوت‌ها و کارهای لازم در `docs/GDD_CHANGES_v0.19.md`. پرامپت اصلی کاربر شامل تسک‌های T00 تا T22 است؛ وضعیت در `docs/TASKS.md`.
- **Unity:** `6000.6.0f1`، URP با Renderer2D (sort axis = Y برای مرتب‌سازی بر اساس پا)، Input System جدید (actionها در کد، `UnityInputSource`)، Cinemachine 6.6 و AI Navigation (کاربر نصب کرده؛ Navigation استفاده نمی‌شود)، Newtonsoft JSON.
- **git:** شاخه‌ی `main`، یک commit برای هر تسک، بدون push مگر کاربر بخواهد.

## معماری (خلاصه)

- گیم‌پلی یک **شبیه‌سازی C# خالص** است (`Hollow.Sim`) با تیک ثابت ۶۰ هرتز؛ MonoBehaviourها فقط رندر می‌کنند و ورودی می‌دهند (D40–D47 در `docs/DECISIONS.md`).
- **همه‌ی اعداد گیم‌پلی** در `Assets/Resources/Balance/Baseline.json` هستند (کلاس‌ها در `Assets/Scripts/Data/BalanceProfile.cs`). هیچ عدد گیم‌پلی در کد نباشد. `Assets/Editor/BaselineSeed.cs` seed است و Baseline.json را بازنویسی می‌کند (با تأیید). از v0.19 عددها اول در seed عوض می‌شوند و بعد با `BaselineSeed.Write()` نوشته می‌شوند (قبلش seed و JSON مقایسه شدند و یکسان بودند).
- **پنل بالانس:** Editor: منوی `Hollow > Balance Panel` (Ctrl+Shift+B). داخل بازی: F9 (در Editor و development build). `BalanceService` = draft/live/apply/undo/reset group.

## ساختار پروژه

| مسیر | توضیح |
|---|---|
| `Assets/Scenes/` | `Boot` → `MainMenu` (منوی موقت IMGUI) → `Game` |
| `Assets/Scripts/Core/` | `Hollow.Core`: state machine، `SimClock`/تایمرها، `SimDriver`، `SimRandom`، `InputRouting/` (InputRouter، PlayerCommand) |
| `Assets/Scripts/App/` | `Hollow.App`: `GameApp` (ریشه‌ی سرویس‌ها)، ورودی Unity، `GameSceneRoot` |
| `Assets/Scripts/Data/` | `Hollow.Data`: پروفایل بالانس، JSON/CSV، calculator، validator، `BalanceService` |
| `Assets/Resources/Localization/fa.json` | همه‌ی متن‌های بازیکن به فارسی (کلید ← متن)، خوانده با `Hollow.UI.Loc` (`Loc.T`/`Loc.F`/`Loc.Weapon`…). متن جدید = کلید جدید در همین فایل؛ تست `LocalizationTests` کلیدهای کد را چک می‌کند |
| `Assets/Plugins/RTLTMPro/` + `Assets/Fonts/Vazirmatn/` | RTLTMPro (MIT، با یک اصلاح در `LigatureFixer` برای جداکننده‌ی بین ارقام) و فونت وزیرمتن؛ فونت TMP با `Hollow > UI > Build Persian Font` در `Resources/Fonts` ساخته می‌شود. پرانتز و کروشه کنار عدد یا متن لاتین به‌هم می‌ریزد؛ از آن‌ها استفاده نکن |
| `Assets/Scripts/Dev/` | `Hollow.Dev`: پنل بالانس IMGUI (`BalancePanelGUI`، `BalanceOverlay` روی F9) |
| `Assets/Editor/` | `ProjectSetup` (ساخت صحنه‌ها)، `BuildTool`، `BaselineSeed`، `BalanceWindow`، `PrefabBuilder`/`EnemyPrefabBuilder` (prefabهای PH_)، `ArenaTools`، `Art/` (importer آرت AI) |
| `Assets/Art/Incoming/` | آرت AI با نام طبق `docs/ART_MASTER_BRIEF.md` اینجا ریخته می‌شود؛ سپس منوی `Hollow > Art > Apply Incoming Art` (SpriteSetها در `Assets/Art/SpriteSets`) |
| `Assets/Tests/EditMode/` | تست‌های NUnit (۴۱۰ تست، همه پاس) |
| `Assets/Scripts/Presentation/Arena/` | `ArenaAuthoring` (لایه‌های هر فاز)، `ArenaProp` (مانع نام‌دار؛ footprint آن در bake به سلول مسدود تبدیل می‌شود)، `ArenaAtmosphere` (نور 2D هر فاز، درخشش بازیکن، مه، تاریک شدن پایان) |
| `Assets/Scripts/Presentation/Feel/` | `FeedbackDirector`: لایه‌های حس بازی از روی SimEventها (muzzle، جرقه، جسد/لکه با سقف، عدد آسیب با محدودیت نرخ، shake با Cinemachine Impulse) |
| `Assets/Scripts/Dev/FrameProbe.cs` | اندازه‌گیری زمان فریم و GC برای stress (T05/T21) |
| `Assets/Scripts/Presentation/Audio/` | `AudioDirector` (busها در کد، cooldown و priority، موسیقی هر فاز، جینگل پیروزی و بعد سکوت، stub بی‌صدا برای cue بدون فایل) و `AudioCatalog` (`Assets/Resources/Audio`، ساخت با `Hollow > Audio > Rebuild Catalog`؛ فایل‌ها در `Assets/Audio` با نام `<Cue>_<n>`) |
| `Assets/Scripts/Sim/Telemetry/` | `RunTelemetry`: لاگ هر بازی (Observed جدا از Calculated)؛ فایل در `persistentDataPath/telemetry` توسط `Game/TelemetryFile.cs` |
| `docs/` | GDD، QUESTIONS، DECISIONS، TASKS، USER_STORIES، `ART_MASTER_BRIEF.md` و `ART_ASSET_LIST.md` (پرامپت ساخت آرت با AI)، `WeaponLevels_Template.csv` |

## قراردادها (Conventions)

- هر عدد گیم‌پلی → فیلد در `BalanceProfile` با برچسب `[Gdd]` یا `[Placeholder]`.
- شناسه‌ها enum هستند (`EnemyId`، `WeaponId`، …) نه رشته.
- کار با ادیتور باز از طریق `unity command ... --project-path` (اسکریپت‌های کمکی در scratchpad: compile/tests). Play mode در Editor حتی بدون فوکوس اجرا می‌شود (`runInBackground` فقط در Editor).
- اگر فایل جدیدی در یک پوشه‌ی تازه ساختی و Unity آن را کامپایل نکرد، پوشه را با `AssetDatabase.MoveAsset` جابه‌جا کن (مشکل کش؛ اتفاق افتاد).
- `Library/`، `Temp/`، `Logs/`، `UserSettings/`، `Builds/` تولیدی‌اند.

## وضعیت فعلی

- انجام‌شده: T00–T04، T06–T15، T16a (importer آرت)، T16 (سه حالت آرنا)، T05 (جلوه‌ها)، T19 (صدا)، T18 (telemetry و ابزار)، T20 (آموزش و خوانایی)، T21 (بات و کارایی)، T22 (build نهایی)، (منوها، pause، ذخیره/بارگذاری، checkpoint، فروشگاه، آیتم‌ها، ۲۴ نوع دشمن، باس‌ها، داستان، پایان)، T17. پنل بالانس F9 و پنل توسعه F10 (فقط dev). ۴۰۲ تست پاس. عددهای بالانس از GDD v0.19 هستند.
- کل بازی از ابتدا تا «You won.» در Play mode اجرا شد.
- باقی‌مانده: هیچ تسکی از T00–T22 نمانده. کارهای بعدی با طراح/کاربر: اعداد سطح ۲ و ۳ سلاح‌ها، playtest انسانی، آرت AI، گوش دادن به صداها (فهرست در `docs/RELEASE_NOTES.md`).
## کارهای باز / ایده‌ها

- **اعمال GDD v0.19**: عددها (سلاح، دشمن، فرزند، باس)، retry با جان پر، falloff و نفوذ گلوله، و جلوه‌های جسد و tracer انجام شد. هنوز مانده: فارسی‌سازی و RTL، انتخاب ۴ AI، هدیه‌ی rifle، یک اسلات آیتم، دو ایستگاه خدمات، فیلم‌نامه‌ی جدید، تنظیمات voice و زیرنویس، صدای هر دشمن. فهرست در `docs/GDD_CHANGES_v0.19.md`.

- آرت با AI توسط کاربر طبق `docs/ART_MASTER_BRIEF.md` تولید می‌شود؛ importer خودکار (T16a) آماده است و زمین، prop، decor و کیوسک فروشگاه را هم پوشش می‌دهد (T16).
---

## تاریخچه‌ی کارها

> هر تسک را با تاریخ، خلاصه‌ی کار و فایل‌های تغییرکرده به انتهای این لیست اضافه کن.

### 2026-10-16 — ساخت agent.md
- بررسی اولیه‌ی ساختار پروژه (تمپلیت 2D URP یونیتی 6).
- ساخت همین فایل `agent.md` به‌عنوان خلاصه و حافظه‌ی پروژه.
- فایل‌های تغییرکرده: `agent.md` (جدید).

### 2026-10-16 — ساخت CLAUDE.md
- ساخت `CLAUDE.md` که با `@agent.md` این فایل را در هر جلسه خودکار بارگذاری می‌کند و قانون به‌روزرسانی را تکرار می‌کند.
- فایل‌های تغییرکرده: `CLAUDE.md` (جدید)، `agent.md`.

### 2026-10-16 — Commit تغییرات
- `485e18c`: حذف HubForceResolve، به‌روزرسانی ProjectSettings، اضافه شدن تنظیمات AI Assistant، انتقال `2d-template.png` به LFS.
- `c439e31`: اضافه شدن `agent.md` و `CLAUDE.md`.

### 2026-10-16 — بررسی اولیه‌ی Hollow و سؤال‌ها (گام ۱ و ۲ پرامپت)
- تبدیل GDD به `docs/GDD.md` با اسکریپت پایتون (pandoc/ffmpeg نصب نیست). بررسی دو ویدیوی مرجع با contact sheet در مرورگر.
- تأیید با اسکریپت: همه‌ی جمع‌های GDD (۱۱۴۵ دشمن، XP و بودجه‌ی سکه) دقیقاً بازتولید می‌شوند؛ سهم خود باس = ۱۰۰/۱۸۰/۲۶۰.
- بررسی محیط: ادیتور یونیتی باز است و `unity` CLI نصب است؛ Cinemachine و AI Navigation نصب نیستند؛ رندرر فعلی 2D است.
- فایل‌های تغییرکرده: `docs/GDD.md`، `docs/QUESTIONS.md`، `docs/USER_STORIES.md` (جدید)، `agent.md`.

### 2026-10-16 — تصمیم 2D و اصلاح سؤال‌ها
- کاربر اعلام کرد بازی 2D (ایزومتریک/شبیه ویدیوی Kill The Crows) است. پاسخ: نیازی به پروژه‌ی 3D نیست.
- ساخت `docs/DECISIONS.md` (D1، D2). اصلاح `docs/QUESTIONS.md`: Q2/Q9 پاسخ داده شد؛ Q4، Q5 (flow field به‌جای NavMesh)، Q10–Q14 برای 2D بازنویسی شد؛ سؤال‌های جدید Q72 (sorting و hitbox)، Q73 (مانع کوتاه/بلند)، Q74 (نورهای 2D و مه).
- فایل‌های تغییرکرده: `docs/DECISIONS.md` (جدید)، `docs/QUESTIONS.md`، `agent.md`.

### 2026-10-16 — جواب سؤال‌ها، پرامپت آرت، T00، T01، T01b
- کاربر به همه‌ی سؤال‌ها جواب داد (اکثراً پیش‌فرض پیشنهادی)؛ ثبت در `docs/DECISIONS.md`. پرامپت master brief و لیست asset برای تولید آرت با AI نوشته شد.
- T00: state machine، دو ساعت (sim/active)، InputRouter، صحنه‌ها، BuildTool، حذف Welcome و SampleScene. build ویندوز موفق.
- T01: BalanceProfile + Baseline.json از GDD، calculator و validator، CSV سطح سلاح‌ها. T01b: پنل بالانس Editor + F9.
- commitها: `35e6893` (T00)، `c9ee16c` (T01).

### 2026-10-16 — T02 تا T11 و شبیه‌سازی T10
- شبیه‌سازی C# خالص (`Assets/Scripts/Sim`)، نمایش pooled (`Presentation`)، UI با کد (`Assets/Scripts/UI`)، صحنه‌ی Game با `Hollow > Setup > Rebuild Game Scene` ساخته می‌شود.
- commitها: `e5de9be` تا آخرین commit موج‌ها.

### 2026-10-16 — موج‌ها در صحنه، T17، T15، T07، T12، T13، T14
- ذخیره‌ی دقیق (WorldSerializer، SlotStore با نوشتن اتمی)، checkpoint اول هر موج/باس، منوی اصلی/slot/pause/loading/settings/controls.
- رفتار همه‌ی دشمن‌ها، producerها با pool ثابت، hazard، آیتم‌ها، باس‌ها، دیالوگ از `Resources/Narrative/Dialogue.json`، انتقال فاز و پایان.
- ابزارهای تست Play mode در scratchpad: `click.ps1`، `state.ps1`، `run.ps1`.

### 2026-10-16 — T16a: pipeline واردکردن آرت AI
- `Assets/Editor/Art/ArtImport.cs`: از روی نام فایل، PPU (۱۲۸ پیکسل در واحد)، pivot (پا/دسته‌ی سلاح/وسط)، برش strip به فریم، برش زمین ۸×۸ به ۶۴ تایل، حذف پس‌زمینه‌ی مژنتا (و آلفای FX روی مشکی) روی خود PNG.
- `Assets/Editor/Art/ApplyIncomingArt.cs`: ساخت SpriteSet برای هر موجودیت و جایگزینی شکل placeholder در prefabهای PH_ و catalog سلاح‌ها؛ `SpriteSetAnimator` حالت‌ها را از viewها می‌گیرد.
- تست با strip مصنوعی گرگ در Play mode؛ فایل تست حذف شد. commit: `bb72f9e`.

### 2026-10-16 — T16: سه حالت آرنا
- هر فاز: زمین جدا (Tilemap)، decor جدا، و propهای نام‌دار (prefab `PH_Prop_P<n>_<Name>`) با footprint از `ART_ASSET_LIST.md`؛ bake برخورد را از propها می‌سازد. آرت AI زمین/prop/decor/کیوسک با `Apply Incoming Art` جایگزین می‌شود.
- نور: گروه جدید `Atmosphere` در BalanceProfile (نور ۱ / ۰.۸۲ / ۰.۶، درخشش بازیکن، مه). هشدار حمله، گلوله، سکه، رتیکل و هسته‌ی بازیکن unlit هستند. پایان: همه‌ی نورها (حتی چراغ‌ها) خاموش می‌شوند.
- باگ پیدا شده با تست: `Resolve` از وسط مانع ۳ خانه‌ای بیرون نمی‌آورد؛ `ArenaGrid.NearestFree` برای جای‌گذاری‌ها (تغییر فاز، spawn، سکه، فروشگاه) اضافه شد.
- تست‌های `ArenaLayoutTests` (اتصال، نبود شکاف یک‌خانه‌ای، مسیر از هر spawn zone، تغییر فاز). اسکرین‌شات‌ها در `docs/screenshots/T16_*`. تصمیم‌ها D49–D52.

### 2026-10-16 — T05: حس بازی (game feel)
- `FeedbackDirector` (Presentation/Feel): muzzle flash، جرقه‌ی ذره‌ای pooled، flash و squash دشمن، جسد و لکه‌ی خون/روغن/سوختگی با سقف، عدد آسیب (جمع گلوله‌های شاتگان در یک عدد، محدودیت نرخ، روشن/خاموش از تنظیمات)، shake کوتاه با Cinemachine Impulse و ضریب تنظیمات. همه‌ی اعداد در `Feedback` داده؛ effect budget سقف‌ها را ضرب می‌کند. `GameSettings.Changed` تنظیمات را فوراً اعمال می‌کند.
- sorting layer جدید: Default → Effects → Telegraphs؛ هشدار حمله، گلوله‌ی دشمن، hazard و رتیکل همیشه رو هستند (sortAtRoot). ذرات باید با متریال sprite باشند (رندرر 2D شیدر ذره‌ی 3D را نمی‌کشد).
- stress با ۴۰ دشمن در Play mode ادیتور: میانگین 7.3ms (136fps)، p95 9.9ms؛ GC حدود 27KB در فریم (برای T21). صدای لایه‌ی ۶ با T19.

### 2026-10-16 — T19 بخش ۱: سیستم صدا
- `AudioDirector` + `AudioCatalog` + `AudioBridge` (حجم از تنظیمات، کلیک UI). ۴۶ cue همه فعلاً stub بی‌صدا.
- منابع CC0 پیدا شد (۶ پک Kenney + ۳ لوپ OpenGameArt، حدود ۱۵ مگابایت دانلود)؛ **دانلود منتظر تأیید کاربر است** (قانون: دانلود هر فایل با اجازه‌ی صریح).

### 2026-10-16 — T19 بخش ۲: فایل‌های صدا
- با تأیید کاربر دانلود شد: ۶ پک CC0 کنی + ۳ لوپ CC0 از OpenGameArt. ۷۱ فایل افکت در `Assets/Audio/Sfx` و ۳ موسیقی در `Assets/Audio/Music` (۵.۵ مگابایت، LFS)، با نام `<Cue>_<n>`؛ همه در `docs/CREDITS.md`.
- هر ۴۶ cue در Play mode پخش شد. انتخاب‌ها بر اساس نام فایل بوده؛ یک انسان باید گوش کند و در صورت نیاز عوض کند. تصمیم D56.
- نکته: اگر ادیتور بسته باشد، `unity status` چیزی نشان نمی‌دهد؛ کاربر ادیتور را از Hub باز می‌کند.

### 2026-10-16 — T18: telemetry، ابزار توسعه، اعداد hard-code
- `RunTelemetry` + `TelemetryFile`: فایل JSON برای هر بازی (مدت موج/باس، FPS، آسیب، سکه، خرید، مهارت، سلاح و سطح، آیتم، هزینه‌ی درمان، علت مرگ). رویدادهای جدید `Purchased` و `SkillChosen`؛ `World.Seed`.
- DevPanel: شروع مستقیم باس، وضعیت شروع؛ در build نسخه‌ی release با `ReleaseBuildStripper` حذف می‌شود.
- ۱۰ عدد گیم‌پلی به داده منتقل شد؛ تست `NoUnlistedNumberLiteralsInTheSimulation` جلوی عدد جدید در `Sim` را می‌گیرد (گزارش: `docs/CONSTANTS_SCAN.md`). health snapshot حالا روی بچه‌ها و add باس هم اعمال می‌شود.
- نکته: کاربر گاهی ادیتور را می‌بندد؛ با `unity open <path>` دوباره باز می‌شود (چند دقیقه).

### 2026-10-17 — T20: آموزش و چک‌لیست خوانایی
- آموزش ابتدای بازی با ورودی واقعی (کلید و ماوس) از اول تا خرید SMG طی شد. اضافه شد: فلش راهنما تا فروشگاه، راهنمای دکمه‌ی خرید، «Press 2 to use the Light SMG».
- تست‌های جدید `ReadabilityTests` (فرصت نفس کشیدن اول فاز، تلگراف‌ها روی لایه‌ی بالا، شکل متمایز دشمن‌ها). چک‌لیست در `docs/T20_CHECKLIST.md` (۴ معیار §15 نیاز به تست انسانی).
- اندازه‌های 1280×720، 1600×900، 1920×1080 بررسی شد؛ برچسب XP و فونت فروشگاه اصلاح شد.
- ابزار تست: `scratchpad/holdkeys.ps1` (نگه داشتن کلید با زمان دقیق از طریق Input System) و `tutbot.ps1`.

### 2026-10-17 — T21: بات کل بازی، stress و کارایی
- `FullRunTests`: بات headless هر ۱۵ موج و ۳ باس را با seed 2026 بازی می‌کند؛ پاداش هر encounter دقیق، ۶ بار save/load بدون drift. باگ: `CoinSystem.Collect` دوبار پرداخت می‌کرد (حالا idempotent).
- `StressRun` (فقط dev): `Hollow.exe -hollowStress [-uncapped]` → موج ۵ فاز ۳؛ گزارش در `persistentDataPath/perf`. نتیجه در build: ۶۰fps قفل، بدون قفل 605fps، GC حدود ۳KB در فریم، ۱۸۵MB.
- HUD فقط وقتی مقدار عوض شود متن می‌سازد. اجرای مستقیم صحنه‌ی Game (تست‌ها) حالا در `dev_saves` ذخیره می‌کند نه slotهای بازیکن.
- گزارش: `docs/PERFORMANCE_REPORT.md`. build توسعه: `Builds/WindowsDev`.

### 2026-10-17 — T22: build نهایی و release notes
- build تمیز release ویندوز: موفق، ۰ خطا، ۱۳۲.۸ مگابایت، `Builds/Windows/Hollow.exe`؛ DevPanel در build حذف شد. هشدارها همه از پکیج AI Inference (Sentis) است.
- ۳۹۸ تست پاس. build در این سیستم تا منوی اصلی اجرا شد؛ روی سیستم دوم/پروفایل تمیز تست نشده.
- `docs/RELEASE_NOTES.md`: چه کار می‌کند، چه placeholder است، چه تست نشده، مشکلات شناخته‌شده، تصمیم‌های فرضی، کارهای طراح، و بررسی همه‌ی user storyها.

### 2026-10-17 — رفع باگ: محل شروع گلوله
- مشکل: هسته‌ی گلوله در prefab با فاصله‌ی ۰.۳۵ «بالا»ی محلی قرار داشت و با جهت گلوله می‌چرخید؛ رو به چپ این فاصله به پایین برمی‌گشت و گلوله زیر بازیکن دیده می‌شد.
- رفع (فقط نمایش، شبیه‌سازی دست نخورد): `ProjectileView` ارتفاع را در جهت بالای صفحه اضافه می‌کند؛ گلوله‌ی بازیکن و Operator از سر سلاح شروع می‌شود و در ۰.۱ ثانیه روی خط واقعی‌اش می‌نشیند؛ تفنگ بازیکن در همان جهت شبیه‌سازی نشانه می‌رود (`PlayerView`)؛ `CompanionView.Muzzle`.

### 2026-10-17 — بررسی GDD v0.19
- کاربر GDD را به v0.19 به‌روز کرد (فایل docx قبلی حذف شد). تبدیل به `docs/GDD_v0.19.md` و مقایسه با v0.12 و با کد و Baseline.json.
- گزارش: `docs/GDD_CHANGES_v0.19.md`. هیچ کدی تغییر نکرد. `docs/GDD.md` (v0.12) فعلاً برای مرجع نگه داشته شد.

### 2026-10-17 — اعمال عددهای GDD v0.19 و تغییرهای کوچک
- seed و `Baseline.json`: هر ۳۰ سطح سلاح طبق v0.19 (همه approved)، سرعت گلوله 60 و 45، جدول زمان‌بندی ۲۱ دشمن، XP نگهبان باتوم 6، نقش‌ها (howler 5/3s، heal 8٪ با بودجه‌ی 25٪، شعاع انفجار)، فرزندها (پهپاد نزدیک‌زن 28HP، برجک 70HP)، باس‌ها (2600/6500/11000، XP انسان 356، الگوها)، فروشگاه 2.2.
- معنای فیلدها: `AttackInterval` شروع‌تاشروع، `ChargeDuration`، `HealFraction`/`HealBudgetFraction` (بودجه‌ی مشترک هر هدف، `EnemyState.HealReceived`)، باس `AttackInterval`/`LowHealthAttackInterval`/`FirstAttackDelay`/`BlastRecovery`.
- retry جان بازیکن و همراه را پر می‌کند؛ falloff شاتگان از نصف برد شروع می‌شود؛ نفوذ 75٪ آسیب باقی‌مانده را به هدف بعدی می‌دهد.
- جلوه‌ها: پرتاب جسد 0.25s، ماندن 6s، محو 1s، سقف 30 جسد و 20 لکه، tracer 0.06s (tracer در Play mode دیده نشد؛ فقط کامپایل و تست).
- تصمیم‌ها D61، D62. ۴۰۲ تست پاس (۴ تست جدید). Play mode: کشتن دشمن، جسد و retry بدون خطا.

### 2026-10-17 — T23: charger
- charger و charge باس با سرعت GDD کل خط هشدار را تا جای ثبت‌شده‌ی بازیکن + ۲ واحد می‌رود (D63). commit `18c30c1`.

### 2026-10-17 — T24: رابط فارسی
- RTLTMPro نسخه‌ی v4.0.0 (فقط اسکریپت‌ها و Ranges) در `Assets/Plugins/RTLTMPro`؛ اصلاح Unity 6 در 3D و اصلاح ترتیب ارقام با جداکننده در `LigatureFixer`. وزیرمتن از پوشه‌ی فونت skill فارسی.
- ۱۹۲ متن در `fa.json`؛ همه‌ی منوها، HUD، فروشگاه، کارت XP، شکست، پایان، راهنما و نوار بالا فارسی شد. تاریخ ذخیره شمسی. عدد آسیب و شمارش معکوس با رقم فارسی. فلش‌های HUD تصویر شدند.
- تست‌های `LocalizationTests`. در Play mode منو، کنترل‌ها، جایگاه‌ها، HUD، فروشگاه و کارت XP با اسکرین‌شات دیده شد. دیالوگ‌ها هنوز انگلیسی‌اند (T29). پنل‌های توسعه انگلیسی می‌مانند. تصمیم D64.

### 2026-10-17 — T25: انتخاب AI
- `AiId` و `BalanceProfile.Characters`؛ `XpSystem.ApplyCharacter` رتبه‌ی ۱ مهارت شخصیت را می‌دهد (جزو سقف ۳). `Player.Ai` ذخیره می‌شود و در خلاصه‌ی جایگاه، نتایج و telemetry می‌آید.
- صفحه‌ی `MenuScreens.ShowAiSelect` بعد از انتخاب جایگاه. `GameApp.StartNewGame(slot, ai)`. تست‌های `CharacterTests`. تصمیم D65.
- نکته: متن‌های جدید را در `scratchpad/fa_<task>.json` نمی‌توان نگه داشت (scratchpad موقت است)؛ `fa.json` خودش منبع است و باید مستقیم ویرایش شود.

### 2026-10-17 — T26: هدیه‌ی تفنگ
- ورود به فاز ۲ یک بار تفنگ صنعتی را رایگان و بدون equip می‌دهد (`PhaseSystem.GiveGift`، `Narrative.GiftReceived`)؛ بعد از صحنه، پنجره‌ی «همین حالا بردار / نگه دار برای بعد». دکمه‌ی اول پنجره‌های تأیید سمت راست است. تست‌های `GiftTests`. تصمیم D66.

### 2026-10-17 — T27: یک اسلات آیتم
- `Inventory.Selected` (ذخیره می‌شود، با اولین خرید تعیین می‌شود)؛ G نوع بعدی را انتخاب می‌کند، F آن را در دست می‌گیرد. HUD یک اسلات با نام، تعداد، «و N نوع دیگر» و راهنمای G/F. دکمه‌ی «اول» فروشگاه حذف شد. تصمیم D67.
