# agent.md — حافظه‌ی پروژه برای ایجنت

> **قانون اصلی (اجباری):**
> 1. در شروع هر کار جدید، **اول همین فایل را بخوان**. هدف این است که لازم نباشد کل پروژه دوباره خوانده شود.
> 2. **پس از اتمام هر تسک**، کارهای انجام‌شده را در بخش «تاریخچه‌ی کارها» اضافه کن و بخش‌های «وضعیت فعلی» و «ساختار پروژه» را هم اگر تغییر کرده‌اند به‌روز کن. این فایل باید **همیشه به‌روز** باشد.
> 3. اگر چیزی در این فایل با واقعیت پروژه نمی‌خواند، پروژه را مرجع بدان و این فایل را اصلاح کن.

---

## خلاصه‌ی پروژه

- **نام:** Game Craft Final — بازی **Hollow** (جم دو روزه). شوتر دوبعدی با دید 3/4 از بالا مثل Kill The Crows (نه پیکسل‌آرت)، سه فاز (حیوانات، ربات‌ها، انسان‌ها)، ۵ موج + ۱ باس در هر فاز.
- **مرجع طراحی:** GDD جدید **v0.19** است: `docs/GDD_v0.19.md` (تبدیل از `Assets/GDD/Hollow_Game_Design_v0.19_EN.docx`). کد حالا بر اساس v0.19 است (به‌جز موبایل)؛ `docs/GDD.md` نسخه‌ی قدیمی v0.12 برای مرجع است. فهرست تفاوت‌ها و وضعیت هر کدام در `docs/GDD_CHANGES_v0.19.md`. پرامپت اصلی کاربر شامل تسک‌های T00 تا T22 است؛ وضعیت در `docs/TASKS.md`.
- **Unity:** `6000.6.0f1`، URP با Renderer2D (sort axis = Y برای مرتب‌سازی بر اساس پا)، Input System جدید (actionها در کد، `UnityInputSource`)، Cinemachine 6.6 و AI Navigation (کاربر نصب کرده؛ Navigation استفاده نمی‌شود)، Newtonsoft JSON.
- **git:** شاخه‌ی `main`، یک commit برای هر تسک، بدون push مگر کاربر بخواهد.

## معماری (خلاصه)

- گیم‌پلی یک **شبیه‌سازی C# خالص** است (`Hollow.Sim`) با تیک ثابت ۶۰ هرتز؛ MonoBehaviourها فقط رندر می‌کنند و ورودی می‌دهند (D40–D47 در `docs/DECISIONS.md`).
- **همه‌ی اعداد گیم‌پلی** در `Assets/Resources/Balance/Baseline.json` هستند (کلاس‌ها در `Assets/Scripts/Data/BalanceProfile.cs`). هیچ عدد گیم‌پلی در کد نباشد. `Assets/Editor/BaselineSeed.cs` seed است و Baseline.json را بازنویسی می‌کند (با تأیید). از v0.19 عددها اول در seed عوض می‌شوند و بعد با `BaselineSeed.Write()` نوشته می‌شوند (قبلش seed و JSON مقایسه شدند و یکسان بودند).
- **پنل بالانس:** Editor: منوی `Hollow > Balance Panel` (Ctrl+Shift+B). داخل بازی: F9 (در Editor و development build). `BalanceService` = draft/live/apply/undo/reset group.

## ساختار پروژه

- Resources/Arena/ArenaLayout.json.Landmarks منبع مشترک چیدمان و برخورد هفت سازهٔ جنگل است؛ Resources/ArenaArt/Landmarks آرت‌ها را نگه می‌دارد. ArenaArtImportSettings کیفیت واردکردن و DetailedGround.shader جزئیات نزدیک کف را کنترل می‌کنند.

- Assets/Resources/ArenaArt شامل دو زمین ۳:۲ و اطلس سه prop جنگل است؛ ArenaAuthoring.ApplyComposition نمایش آن‌ها را روی مختصات قبلی انجام می‌دهد. فاز سوم از زمین صنعت با tint سرد استفاده می‌کند.

| مسیر | توضیح |
|---|---|
| `Assets/Scenes/` | `Boot` → `MainMenu` (منوی uGUI با پس‌زمینه‌ی parallax) → `Game` |
| `Assets/Scripts/Core/` | `Hollow.Core`: state machine، `SimClock`/تایمرها، `SimDriver`، `SimRandom`، `InputRouting/` (InputRouter، PlayerCommand) |
| `Assets/Scripts/App/` | `Hollow.App`: `GameApp` (ریشه‌ی سرویس‌ها)، ورودی Unity، `GameSceneRoot` |
| `Assets/Scripts/Data/` | `Hollow.Data`: پروفایل بالانس، JSON/CSV، calculator، validator، `BalanceService` |
| `Assets/Resources/Localization/fa.json` | همه‌ی متن‌های بازیکن به فارسی (کلید ← متن)، خوانده با `Hollow.UI.Loc` (`Loc.T`/`Loc.F`/`Loc.Weapon`…). متن جدید = کلید جدید در همین فایل؛ تست `LocalizationTests` کلیدهای کد را چک می‌کند |
| `Assets/Scripts/UI/HintRules.cs` + `HintCoach.cs` | راهنماهای یک‌باره (سلاح دوم، آیتم اول، نوع دوم آیتم، اپراتور زخمی؛ ۳ ثانیه، بازی در این مدت متوقف است). قانون‌ها در `HintRules` (تست‌پذیر)، نمایش spotlight در `HintCoach`؛ دیده‌شده‌ها با کلید `hint:Id` در `Narrative.SeenScenes` ذخیره می‌شوند. راهنمای جدید = مقدار جدید `HintId` + شرط در `Due` + متن `hint.<Id>` در `fa.json` |

| `Assets/Plugins/RTLTMPro/` + `Assets/Fonts/Vazirmatn/` | RTLTMPro (MIT، با یک اصلاح در `LigatureFixer` برای جداکننده‌ی بین ارقام) و فونت وزیرمتن؛ فونت TMP با `Hollow > UI > Build Persian Font` در `Resources/Fonts` ساخته می‌شود. پرانتز و کروشه کنار عدد یا متن لاتین به‌هم می‌ریزد؛ از آن‌ها استفاده نکن |
| `Assets/Scripts/Dev/` | `Hollow.Dev`: پنل بالانس IMGUI (`BalancePanelGUI`، `BalanceOverlay` روی F9) |
| `Assets/Editor/` | `ProjectSetup` (ساخت صحنه‌ها)، `BuildTool`، `BaselineSeed`، `BalanceWindow`، `PrefabBuilder`/`EnemyPrefabBuilder` (prefabهای PH_)، `ArenaTools`، `Art/` (importer آرت AI) |
| `Assets/Art/Incoming/` | آرت AI با نام طبق `docs/ART_MASTER_BRIEF.md` اینجا ریخته می‌شود؛ سپس با صحنه‌ی Game باز منوی `Hollow > Art > Apply Incoming Art` (SpriteSetها در `Assets/Art/SpriteSets`). موج ۱ آرت (۱۰۴ فایل) اینجاست؛ یادداشت تیم آرت و کارت‌ها در `docs/art/` |
| `Assets/Tests/EditMode/` | تست‌های NUnit (۴۹۱ تست، همه پاس؛ به `Hollow.Editor` هم ارجاع دارد) |
| `Assets/Scripts/Presentation/Menu/MenuBackdrop.cs` + `Assets/Art/Menu/` | پس‌زمینه‌ی parallax منوی اصلی (لایه‌ها با عمق، حرکت با ماوس، دود، چراغ چشمک‌زن، پنجره‌ی روشن، دستیار AI، جرقه). آرت با `Hollow > Art > Generate Menu Backdrop` (`Editor/Art/MenuArt.cs`) و صحنه با `Rebuild Main Menu Scene` (`MenuBackdropBuilder`) ساخته می‌شود |
| `ArtSource/` | فایل‌های منبع بیرون از Assets (import نمی‌شوند): پک CC0 کنی Background Elements Redux برای منو |
| `Tools/ArtGen/` | اسکریپت‌های پایتون آرت (فقط Pillow لازم است): `animate.py` از هر فریم تکی با خم کردن mesh (تاب پا حول لگن، بالا و پایین رفتن، خم شدن، لگد سلاح) sheet انیمیشن می‌سازد و در `Assets/Art/Incoming/Generated` می‌نویسد؛ `fix_art.py` مشکلات آرت موج ۱ را درست می‌کند و نسخه‌ی اصلی را در `ArtSource/ArtWave1_Original` نگه می‌دارد (دوباره اجرا کردنش همان نتیجه را می‌دهد). ترتیب: `fix_art.py`، بعد `animate.py`، بعد `Apply Incoming Art`. sheet دست‌کشیده‌ی هم‌نام همیشه بر sheet ساخته‌شده مقدم است |
| `Assets/Scripts/Presentation/Arena/` | `ArenaAuthoring` (لایه‌های هر فاز)، `ArenaProp` (مانع نام‌دار؛ footprint آن در bake به سلول مسدود تبدیل می‌شود)، `ArenaAtmosphere` (نور 2D هر فاز، درخشش بازیکن، مه، تاریک شدن پایان) |
| `Assets/Editor/VisualTuningWindow.cs` + `docs/VISUAL_DEPTH_PASS.md` | داشبورد `Hollow > Visual Tuning` برای رنگ نور، افکت‌های دوربین و تنوع روشنایی زمین در هر فاز؛ روش تست و محدودیت آرت در سند |
| `Assets/Scripts/Presentation/Feel/` | `FeedbackDirector`: لایه‌های حس بازی از روی SimEventها (muzzle با دود و پوکه، جرقه، خون/روغن پاشیده با لکه‌های کوچک، جسد/لکه با سقف، حلقه‌ها، ردّ دش، جلوه‌ی level up و مرگ بازیکن، عدد آسیب با محدودیت نرخ، shake با Cinemachine Impulse). چهار ParticleSystem (sparks، gore با جاذبه، puffs، motes رو به بالا) و حلقه‌ی ساخته‌شده با کد. لگد سلاح در `PlayerView.Kick`، `CompanionView.Fired(dir)` و `EnemyView.Kick` |
| `Assets/Scripts/Dev/FrameProbe.cs` | اندازه‌گیری زمان فریم و GC برای stress (T05/T21) |
| `Assets/Scripts/Presentation/Audio/` | `AudioDirector` (busها در کد، cooldown و priority، موسیقی هر فاز، جینگل پیروزی و بعد سکوت، stub بی‌صدا برای cue بدون فایل) و `AudioCatalog` (`Assets/Resources/Audio`، ساخت با `Hollow > Audio > Rebuild Catalog`؛ فایل‌ها در `Assets/Audio` با نام `<Cue>_<n>`) |
| `Assets/Scripts/Sim/Telemetry/` | `RunTelemetry`: لاگ هر بازی (Observed جدا از Calculated)؛ فایل در `persistentDataPath/telemetry` توسط `Game/TelemetryFile.cs` |
| `docs/` | GDD، QUESTIONS، DECISIONS، TASKS، USER_STORIES، `ART_MASTER_BRIEF.md` و `ART_ASSET_LIST.md` (پرامپت ساخت آرت با AI)، `ART_ANIMATION_BRIEF.md` (پرامپت انیمیشن موج ۲ از روی فریم‌های موجود)، `WeaponLevels_Template.csv` |

## قراردادها (Conventions)

- هر عدد گیم‌پلی → فیلد در `BalanceProfile` با برچسب `[Gdd]` یا `[Placeholder]`.
- شناسه‌ها enum هستند (`EnemyId`، `WeaponId`، …) نه رشته.
- کار با ادیتور باز از طریق `unity command ... --project-path` (اسکریپت‌های کمکی در scratchpad: compile/tests). Play mode در Editor حتی بدون فوکوس اجرا می‌شود (`runInBackground` فقط در Editor).
- اگر فایل جدیدی در یک پوشه‌ی تازه ساختی و Unity آن را کامپایل نکرد، پوشه را با `AssetDatabase.MoveAsset` جابه‌جا کن (مشکل کش؛ اتفاق افتاد).
- `Library/`، `Temp/`، `Logs/`، `UserSettings/`، `Builds/` تولیدی‌اند.
- **Enter Play Mode بدون Domain Reload است** (`EditorSettings`). هر فیلد یا event استاتیک قابل‌تغییر باید یک متد `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` برای reset داشته باشد؛ وگرنه از اجرای قبلی می‌ماند (D72). هیچ‌وقت شیء Unity را در closure یک event استاتیک نگه ندار.
- صدا: تنها AudioListener روی `AudioDirector` (DontDestroyOnLoad) است؛ به دوربین‌ها listener اضافه نکن. «در حال پخش» بودن AudioSource ثابت نمی‌کند صدا شنیده می‌شود.

## وضعیت فعلی

- D87: جان بازیکن دیگر خودکار پر نمی‌شود (فقط درمان فروشگاه). شارژ باس موانع فاز را می‌شکند و رد می‌شود (دیوار و ایستگاه نه؛ `World.BrokenCells` ذخیره می‌شود، `Sim/Arena/Obstacles.cs`). سایه‌ی باس از شعاع بدن ساخته می‌شود و باس دیگر معلق به نظر نمی‌رسد. باس‌ها هر ۸ ثانیه نیروی کمکی بدون پاداش می‌آورند و سقف دشمن‌ها بالاتر رفت. در Play mode شکستن سنگ و سایه‌ی باس ۱ دیده شد؛ با دست بازی نشده است.

- D86 (سختی): باس‌ها زره دارند (۳۰/۴۰/۴۵٪، جز وقت recovery یا stun) و جان بیشتر (3400/8000/13500)؛ چرخه‌ی حمله از داده (`BossData.Moves`) با دو حرکت تازه: pounce (پرش روی جای علامت‌خورده‌ی بازیکن) و leap (بیرون رفتن از نقشه، دایره‌ی فرود بازیکن را دنبال می‌کند، قفل، کوبیدن). رگبار باس ۲ پهن، سه‌تایی، رفت‌وبرگشتی و دنبال‌کننده است. تفنگ‌های رگباری ضعیف‌تر شدند و bloom دارند (رتیکل باز می‌شود). در Play mode دیده شد؛ با دست بازی نشده است.

- D85: آتش سر تفنگ پررنگ‌تر شد. E کنار هر ایستگاه فروشگاه را باز می‌کند (در موج و باس هم؛ D82 که خرید در نبرد را می‌بست جایگزین شد). فروشگاه زمان‌دار بین موج‌ها خودکار باز می‌شود و حالا با E بسته می‌شود؛ بالای آن نوشته «برای بستن E را بزن». کارت‌های راهنما خط «برای بستن راهنما E را بزن» دارند و E آن‌ها را زودتر می‌بندد.

- D84: تیر حالا به کل بدن دشمن‌ها و باس‌ها می‌خورد (کپسول عمودی با `HitHeight` در بالانس، نه فقط دایره‌ی پا). همه‌ی شخصیت‌ها موقع راه رفتن ردّ ذره دارند (پهپاد بازیکن: جرقه‌ی فیروزه‌ای؛ بقیه: گرد و خاک به رنگ زمین هر فاز، جرقه‌ی ربات‌ها، سنگ‌ریزه‌ی انسان‌ها). در Play mode هر سه فاز دیده شد؛ با دست بازی نشده است.

- پاس juice (D83): گلوله‌ها درست می‌چرخند، هشدار قرمز فقط برای charger و باس (کم‌رنگ‌تر)، پالس رانش حذف شد، لگد سلاح و muzzle برای بازیکن/اپراتور/دشمن تفنگ‌دار، shake شلیک و باتوم، جلوه‌ی دش، level up، مرگ دشمن و مرگ بازیکن. در Play mode دیده شد؛ هنوز کسی آن را با دست بازی نکرده است.

- شاخهٔ codex/gameplay-ux-pass اکنون گیم‌پلیِ آموزش/فروشگاه زمان‌دار و آخرین آرت/نقشهٔ main را با هم دارد. تغییرات محلی صحنه و واردکننده‌ها نیز حفظ شده‌اند.

- D79: علت قطع کنترل‌ها خطای برش اطلس کوچک‌شده بود؛ برش نرمال و راه‌اندازی مقاوم شد. هفت آرت جدید جنگل با footprint مشترک collider/شبیه‌سازی و کف دارای جزئیات نزدیک افزوده شدند. راهنما: docs/ARENA_LANDMARKS.md. شروع مرحله در Unity دیده شد؛ بررسی کامل ظاهر پس از توقف Computer Use توسط کاربر باقی است.

- نقشه با دو مرجع تازه بازترکیب شد: زمین یکپارچهٔ جنگل و صنعت، آرت سه‌ربع کنده/الوار/بیل مکانیکی، سایهٔ تماس کوچک‌تر و محوشدن اجسام بلند روی بازیکن. کنترل Composed arena floor در Visual Tuning برای مقایسه حاضر است. راهنما و پرامپت‌ها در docs/ARENA_COMPOSITION.md؛ نمایش نهایی Play Mode هنوز تأیید نشده است.

- پاس عمق بصری D77 برای اسکرین‌شات‌های پنج‌گانه: تنظیم مستقل رنگ نور/کنتراست/کاهش اشباع/تیرگی لبه‌ها برای هر فاز، Bloom ملایم دوربین دنیا، نور موضعی کیوسک‌ها و تنوع نرم روشنایی زمین. داشبورد `Hollow > Visual Tuning` با Apply در Play Mode و Save/Reset افزوده شد؛ بررسی تصویری در Play Mode هنوز لازم است. تغییرات `.meta` آرت که پیش از این تسک موجود بودند متعلق به این کار نیستند و دست نخورده‌اند.

- انجام‌شده: T00–T04، T06–T15، T16a (importer آرت)، T16 (سه حالت آرنا)، T05 (جلوه‌ها)، T19 (صدا)، T18 (telemetry و ابزار)، T20 (آموزش و خوانایی)، T21 (بات و کارایی)، T22 (build نهایی)، (منوها، pause، ذخیره/بارگذاری، checkpoint، فروشگاه، آیتم‌ها، ۲۴ نوع دشمن، باس‌ها، داستان، پایان)، T17. پنل بالانس F9 و پنل توسعه F10 (فقط dev). ۴۴۵ تست پاس. هر دو اسلات سلاح هر سلاح خریده‌شده را می‌گیرند (D73). منوی اصلی پس‌زمینه‌ی 2D parallax دارد (D74). راهنماهای یک‌باره با تیره شدن صفحه و روشن ماندن بخش مربوط HUD (D75). عددهای بالانس از GDD v0.19 هستند. T23 تا T31 (اعمال v0.19): charger، رابط فارسی، انتخاب AI، هدیه‌ی تفنگ، یک اسلات آیتم، دو ایستگاه، فیلم‌نامه‌ی فارسی، تنظیمات صدا و زیرنویس، صدای هر دشمن.
- کل بازی از ابتدا تا «You won.» در Play mode اجرا شد.
- باقی‌مانده: هیچ تسکی از T00–T22 نمانده. کارهای بعدی با طراح/کاربر: اعداد سطح ۲ و ۳ سلاح‌ها، playtest انسانی، آرت AI، گوش دادن به صداها (فهرست در `docs/RELEASE_NOTES.md`).
## کارهای باز / ایده‌ها

- **GDD v0.19**: همه‌ی بخش‌ها جز موبایل (F13، فقط اگر وقت بماند) انجام شد. با کاربر/تیم: گوش دادن به صداهای سنتزشده‌ی دشمن‌ها، ضبط صداپیشگی طبق `docs/VOICE_LINES.md`، بازبینی متن فارسی و فیلم‌نامه توسط آرتیست، playtest انسانی.

- همه‌ی حالت‌های شخصیت‌ها، دشمن‌ها و دو باس حالا sheet انیمیشن ساخته‌شده (`Tools/ArtGen`) دارند؛ وقتی sheetهای دست‌کشیده‌ی موج ۲ برسند، خودکار جایگزین می‌شوند. باس ۱ هنوز ظاهر بین خرس و گراز دارد (از آرت است، با کد درست نمی‌شود).
- آرت موج ۱ اعمال شد. هنوز آرت ندارند (placeholder می‌مانند): ۱۸ نوع دشمن (همه جز A1، A2، R1، R2، H2)، باس ۳، propها و decorهای فاز ۲ و propهای Stumps/LogPile/Excavator/LightPost، سلاح‌های BAT/IRF/MSG/HAM/ERF/ESG/EBL، آیتم‌ها جز نارنجک. از آرت موج ۱ هنوز استفاده نمی‌شود: آیکن‌ها (`ICO_*`)، دکمه/پنل/اسلات UI، `PKP_GRN` (رابط با کد ساخته شده و جای آیکن ندارد).
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

### 2026-10-17 — T28: دو ایستگاه خدمات
- `ArenaLayout.ShopPositions` و `NearestShop`؛ ایستگاه دوم در (12، 0.5) به صحنه‌ی Game اضافه و bake شد. هر دو یک فروشگاه با موجودی مشترک‌اند. تست‌ها: `EveryServiceStationOpensTheSameShopWithSharedStock` و جلوی هر دو ایستگاه باز است. تصمیم D68.

### 2026-10-17 — T29: فیلم‌نامه‌ی فارسی
- `Dialogue.json` با صحنه‌های C00 تا C06 (فارسی، زمان‌ها مطابق GDD)، نام گوینده‌ی دستیار = AI انتخاب‌شده، barkهای اختیاری با `Next` و `Cooldown`. منبع این متن‌ها `Dialogue.json` است (نه `fa.json`). تصمیم D69.

### 2026-10-17 — T30: صدای گفت‌وگو و زیرنویس
- تنظیمات «صدای گفت‌وگوها» و «زیرنویس». bus جدید `Voice`؛ فایل ضبط‌شده در `Resources/Audio/Voice/<شناسه>` با زیرنویس پخش می‌شود. فهرست شناسه‌ها: `docs/VOICE_LINES.md`. تصمیم D70.

### 2026-10-17 — T31: صدای هر دشمن
- ۲۷ پروفایل صدا (`AudioCatalog.Enemies`) با حضور، هشدار، حمله، ضربه و مرگ؛ باس‌ها هشدار جدا برای حمله‌ی ناحیه‌ای. ۱۱۱ فایل WAV سنتزشده با `Hollow > Audio > Generate Enemy Sounds` در `Assets/Audio/Enemies` (جایگزین‌پذیر با فایل هم‌نام). سقف ۲۴ صدا، ۴ هشدار هم‌زمان، فاصله‌ی ۰.۱۵ حضور. تست‌های `EnemyAudioTests`. تصمیم D71. هنوز کسی به صداها گوش نداده.

### 2026-10-17 — رفع باگ: دکمه‌های بی‌اثر و نبودن صدا
- هیچ صحنه‌ای AudioListener نداشت (از اول پروژه)؛ حالا روی `AudioDirector`. Domain Reload خاموش است و handler صدای کلیک به AudioDirector نابودشده از اجرای قبلی اشاره می‌کرد و قبل از عمل دکمه exception می‌داد. همه‌ی استاتیک‌ها reset می‌شوند و صدای کلیک دیگر عمل دکمه را قطع نمی‌کند. تست `UIButtonTests`. در دو اجرای پشت‌سرهم بررسی شد. تصمیم D72.

### 2026-10-17 — اسلات ۱ قابل تعویض و منوی parallax
- گزارش کاربر: «تپانچه یا اسلات ۱ را اصلاً نمی‌شود عوض کرد». کلیدهای ۱ و ۲ کار می‌کردند (با تزریق مستقیم ورودی بررسی شد)؛ محدودیت از طراحی بود. حالا هر دو اسلات هر سلاح خریده‌شده (و خود تپانچه) را می‌گیرند؛ در فروشگاه هر سلاح یک ردیف با دکمه‌های «به اسلات ۱/۲» دارد؛ جابه‌جایی بین اسلات‌ها swap است؛ اسلات ۱ هیچ‌وقت خالی نمی‌شود. `LoadoutState.Slot1` با نام ذخیره‌ی `SlotPistol` (سازگار با saveهای قبلی)، `WeaponSystem.Equip/CanEquip`، `ShopOffer.Slot`. اگر صفحه‌ی فروشگاه از پنل بلندتر شود، کوچک می‌شود. تست‌های جدید در `ShopTests`. تصمیم D73.
- منو: با اجازه‌ی کاربر پک CC0 کنی (۱ مگابایت) دانلود شد؛ منبع در `ArtSource/`. لایه‌های صنعتی، دکل برق، مه، دود، دستیار AI و vignette با کد ساخته می‌شوند. ستون دکمه‌ها سمت راست، بدون تیره کردن صفحه. تصمیم D74. اسکرین‌شات‌ها: `docs/screenshots/MainMenu_Parallax.png`، `Shop_WeaponSlots.png`.
- نکته‌ی تست: وقتی پنجره‌ی Game فوکوس ندارد، تزریق کلید با `QueueStateEvent` به actionها نمی‌رسد؛ برای آزمودن زنجیره‌ی ورودی، action را مستقیم در `pressed` ورودی Unity بگذار. تزریق موقعیت ماوس هم از CLI اعمال نمی‌شود.

### 2026-10-17 — راهنماهای یک‌باره و فروشگاه مرتب‌تر
- `HintRules` و `HintCoach`: وقتی بازیکن سلاح دوم، آیتم اول یا نوع دوم آیتم می‌گیرد (و چند مورد مشابه: سه سلاح، خشاب خالی، جان کم، اپراتور زخمی، سکه‌ی کافی برای ارتقا) یک کارت راهنما می‌آید؛ صفحه کمی تیره می‌شود و بخش مربوط HUD با قاب چشمک‌زن روشن می‌ماند. هر راهنما یک بار در هر بازی، با ذخیره. وقتی فروشگاه، منو، صحنه یا قدم‌های اول آموزش باز است صبر می‌کند. تصمیم D75.
- فروشگاه: دو کارت اسلات ۱ و ۲ با دکمه‌ی «جابه‌جا»، بعد سلاح‌های دیگر در یک خط با دکمه‌های اسلات، بعد خرید سلاح. پنل پهن‌تر (۶۶۰). `ShopPanel.Row` حالا تابع برچسب خودش را دارد.
- تست‌های `HintTests`. اسکرین‌شات‌ها: `docs/screenshots/Hint_WeaponSwitch.png`، `Shop_WeaponSlots.png`.
- نکته‌ی تست: برای اسکرین‌شات در Play mode بدون مردن بازیکن، `GameApp.Instance.ToolFreeze = true` شبیه‌سازی را نگه می‌دارد ولی UI (و راهنماها) کار می‌کنند. اسکریپت‌های پایتون طولانی را با heredoc در Bash اجرا نکن (خطای quote می‌دهد)؛ در فایل بنویس.

### 2026-10-17 — کوتاه کردن راهنماها
- به خواست کاربر راهنماهای چند سلاح، خشاب خالی، جان کم و سکه برای ارتقا حذف شدند. متن اپراتور زخمی حالا می‌گوید خود بازیکن هم در فروشگاه درمان می‌شود. زمان: حداکثر ۴ ثانیه (قبلاً ۷)، حداقل ۰.۸، فاصله‌ی بین دو راهنما ۱.۲ ثانیه. ۴۳۳ تست پاس.

### 2026-10-17 — اعمال آرت موج ۱
- کاربر پوشه‌ی `Assets/Art Images` (۱۰۴ PNG طبق brief) را اضافه کرد. فایل‌ها به `Assets/Art/Incoming` منتقل شدند، `HANDOFF_wave1.md` و `ASSET_CARDS` به `docs/art/`، و پوشه‌ی `Art Images` حذف شد. سپس `Apply Incoming Art` اجرا شد (۴۱ مجموعه).
- importer گسترش یافت: حاشیه‌ی آرنا (`ENV_P<n>_BorderH/V_prop`) با `ArenaTools.PlaceBorders` در `ArenaAuthoring.Borders` هر فاز (فقط نمایشی، Walls زیرشان)؛ فاز ۲ تا آمدن آرت خودش زمین و حاشیه‌ی فاز ۳ را می‌گیرد (`StandIn`)؛ decor بدون آرت در فاز دارای زمین آرت پنهان می‌شود؛ گلوله‌ها (`VisualCatalog.PlayerBulletArt/PlayerPelletArt/EnemyShotArt` بر اساس فاز)؛ جلوه‌های muzzle/impact/explosion به‌صورت strip در `FeedbackDirector.Fx`؛ جسد از فریم `death`؛ رتیکل؛ ظاهر فاز ۲ اپراتور (`CompanionView.phaseTwoSet`)؛ حالت `hit` بازیکن و اپراتور.
- در Play mode فاز ۱ و ۲ و ۳، دو باس، شلیک، جسد و جلوه‌ها دیده شد. اسکرین‌شات: `docs/screenshots/ArtWave1_Phase1.png`، `ArtWave1_Phase3.png`. ۴۳۳ تست پاس.
- نکته‌ی تست: اجرای تست‌ها با `unity command run_tests --mode EditMode --timeout 600`

### 2026-10-17 — بررسی مشکلات آرت موج ۱ (فقط بررسی، بدون تغییر کد)
- همه‌ی حالت‌های شخصیت‌ها یک فریم‌اند (تیم آرت در `HANDOFF_wave1.md` گفته strip بعداً می‌آید)؛ پس حرکت بی‌انیمیشن از asset است. ولی کد هم برای حالت تک‌فریمی حرکت جبرانی (تکان/کج شدن) ندارد.
- مشکلات سمت کد: `CompanionView` هیچ‌وقت حالت `attack` اپراتور را نمی‌خواهد؛ `BossView` در هشدار انفجار `blast` را نشان می‌دهد و `windup_blast` استفاده نمی‌شود.
- مشکلات سمت asset: باس ۱ حالت `move` ندارد و بین خرس و گراز قاطی است؛ رنگ حالت‌های باس ۲ یکی نیست؛ اپراتور فاز ۲ فقط `idle` دارد؛ درز در تکرار زمین فاز ۱ و حاشیه‌های P1 افقی و P3 عمودی؛ فریم ۲ و ۳ انفجار بریده و ۵ تا ۷ خالی؛ دکمه‌های UI چهار ارتفاع مختلف؛ آیکن سکه/قفل/بی‌نهایت تار. سایه‌ی زیر پا در هیچ‌کدام نیست.

### 2026-10-17 — رفع مشکلات سمت کد آرت موج ۱
- `SpriteSetAnimator`: وقتی موجود حرکت می‌کند و حالت فعلی فقط یک فریم دارد، رندرر آرت (فرزند body) با پرش و تکان کوچک «قدم» برمی‌دارد. سرعت از جابه‌جایی خود transform اندازه گرفته می‌شود و تعداد قدم با اندازه‌ی sprite تنظیم می‌شود. اعداد فقط نمایشی‌اند و روی کامپوننت هستند. strip چندفریمی همان‌طور که کشیده شده پخش می‌شود. برای پهپاد بازیکن خاموش است (`StepMotion = false`، خودش bob دارد).
- `CompanionView.Fired()`: با هر گلوله‌ی اپراتور ۰.۳ ثانیه حالت `attack` (صدا زده از `WorldPresenter` هنگام ساخت view گلوله).
- `BossView`: باس ۲ در هشدار `windup_blast` و بعد از انفجار `blast` نشان می‌دهد.
- ۴۳۳ تست پاس. در Play mode گرگ‌ها هنگام حرکت قدم برمی‌دارند و اپراتور بعد از شلیک حالت attack دارد؛ باس ۲ در Play mode دیده نشد.
- نکته: در Unity 6.6 `GetInstanceID()` منسوخ و خطای کامپایل است (`GetEntityId`). (بدون `--mode` صفر تست اجرا می‌شود). `eval` طولانی با خطای timeout ۵ ثانیه برمی‌گردد ولی کارش ادامه پیدا می‌کند؛ نتیجه را در console ببین.

### 2026-10-17 — رفع باگ: فاصله‌ی گلوله از سر تفنگ
- گزارش کاربر: گلوله‌ها دورتر از سر تفنگ ظاهر می‌شوند. علت: `PlayerView.Muzzle` سر لوله را لبه‌ی راست canvas فرض می‌کرد (۷۵٪ عرض از دسته)، ولی در آرت AI تفنگ فقط تا حدود ۵۸–۷۰٪ عرض canvas کشیده شده؛ فاصله ۰.۴۵ تا ۰.۷۷ واحد بود.
- رفع: `ApplyIncomingArt.BarrelTip` سر لوله را از پیکسل‌های تصویر پیدا می‌کند (راست‌ترین ستون مات) و در `WeaponVisual.MuzzleLocal` ذخیره می‌کند؛ `PlayerView.Muzzle` از آن استفاده می‌کند (با flipY). گلوله، muzzle flash و tracer همه از همین نقطه شروع می‌شوند. سلاح‌های بدون آرت فرمول قبلی را دارند.
- در Play mode برای تپانچه، SMG و دولول (چپ و راست) فاصله‌ی نقطه‌ی محاسبه‌شده تا سر لوله‌ی کشیده‌شده ۰.۰۱–۰.۰۲ واحد شد. ۴۳۳ تست پاس.

### 2026-10-17 — پرامپت انیمیشن آرت (موج ۲) و sheet شبکه‌ای
- `docs/ART_ANIMATION_BRIEF.md`: یک addendum که بعد از master brief چسبانده می‌شود؛ فریم موجود به‌عنوان reference ضمیمه می‌شود و برای هر حالت حرکت فریم‌به‌فریم نوشته شده (اولویت A: move گرگ، جوجه‌تیغی، Crawler، Shooter، نگهبان، اپراتور و move باس ۱؛ attack اپراتور). اشاره به آن در `ART_MASTER_BRIEF.md`.
- importer (`ArtRules.Grid`): sheet می‌تواند شبکه باشد (۴ فریم = ۲×۲ مربع، ۶ = ۳×۲ در 3:2، ۸ = ۴×۲) چون ابزارهای تصویر عکس 6:1 نمی‌دهند؛ ترتیب چپ به راست و بالا به پایین؛ strip یک‌ردیفه هنوز کار می‌کند. اگر فایل تک‌فریمی و sheet یک حالت هر دو باشند، sheet انتخاب می‌شود (`ApplyIncomingArt.Collect`).
- با یک sheet مصنوعی ۳×۲ گرگ بررسی شد (برش، pivot، اندازه‌ی ۲ واحد، انتخاب sheet) و فایل حذف شد. تست‌های `ArtSheetTests`؛ ۴۴۰ تست پاس.
- باز: همه‌ی sheetها با ۱۰ فریم در ثانیه پخش می‌شوند؛ وقتی sheetها رسیدند شاید idle به سرعت کمتری نیاز داشته باشد.

### 2026-10-17 — راهنما: ۳ ثانیه و توقف بازی
- به خواست کاربر راهنماهای یک‌باره حالا حداکثر ۳ ثانیه‌اند (قبلاً ۴) و وقتی یکی روی صفحه است بازی متوقف می‌شود: `GameSession` تا وقتی `HintCoach.Showing` است قدم شبیه‌سازی نمی‌زند و با `PushModal/PopModal` ورودی دنیا را نگه می‌دارد (در `OnDestroy` هم آزاد می‌شود). چون بازیکن در این مدت کاری نمی‌تواند بکند، بستن زودتر با «انجام دادن کار» عملاً پیش نمی‌آید؛ راهنما بعد از ۳ ثانیه بسته می‌شود. D75 به‌روز شد.
- ۴۴۰ تست پاس. در Play mode: با راهنمای سلاح دوم، tick شبیه‌سازی در مدت نمایش ثابت ماند، ورودی دنیا بسته بود و بعد از حدود ۳ ثانیه بازی ادامه پیدا کرد.

### 2026-10-17 — انیمیشن ساخته‌شده برای همه‌ی شخصیت‌ها و رفع مشکلات تصویر آرت موج ۱
- `Tools/ArtGen/animate.py`: از فریم‌های تکی، ۶۵ sheet (سلول ۵۱۲ پیکسل، شبکه‌ی سازگار با `ArtRules.Grid`) برای بازیکن، اپراتور فاز ۱ و ۲، پنج دشمن و دو باس: idle ۸ فریم (نفس کشیدن یا شناور بودن)، move ۶ فریم (چهارپا: تاب پاهای جلو و عقب، pitch، پرش؛ دوپا: هر پا یک لایه‌ی جدا که حول لگن می‌چرخد و پای جلو روی پای عقب کشیده می‌شود، بدن با بسته شدن پاها بالا می‌رود)، windup/attack/hit/recover، death (۲ فریم افتادن از حالت ایستاده + فرود روی فریم death؛ آخرین فریم همان فریم کشیده‌شده است پس جسد عوض نمی‌شود). حالت‌های نبوده ساخته شد: move باس ۱ از idle، و move/attack/hit/death اپراتور فاز ۲ (مرگ: افتادن به پشت). باس‌ها: charge، burst و stunned حلقه‌ای، تپش درخشش سبز در windup_blast.
- `Tools/ArtGen/fix_art.py`: رنگ burst (زنگ‌زده) و blast/windup_blast (سبز) باس ۲ با idle یکی شد (درخشش و شعله دست نخورد)؛ رگه‌های تیغ نقاشی‌شده در attack جوجه‌تیغی حذف شد؛ فریم‌های انفجار در سلول جا شدند، لبه‌ها محو و انتهای آن کم‌کم محو می‌شود؛ لبه‌ی muzzle و impact محو شد؛ درز تکرار حاشیه‌های P1 افقی و عمودی و زمین P1 برطرف شد؛ ستون حصار P3 از پنل‌های کامل دوباره ساخته شد (تکرار پنل به پنل، لکه‌های کنار حذف)؛ آیکن سکه، قفل و بی‌نهایت و چهار حالت دکمه (هم‌اندازه) دوباره کشیده شدند.
- `ApplyIncomingArt.Collect`: اولویت sheet دست‌کشیده > sheet ساخته‌شده > فریم تکی (`Rank`). `SpriteState.FramesPerSecond` (idle با ۶۰٪ سرعت)؛ charge/burst/stunned حلقه‌ای.
- در Play mode دیده شد: گرگ‌ها move/windup/recover، اپراتور idle/move/attack، بازیکن idle/hit/death با فریم‌های ساخته‌شده پخش می‌شوند. ۴۴۵ تست پاس (۵ تست جدید در `ArtSheetTests`). تصویر: `docs/screenshots/ArtAnimated_Walks.png`.
- محدودیت: حرکت‌ها از خم کردن یک تصویر ساخته شده‌اند؛ پای دوپاها در هر قدم جابه‌جا نمی‌شود (هر حلقه یک قدم)، و حالت حمله‌ی اپراتور فاز ۲ فقط لگد سلاح است. حجم sheetها حدود ۳۶ مگابایت (LFS).
- نکته‌ی تست: `capture_game_view --save_path X` فایل را در `Assets/X` می‌گذارد؛ بعد از برداشتن، پوشه و meta آن را پاک کن.

### 2026-10-17 — پاس عمق بصری و داشبورد نور
- پنج تصویر کاربر با مرجع v0.19 و نمونه‌های Kill the Crows، Hades و The Ascent بررسی شد. تکرار کاشی زمین، لکه‌های تیره، رنگ نزدیک فازها و placeholderهای صنعتی مسائل اصلی‌اند؛ اولویت این پاس عمق بصری همراه با خوانایی نبرد است.
- `AtmosphereData` و seed/JSON بالانس با رنگ نور، کنتراست، کاهش اشباع، vignette سه فاز، Bloom و تنوع روشنایی زمین گسترش یافت. `ArenaAtmosphere` حجم post-process دنیا و نور فیروزه‌ای کیوسک‌ها را می‌سازد؛ `ArenaAuthoring` نویز نرم غیرتکراری روی زمین می‌گذارد؛ `GameSession` این‌ها را هنگام شروع/بارگذاری/Apply زنده به‌روزرسانی می‌کند. `VisualTuningWindow` و راهنمای `docs/VISUAL_DEPTH_PASS.md` افزوده شد. برای جلوگیری از حذف shaderهای Volume ساخته‌شده در اجرا، حذف variantهای استفاده‌نشدهٔ Post Processing در تنظیمات URP خاموش شد. تصمیم D77.
- اسمبلی‌های Data/Presentation/Game/Editor/Tests.EditMode با کامپایلر Unity کامپایل شدند؛ JSON و بازه‌های تنظیم معتبرند. Test Runner به‌دلیل باز بودن پروژه در Editor از CLI اجرا نشد و بازبینی دیداری Play Mode باقی است. فایل‌های `.meta` آرت موجود پیش از این تسک در commit این کار وارد نمی‌شوند.

### 2026-10-18 — ترکیب‌بندی سه‌ربع نقشه براساس مرجع کاربر
- با imagegen داخلی دو زمین غیرتکراری و اطلس شفاف کنده، الوار و بیل مکانیکی تولید، بررسی و در Resources/ArenaArt ذخیره شد. ApplyComposition زمین‌ها و سه آرت جایگزین را با pivot پایه روی موانع قبلی می‌نشاند. سایه‌ها براساس footprint کوچک شدند و اجسام بلند هنگام پوشاندن بازیکن نرم محو می‌شوند. تنظیم‌های ComposedGround و ContactShadowStrength در seed، JSON و داشبورد حاضرند.
- اسمبلی‌های Data/Presentation/Game/Editor/Tests.EditMode کامپایل شدند؛ Test Runner به‌علت باز بودن پروژه در Editor اجرا نشد. نمایش واقعی Play Mode هنوز بررسی نشده است. فایل‌های art meta و تصاویر exec موجود پیش از این کار دست نخورده‌اند. تصمیم D78 و راهنمای docs/ARENA_COMPOSITION.md.

### 2026-10-18 — رفع قطع کنترل‌ها و نصب هفت سازهٔ جنگل با برخورد
- خطای ابعاد اطلس در Editor.log پیدا شد. ImportedAtlasRect برش را با اندازهٔ واردشده مقیاس می‌کند و ApplyArenaVisuals خطای آرت را از مسیر Loading جدا می‌کند. تست رگرسیون برای اندازه‌های واقعی/کوچک‌شده و shader افزوده شد.
- هفت PNG ارسالی در Resources کپی و با footprintهای معتبر و ماسک گوشه‌ها به ArenaLayout اضافه شدند. نمای Runtime و BoxColliderهای ردیفی از همان دادهٔ برخورد سیم ساخته می‌شوند؛ Bake رکوردها را حفظ می‌کند. ذخیره‌های قدیمی در صورت برخورد با سازهٔ تازه به نقطهٔ آزاد منتقل می‌شوند. کف shader دارای detail و واردکنندهٔ بدون فشرده‌سازی دارد.
- Sim/Presentation/Game/Editor/Tests.EditMode کامپایل؛ تست اجرایی Mono حرکت چهار جهت، برش و برخورد/خروج پاس؛ بررسی مسیرها در هر سه فاز پاس. ورود از منو به مرحله در Unity بدون خطای قبلی مشاهده شد. کاربر Computer Use را با Escape متوقف کرد؛ اجرای کامل NUnit و بررسی کامل نمای نهایی باقی است. جایگاه خالی سوم برای بررسی استفاده شد، دو جایگاه قبلی دست نخورده‌اند. تصمیم D79؛ docs/ARENA_LANDMARKS.md و نقشهٔ تشخیصی برخورد.

### 2026-10-17 — بازبینی آموزش، فروشگاه، اپراتور و اقتصاد
- فایل Word مرجع v0.19 خوانده و با `docs/GDD_v0.19.md` و بالانس فعلی سنجیده شد. طبق درخواست کاربر، نمایش بالای HUD فقط محیط و حالت نبرد/استراحت را نشان می‌دهد؛ فروشگاه تب فاز ندارد و پیشنهادهای بازشده تا فاز فعلی را نشان می‌دهد. فلش آموزش به نزدیک‌ترین سکهٔ باقی‌مانده می‌رود. متن شروع با دش آغاز می‌شود و می‌گوید مرگ اپراتور بازی را تمام می‌کند.
- خرید SMG آموزشی یک نارنجک رایگان می‌دهد؛ گروه دوم تا استفادهٔ موفق از آن صبر می‌کند. اپراتور پس از ۸ ثانیه بی‌آسیب‌ماندن با نرخ ۲ HP/s تا سقف جان خود ترمیم می‌شود؛ این اعداد در seed و Baseline.json هستند. پروفایل‌های ذخیره‌شدهٔ قبل از revision 3 فقط دو فیلد تازه را از baseline می‌گیرند.
- بررسی عددی: داکیومنت ۵۰۸۹ سکهٔ درآمد سناریویی و ۱۴۲۱ سکه ماندهٔ مسیر خرید نمونه پیش‌بینی می‌کند. خشاب‌ها زمان reload دارند اما مهمات ذخیره محدود نیست. توصیهٔ اولیه حفظ دو اسلات و آزمودن تنظیم سلاح و درآمد پس از دادهٔ playtest؛ گزارش کامل در `docs/BALANCE_REVIEW_2026-10-17.md`. تصمیم D77.
- فایل‌های تغییرکرده: `Assets/Editor/BaselineSeed.cs`، `Assets/Resources/Balance/Baseline.json`، `Assets/Resources/Localization/fa.json`، `Assets/Scripts/Data/BalanceProfile.cs` و `BalanceProfileStore.cs`، `Assets/Scripts/Game/GameSession.cs`، `Assets/Scripts/Sim/Companion/CompanionSystem.cs`، `Assets/Scripts/Sim/Items/ItemSystem.cs`، `Assets/Scripts/Sim/Shop/ShopSystem.cs`، `Assets/Scripts/Sim/Waves/WaveSystem.cs`، `Assets/Scripts/UI/ShopPanel.cs`، تست‌های ویرایش‌شده، `docs/DECISIONS.md`، گزارش بالانس و `agent.md`.
- تأیید: پنج اسمبلی `Hollow.Data/Sim/UI/Game/Tests.EditMode` با کامپایلر Unity بدون خطا کامپایل شدند؛ دو JSON معتبرند و کلیدهای تازهٔ فارسی حاضرند. تست‌های NUnit و بررسی ظاهری در Play Mode به اتصال Pipeline ادیتور نیاز دارند و اجرا نشدند. فایل تغییرکردهٔ مستقل `ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json` دست‌نخورده ماند.

### 2026-10-17 — زمین و آرنای 3/4
- گزارش کاربر: زمین بازی 3/4 نیست و دوبعدی صاف است. علت: brief زمین را «از بالای بالا» خواسته بود و بافت ۸ واحدی با تکرار شبکه‌ای دیده می‌شد؛ decalهای زمین آزادانه می‌چرخیدند؛ سایه‌ها کم‌رنگ بودند.
- `Tools/ArtGen/ground34.py`: هر زمین (از نسخه‌ی اصلی در `ArtSource/ArtWave1_Original`) به ۷۵٪ ارتفاع کوتاه می‌شود و یک بافت بی‌درز ۲۴×۲۴ واحدی (۳۰۷۲ پیکسل) ساخته می‌شود: خاک فاز ۱ با کپی‌های آینه‌ای و جابه‌جا زیر ماسک نرم؛ بتن فاز ۳ (و ۲) با تخته‌های ۴×۳ واحدی که جابه‌جا و رنگ‌به‌رنگ می‌شوند. درز زمین P1 حالا اینجا درست می‌شود نه در `fix_art.py`.
- importer: زمین یک sprite تکی با ۱۲۸ پیکسل در واحد، FullRect و wrap Repeat (`ArtRules.GroundPixelsPerUnit`)؛ برش ۶۴ تایلی حذف شد و پوشه‌ی `Assets/Art/Tiles` پاک شد. `ArenaTools.PaintGround` زمین آرت را با یک SpriteRenderer تکرارشونده (`GroundArt`) روی کل آرنا می‌کشد.
- `ArenaTools.FlattenDecor`: decalهای چرخیده چرخششان را روی فرزند Body می‌گیرند و با ارتفاع ۷۵٪ کشیده می‌شوند (`GroundSquash`)؛ از `Apply Incoming Art` و ساخت صحنه صدا زده می‌شود. `EdgeShade`: نوار تیره‌ی نرم داخل دیوارها (شمال ۲.۴ واحد، کناره‌ها ۰.۹، جنوب ۰.۵) زیر `Border_P<n>`. `PH_Shadow` پررنگ‌تر (هسته ۰.۷) و `PH_EdgeShade` جدید.
- در Play mode هر سه فاز با دشمن دیده شد؛ تصویر: `docs/screenshots/Ground34_Phase1.png`، `Ground34_Phase3.png`. ۴۴۶ تست پاس (تست جدید `GroundArtIsOneTiledSpriteAt128PixelsPerUnit`). تصمیم D76.
- باز: آرت حاشیه‌ها (به‌خصوص حاشیه‌ی کناری P3 که پنل حصار را از بالا نشان می‌دهد) هنوز همان آرت موج ۱ است؛ اگر آرت جدید زمین از تیم بیاید فقط کافی است `ground34.py` و بعد `Apply Incoming Art` اجرا شود.

### 2026-10-17 — زمین فاز ۲ و ۳ با عمق
- بازخورد کاربر: فاز ۱ خوب است ولی فاز ۲ و ۳ هنوز تخت به نظر می‌رسد (فاز ۲ تا آمدن آرت خودش زمین فاز ۳ را دارد). علت: شبکه‌ی تخته‌های هم‌سطح با نور یکنواخت؛ مستطیل به‌تنهایی عمق نشان نمی‌دهد.
- `ground34.slabs`: هر تخته حالا رو + لبه‌ی جلو دارد: لبه‌ی بالا و چپ روشن (نور از بالا-چپ)، لبه‌ی راست تیره، لبه‌ی جلوی تیره (۷ پیکسل)، درز تیره؛ ۲۰٪ تخته‌ها بالاتر (لبه‌ی جلو +۱۸ پیکسل) و ۱۵٪ فرورفته (سایه از تخته‌ی پشتی). ردیف‌ها هم‌راستا ماندند؛ ردیف‌های جابه‌جا با لبه‌ی جلو شبیه دیوار آجری دیده می‌شدند.
- در Play mode فاز ۲ و ۳ دیده شد؛ تصویر: `docs/screenshots/Ground34_Phase2.png`، `Ground34_Phase3.png`. ۴۴۶ تست پاس. D76 به‌روز شد.

### 2026-10-18 — commit و push
- `git pull` (چیز جدیدی نبود)، commit اطلس فونت‌ها و organizationId، و push هر سه commit به `origin/main`. پوشه‌ی `Assets/_Recovery` (نسخه‌ی بازیابی صحنه‌ی یونیتی بعد از crash) commit نشد.

### 2026-10-17 — شروع از منو، آموزش نارنجک، رابط و بالانس مبارزه
- شاخهٔ `codex/gameplay-ux-pass` از آخرین commit ساخته شد. Play ادیتور از MainMenu آغاز می‌شود. خرید آموزشی با فلش روی دکمهٔ واقعی و چک‌پوینتِ پس از خرید، و نارنجک با دشمن تمرینی ثابت، نگه‌داشتن برای نشانه‌گیری، رهاکردن برای پرتاب، انتظار برای انفجار و جبران پرتاب ناموفق اجرا شد.
- نوار reload با نشان‌های هر گلوله جایگزین شد؛ مدت بارگذاری به نسبت خشاب خالی است و تیرها یکی‌یکی اضافه می‌شوند. HUD محیط و شمارهٔ موج را نشان می‌دهد. Pause گزینهٔ Load ندارد؛ تنظیمات زیرنویس و جلوهٔ تصویری پنهان‌اند؛ جایگاه‌ها درصد پیشرفت، زمان بازی و آخرین ذخیره را نمایش می‌دهند. متن‌های گذرای اپراتور در بابل بالای سرش با فاصلهٔ ۲۸ تا ۴۴ ثانیه و جدا از دیالوگ داستانی می‌آیند.
- بازسازی جان بازیکن (۶ ثانیه، ۳ HP/s)، مقاومت اپراتور فاز صنعتی (۲۶۰ HP و ۳۵٪ آسیب)، سقف/سرعت موج‌های انسانی و مهارت مین دش (از فاز ۲، ۰٫۷ ثانیه تأخیر و آسیب رتبه‌ای ۴۰/۶۵/۹۰) در seed و Baseline.json ثبت شد. پروفایل‌ها و ذخیره‌های قدیمی مهاجرت داده می‌شوند. تصمیم D78.
- همهٔ اسمبلی‌های `Hollow.Core/Data/Sim/App/UI/Game/Editor/Tests.EditMode` با Roslyn یونیتی کامپایل شدند و JSONها معتبرند. `unity test` به‌سبب باز بودن پروژه در Editor بدون اتصال Pipeline اجرا نشد؛ بررسی Play Mode باقی است. تغییر مستقل `ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json` دست‌نخورده ماند.

### 2026-10-18 — ادغام شاخه‌های گیم‌پلی و آرت
- تغییرات اخیر محلی Unity حفظ شد؛ main و Art-Improvement در codex/gameplay-ux-pass ادغام شدند. تعارض‌ها با اولویت آرت و صحنهٔ تازه و حفظ قابلیت‌های گیم‌پلی حل شدند.

### 2026-10-18 — merge شاخه‌ی codex/gameplay-ux-pass در main
- conflictها: meta زمین‌های P1 و P3 و اطلس وزیرمتن از main (زمین یک sprite تکی ۱۲۸ پیکسلی طبق D76 است). در `docs/DECISIONS.md` تصمیم D76 (زمین 3/4) از main ماند و تصمیم‌های شاخه از D76–D81 به **D77–D82** شماره‌گذاری دوباره شدند (ارجاع‌های همین فایل هم عوض شد). تاریخچه‌ی هر دو طرف نگه داشته شد.
- زمین: در commit بعدی به خواست کاربر فقط زمین شاخه ماند (پایین را ببین).
- کامپایل بدون خطا. ۴۶۵ تست، ۴۶۰ پاس؛ ۵ تست ردشده همه از کد شاخه‌اند (روی شاخه NUnit اجرا نشده بود): `LocalizationTests` (کلید ساخته‌شده‌ی `operator.idle` + عدد)، `CompanionTests.PhaseTwoEntryRestoresToTheLowerMaximum` (۱۰۰ در برابر ۲۶۰ HP جدید)، `FullRunTests` (پاداش موج ۱ فاز ۱ با گرگ تمرینی نارنجک: ۳۱ سکه/۲۳ XP به‌جای ۲۹/۲۰)، `NoUnlistedNumberLiteralsInTheSimulation` (`ShopSystem.cs` عدد 3.5f)، `TelemetryRecordsTheTutorialWave` (موج آموزش unfinished).

### 2026-10-18 — زمین شاخه به‌جای زمین main و رفع ۵ تست
- به خواست کاربر زمین فقط از شاخه‌ی gameplay-ux است: کف ترکیبی (`ArenaAuthoring.ApplyComposition`، D78) روی tilemap آرت ۸×۸ (۶۴ تایل در `Assets/Art/Tiles`). از زمین main حذف شد: `Tools/ArtGen/ground34.py`، زمین تک‌sprite ۱۲۸ پیکسلی در `ArtImport`/`ArenaTools.PaintGround` (`PaintGround` حالا `GroundArt` قدیمی صحنه را پاک می‌کند)، تست `GroundArtIsOneTiledSpriteAt128PixelsPerUnit`، نسخه‌ی اصلی P3 در `ArtSource`. تصویرهای زمین P1/P3 همان نسخه‌ی شاخه‌اند. decal خوابیده، سایه‌ی زیر دیوار و سایه‌ی تماس main ماندند. صحنه‌ی Game دوباره رنگ شد (۱۹۲ تایل). D76 به‌روز شد.
- تست‌ها: کلیدهای `operator.idle.<n>` (پیشوند نقطه‌دار مثل `ai.`)؛ تست اپراتور فاز ۲ جان را از پروفایل می‌خواند (۲۶۰)؛ گرگ تمرینی نارنجک kill حساب می‌شود ولی XP و سکه نمی‌دهد (`Rewards.OnEnemyKilled`) تا پاداش موج همان بودجه‌ی GDD بماند؛ فاصله‌ی آن `Spawning.TutorialGrenadeTargetDistance` = 3.5 (seed و JSON)؛ تست telemetry نارنجک تمرینی را پرتاب می‌کند و ۷ kill می‌شمارد. D80 به‌روز شد.
- ۴۶۳ تست پاس. Play mode از CLI شروع نشد (فوراً stopped، بدون خطا)؛ زمین در Play mode دیده نشده.
- نکته: یک جلسه‌ی دیگر (VFX/juice) هم‌زمان در همین پوشه کار می‌کند؛ فایل‌های نیمه‌کاره‌ی آن commit نشدند و در فایل‌های مشترک (`BalanceProfile.cs`، `BaselineSeed.cs`، `Baseline.json`) فقط hunkهای این کار stage شد.

### 2026-10-18 — پاس juice: جلوه‌ها، لگد سلاح و shake
- گلوله: هسته‌ی prefab گلوله (`PH_Projectile_*`) برای placeholder ۹۰ درجه چرخیده بود و آرت وارد شده هم با آن می‌چرخید؛ `ProjectileView` حالا برای آرت چرخش را صفر می‌کند و برای placeholder برمی‌گرداند.
- هشدار قرمز: `EnemyView` فقط برای charger خط مسیر می‌کشد (کم‌رنگ‌تر: آلفای 0.12 تا 0.38)؛ خط نشانه‌گیری تیرانداز، قوس حمله‌ی نزدیک، دایره‌ی انفجاری و دایره‌ی پرتاب‌کننده حذف شد (شمارش معکوس انفجاری و هاله‌ی زرد درمان مانده‌اند). هشدار باس و منطقه‌ی hazard دشمن کم‌رنگ‌تر شد. رویدادهای telegraph شبیه‌سازی دست نخوردند.
- پالس رانش: از seed و `Baseline.json` حذف شد؛ مقدار enum برای saveهای قدیمی ماند. `BalanceProfile.HasItem`، `ItemSystem.Available` نوع بدون داده را رد می‌کند، `WorldSerializer` موجودی فروشگاه save قدیمی را هم‌تراز می‌کند، revision 6 پروفایل ذخیره‌شده را مهاجرت می‌دهد. کد `Push` و متن فروشگاه و cue آن حذف شد. تست‌های آیتم/فروشگاه/راهنما/باس با مین و EMP بازنویسی شدند؛ تست جدید `ThePushPulseIsGoneEvenFromAnOldSave`.
- لگد سلاح: `PlayerView.Kick` (سلاح ۱۱ درجه بالا و ۰.۱ واحد عقب، برگشت نمایی؛ اعداد روی prefab)، بدن اپراتور و دشمن تفنگ‌دار (ربات و انسان تیرانداز، نه حیوان و نه پرتاب‌کننده: `FeedbackDirector.FiresAGun`) هنگام شلیک عقب می‌رود. muzzle: strip آرت یا glow، نور، جرقه، دود و پوکه‌ی برنجی؛ برای دشمن رنگ گرم به‌جای قرمز هشدار.
- shake: `ShakeOnShot` 0.09 (شاتگان ×۲)، داده‌های جدید `ShakeOnMelee` 0.12، `ShakeOnDash` 0.06، `ShakeOnPlayerDeath` 0.6؛ `DeathSparks` 22 و `DeathGore` 26 (seed، JSON، مهاجرت).
- جلوه‌های جدید: دش (گرد و خاک، حلقه، جرقه، ردّ تصویر فیروزه‌ای هر ۰.۰۳۵ ثانیه، کش آمدن بدن)، level up (لحظه‌ای که کارت XP آماده می‌شود: نور طلایی، سه حلقه، جرقه و ذرات رو به بالا؛ انتخاب مهارت هم یک حلقه‌ی کوچک)، مرگ دشمن (پاشش خون/روغن با تکه‌ها، مه خون، حلقه، نور مرگ، ۷ لکه‌ی کوچک که با تأخیر روی زمین می‌نشینند و بعد از ۴ ثانیه محو می‌شوند)، مرگ بازیکن (نور، سه حلقه، ۴۰ جرقه، تکه‌های فلزی، دود، ذرات)، ضربه‌ی باتوم (جرقه‌ی قوس). انفجار و مرگ باس هم حلقه و پاشش گرفتند.
- ۴۶۳ تست پاس. Play mode (جایگاه ۳، بعد پاک شد): مرگ دشمن، دش واقعی، شلیک و level up دیده شد، بدون خطای console. تصویرها: `docs/screenshots/Juice_KillDashLevelUp.png`، `Juice_DashAfterimage.png`. لگد سلاح و شدت shake فقط در کد بررسی شد و با دست بازی نشده است. تصمیم D83.
- فایل‌های تغییرکرده: `Presentation/Feel/FeedbackDirector.cs`، `Presentation/WorldPresenter.cs`، `Views/PlayerView.cs`، `CompanionView.cs`، `EnemyView.cs`، `BossView.cs`، `ProjectileView.cs`، `SpriteSetAnimator.cs`، `Data/BalanceProfile.cs`، `BalanceProfileStore.cs`، `Ids.cs`، `Editor/BaselineSeed.cs`، `Resources/Balance/Baseline.json`، `Sim/Items/ItemSystem.cs`، `Sim/Save/WorldSerializer.cs`، `UI/ShopPanel.cs`، `Audio/AudioDirector.cs`، تست‌های Item/Shop/Hint/Boss، `docs/DECISIONS.md`، `agent.md`.

### 2026-10-18 — commit و push پاس juice
- `cebf9cb` (پاس juice) و `5fbfe32` (تغییرات خودکار یونیتی: spriteIDهای تازه با internalID ثابت، ورودی WebGL در meta تصویرها، glyphهای اضافه‌شده‌ی فونت وزیرمتن) روی `main` به `origin/main` push شدند. `Assets/_Recovery` مثل قبل commit نشد.

### 2026-10-18 — برخورد تیر با کل بدن و ردّ راه رفتن
- گزارش کاربر: در فاز ۳ تیر به شکم به بالای تیراندازها نمی‌خورد و از آن‌ها رد می‌شد. علت: شبیه‌سازی تیر را روی خط زمین می‌برد و نمایش آن را ۰.۳۵ واحد بالاتر می‌کشد، ولی بدن فقط دایره‌ای دور پا بود؛ برای همه‌ی دشمن‌های بلند (و باس‌ها) همین بود. حالا بدن یک کپسول عمودی است: `SimMath.SegmentCapsuleHit` (دایره‌ی پا که به اندازه‌ی `HitHeight` رو به بالای صفحه کشیده می‌شود). `HitHeight` فیلد تازه‌ی `EnemyData` و `BossData` است؛ در seed از جدول `BodyTops` (بالای بدن کشیده‌شده، اندازه‌گیری‌شده روی آرت) منهای ۰.۳۵ و شعاع حساب می‌شود. revision 7 پروفایل ذخیره‌شده را مهاجرت می‌دهد. تیری که از داخل بخش بالایی بدن شروع شود فقط وقتی به سمت بدن برود می‌خورد. بدن بازیکن و اپراتور عوض نشد.
- ردّ راه رفتن (`FeedbackDirector.Walking`، فقط نمایشی): دو ParticleSystem تازه روی لایه‌ی Default زیر همه‌ی بدن‌ها (`WalkDust`، `WalkGlints`). پهپاد بازیکن جرقه‌ی فیروزه‌ای از زیرش می‌ریزد؛ اپراتور، حیوان‌ها، ربات‌ها، انسان‌ها و باس‌ها گرد و خاک به رنگ زمین هر فاز بلند می‌کنند؛ ربات‌ها جرقه‌ی سایش (شارژرها بیشتر)، انسان‌ها سنگ‌ریزه، پهپاد فرزند R7c دود گرم موتور. بر اساس مسافت طی‌شده در تیک‌های شبیه‌سازی (هنگام توقف بازی چیزی نمی‌سازد).
- تست‌های تازه‌ی `HitShapeTests` (شلیک به سینه‌ی H1/H2/H6/R2 آسیب می‌زند، بالای سر نمی‌خورد، کپسول باس، شلیک رو به بیرون از بدنی که روی تیرانداز افتاده). ۴۷۲ تست پاس. seed و `Baseline.json` دقیقاً یکی‌اند. Play mode (صحنه‌ی Game مستقیم، بدون دست زدن به جایگاه‌ها): ردّ هر سه فاز دیده شد و شلیک به سینه‌ی نگهبان تفنگ‌دار آسیب زد. تصمیم D84.
- نکته‌ی تست: `playModeStartScene` بعد از هر recompile دوباره MainMenu می‌شود (`ProjectSetup`)؛ برای اجرای مستقیم صحنه‌ی Game قبل از هر Play آن را null کن.
- فایل‌های تغییرکرده: `Sim/SimMath.cs`، `Sim/Combat/ProjectileSystem.cs`، `Data/BalanceProfile.cs`، `Data/BalanceProfileStore.cs`، `Editor/BaselineSeed.cs`، `Resources/Balance/Baseline.json`، `Presentation/Feel/FeedbackDirector.cs`، `Tests/EditMode/Sim/HitShapeTests.cs`، `docs/DECISIONS.md`، `agent.md`.

### 2026-10-18 — تنظیم ردّ راه رفتن
- به خواست کاربر: ردّ جرقه‌ی پهپاد بازیکن حدود نصف شد (`GlintStride` 0.17، کوچک‌تر و کم‌رنگ‌تر). گرد و خاک NPCها و اپراتور بزرگ‌تر و پخش‌تر شد و هر پف به‌طور تصادفی کوچک (۰.۵–۰.۷۵)، معمولی (۰.۹–۱.۱) یا بزرگ (۱.۳۵–۱.۷۵) است؛ پف‌های بزرگ کم‌رنگ‌تر و ماندگارترند (`FeedbackDirector.FootDust`). در Play mode فاز ۱ دیده شد. ۴۷۲ تست پاس. D84 به‌روز شد.
- سپس به خواست کاربر تعداد گرد و خاک NPCها حدود نصف شد (فاصله‌ی قدم‌ها `StrideBase` 0.45 و `StridePerRadius` 1.2، پف کوچک پاشنه فقط ۴۰٪ وقت‌ها)؛ تنوع اندازه دست نخورد. در Play mode دیده شد، ۴۷۲ تست پاس.

### 2026-10-18 — آتش سر تفنگ، باز کردن فروشگاه با E، بستن راهنما با E
- `FeedbackDirector.GunFlash`: strip بزرگ‌تر (×۱.۴۵)، هسته‌ی سفید، زبانه‌ی شعله‌ی نارنجی در دو glow رو به جلو، نور پخش بزرگ‌تر و کم‌رنگ‌تر، جرقه‌ها دو برابر به‌اضافه‌ی چند جرقه‌ی سریع نارنجی، دود بیشتر. `Flash` آلفای اختیاری گرفت و pool آن از ۲۴ به ۴۸ رسید. فقط نمایشی؛ با دست بازی و دیده نشده (شلیک از CLI تزریق نشد).
- `ShopSystem.Step`: E کنار ایستگاه در هر encounter به‌جز PhaseComplete فروشگاه را باز و بسته می‌کند. فروشگاه زمان‌دار هنوز در `WaveSystem` خودکار باز می‌شود؛ دیگر هر تیک دوباره باز نمی‌شود، E آن را می‌بندد و حرکت و دش نه. `ShopPanel`: خط بالا در فروشگاه زمان‌دار با رنگ accent «{N} ثانیه تا موج بعد · برای بستن E را بزن». `GameSession`: «برای باز کردن فروشگاه E را بزن» حالا کنار هر ایستگاه بسته دیده می‌شود.
- `HintCoach`: کارت بلندتر با خط «برای بستن راهنما E را بزن» (`hint.close`)؛ `InputRouter.InteractDownThisFrame` فشردن E را حتی وقتی ورودی دنیا بسته است می‌دهد و `HintCoach.Sync` با آن کارت را می‌بندد.
- تست `WaveTests` به قانون تازه به‌روز شد. ۴۷۲ تست پاس. Play mode: فروشگاه زمان‌دار با متن تازه، بسته شدن آن با E، باز شدن با E کنار ایستگاه در موج، و بسته شدن کارت راهنما با E (۰.۲۵ ثانیه بعد از فشردن) دیده شد. تصمیم D85.

### 2026-10-18 — سخت‌تر کردن بازی: زره و حرکت‌های تازه‌ی باس‌ها، متعادل کردن سلاح‌ها (D86)
- زره باس: `BossData.Armor` سهمی از هر ضربه را می‌گیرد، جز در پنجره‌های exposed (هر recovery بعد از حمله یا stun با EMP) که `ExposedArmor` = 0 است. جان باس‌ها 3400/8000/13500. `BossSystem.ArmorNow/Exposed/Targetable/InAir/OffMap`.
- `BossMove` و `BossData.Moves`: چرخه‌ی حمله از داده. Pounce (`BossAi.PounceWindup/Pouncing/PounceRecover`، زیر ۳۵٪ دو بار پشت‌سرهم) برای باس ۱ و ۳؛ Leap (`LeapRise/LeapAir/LeapLock/LeapRecover`) برای باس ۲ و ۳. در هوا باس هدف هیچ چیزی نیست (همه‌ی بررسی‌های `boss.Active && !boss.Dead` در Projectile/Melee/Dash/Hazard/Mine/Companion حالا `BossSystem.Targetable`) و با بدن‌ها برخورد ندارد. `BossView` بدن را بالا می‌برد، سایه می‌ماند و کوچک می‌شود؛ دایره‌ی فرود رسم می‌شود.
- رگبار باس ۲: `BurstFanCount/BurstFanSpacing/BurstSweeps/BurstTrackRate`؛ مخروط ۷۰ درجه، سه گلوله هر ۰.۱۵ ثانیه، رفت‌وبرگشت در ۱.۸ ثانیه، دنبال کردن ۴۰ درجه بر ثانیه، ۱۸ آسیب. مخروط هنگام شلیک هم رسم می‌شود.
- سلاح‌ها: SMG، تفنگ صنعتی و انرژی آسیب و خشاب کمتر و reload طولانی‌تر؛ `WeaponData.Bloom*` (پخش شدن با شلیک نگه‌داشته، برگشت بعد از مکث؛ `WeaponSystem.CurrentBloom`، `LoadoutState.Bloom/BloomTime`)؛ `ReticleView` با bloom باز می‌شود. تپانچه سطح ۲–۳ و شاتگان‌ها کمی قوی‌تر (falloff 0.6). revision 8 پروفایل ذخیره‌شده باس‌ها و سلاح‌ها را از baseline می‌گیرد.
- دو باگ قدیمی: save وسط reload از خشاب خالی بعد از load شروع reload را اشتباه می‌گرفت (`LoadoutState.ReloadByRound`)؛ `ArcMesh` meshهای نابودشده‌ی Play قبلی را در cache استاتیک نگه می‌داشت (Domain Reload خاموش) و مخروط/دایره‌ی هشدار باس بعد از اولین Play دیده نمی‌شد.
- تست‌ها: زره، leap (ترک نقشه، دنبال کردن، کوبیدن، فرار از دایره‌ی قفل)، pounce، رگبار پهن و دنبال‌کننده، EMP در هوا، bloom، save وسط reload. `FullRunTests` حالا محل اختلاف JSON را نشان می‌دهد. ۴۸۵ تست پاس. Play mode: leap، رگبار و pounce با اسکرین‌شات دیده شد (`docs/screenshots/Boss2_LeapTracking.png`، `Boss2_LeapLocked.png`، `Boss2_FanBurst.png`، `Boss1_Pounce.png`). با دست بازی نشده؛ اعداد باید با playtest تنظیم شوند.
- نکته‌ی تست Play mode: با `GameApp.Instance.ToolFreeze = true` و `session.Sim.Step(...)` در eval می‌شود شبیه‌سازی را دقیقاً تا یک حالت باس جلو برد و با `capture_game_view --source camera` عکس گرفت (حالت screen وقتی ادیتور فوکوس ندارد فریم قدیمی می‌دهد).

### 2026-10-18 — بدون بازسازی جان بازیکن، شکستن موانع با شارژ باس، سایه‌ی باس، نیروی کمکی باس (D87)
- جان بازیکن: کد بازسازی خودکار و فیلدهای `PlayerData.RegenerationDelay/Rate` حذف شد (seed، JSON). بازسازی اپراتور سر جایش است. تست `PlayerHealthNeverComesBackByItself`.
- شکستن موانع: `Sim/Arena/Obstacles.BreakAround` در هر تیک شارژ جلوی باس را بررسی می‌کند؛ کل بلوک متصل سلول‌های مسدود فاز (نه `BaseBlocked`: دیوار و ایستگاه) باز می‌شود، در `World.BrokenCells` ذخیره می‌شود (در `Bind` اعمال، در `EnterPhase` پاک، با retry برمی‌گردد) و `FlowField.Invalidate`. رویداد `ObstacleBroken`: تراشه، گرد و خاک، حلقه، shake و صدا. `ArenaAuthoring.SyncBroken` prop یا تایل‌های مانع شکسته را پنهان می‌کند و در صورت لزوم برمی‌گرداند. `BossData.ChargeBreaksObstacles`.
- سایه‌ی باس: `BossView` اندازه و جای سایه را از شعاع بدن می‌سازد (۲.۵ × ۰.۷۵ شعاع، ۰.۲ شعاع بالاتر از پا)؛ قبلاً بیضی ۵ × ۳.۷ واحدی دور پا بود.
- نیروی کمکی: `BossData.Reinforcements` و `ReinforceInterval` (۸ ثانیه)؛ `BossState.ReinforceQueue`، گروه `BossState.ReinforcementGroup` که XP و سکه نمی‌دهد (استخر پاداش ثابت می‌ماند). سقف‌ها 24/32/40، فاصله‌ی spawn کمتر. فرزندان producer حالا سقف باس را هم رعایت می‌کنند (باگ قدیمی). revision 9 داده‌ی باس‌ها را از baseline می‌گیرد.
- تست‌های تازه‌ی `BossPressureTests` (شکستن و ادامه‌ی شارژ، ذخیره، دیوار نمی‌شکند، retry برمی‌گرداند، نیروی کمکی بدون پاداش). `SlowKillPaysExactlyTheSamePool` حالا ضربه‌ی آخر را وسط leap نمی‌زند. ۴۹۱ تست پاس. Play mode: باس ۱ با شارژ سنگ را شکست و رد شد، سایه زیر پا دیده شد. با دست بازی نشده است.

