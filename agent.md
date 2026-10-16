# agent.md — حافظه‌ی پروژه برای ایجنت

> **قانون اصلی (اجباری):**
> 1. در شروع هر کار جدید، **اول همین فایل را بخوان**. هدف این است که لازم نباشد کل پروژه دوباره خوانده شود.
> 2. **پس از اتمام هر تسک**، کارهای انجام‌شده را در بخش «تاریخچه‌ی کارها» اضافه کن و بخش‌های «وضعیت فعلی» و «ساختار پروژه» را هم اگر تغییر کرده‌اند به‌روز کن. این فایل باید **همیشه به‌روز** باشد.
> 3. اگر چیزی در این فایل با واقعیت پروژه نمی‌خواند، پروژه را مرجع بدان و این فایل را اصلاح کن.

---

## خلاصه‌ی پروژه

- **نام:** Game Craft Final
- **نوع:** بازی دوبعدی (2D) با Unity
- **نسخه‌ی Unity:** `6000.6.0f1` (Unity 6)
- **رندر پایپ‌لاین:** URP با Renderer2D (`Assets/Settings/UniversalRP.asset`، `Assets/Settings/Renderer2D.asset`)
- **ورودی:** Input System جدید (`Assets/Settings/InputSystem_Actions.inputactions`)
- **کنترل نسخه:** git، شاخه‌ی `main`
- **مرحله:** پروژه تازه از تمپلیت 2D یونیتی ساخته شده و هنوز کد گیم‌پلی ندارد.
- **بازی:** «Hollow» — شوتر دید از بالا با نشانه‌گیری دستی، سه فاز (حیوانات، ربات‌ها، انسان‌ها)، ۵ موج + ۱ باس در هر فاز. مرجع طراحی: docs/GDD.md (تبدیل‌شده از Hollow_Game_Design_EN.docx نسخه‌ی 0.12).

## ساختار پروژه

| مسیر | توضیح |
|---|---|
| `Assets/Scenes/SampleScene.unity` | تنها صحنه؛ در Build Settings فعال است |
| `Assets/Settings/` | تنظیمات URP، Renderer2D، Volume Profile، Input Actions، Scene Template |
| `Assets/Welcome/` | فایل‌های صفحه‌ی خوش‌آمد تمپلیت (`Welcome2DScript.cs` و asmdef) — مربوط به گیم‌پلی نیست |
| `Packages/manifest.json` | لیست پکیج‌ها |
| `ProjectSettings/` | تنظیمات پروژه |
| `docs/GDD.md` | سند طراحی بازی (منبع حقیقت) |
| `docs/QUESTIONS.md` | سؤال‌های باز با پیش‌فرض پیشنهادی (★ = مسدودکننده) و یافته‌های بررسی |
| `docs/DECISIONS.md` | لاگ تصمیم‌ها با منبع هر تصمیم |
| `docs/USER_STORIES.md` | پیش‌نویس user story برای تسک‌های T00 تا T22 (تأییدنشده) |

### پکیج‌های مهم
2D Animation، Aseprite Importer، PSD Importer، Sprite Shape، Tilemap + Tilemap Extras، Input System، URP 17.6، Timeline، uGUI، Visual Scripting، Test Framework، AI Assistant / AI Inference.

## قراردادها (Conventions)

- اسکریپت‌های گیم‌پلی جدید را در `Assets/Scripts/` قرار بده (هنوز ساخته نشده).
- اسکریپت‌های Editor در `Assets/Editor/`.
- فایل‌های `Library/`، `Temp/`، `Logs/`، `UserSettings/` و `*.csproj` تولیدی هستند؛ آن‌ها را نخوان و ویرایش نکن.

## وضعیت فعلی

- هیچ سیستم گیم‌پلی‌ای پیاده‌سازی نشده است.
- اسکریپت `Assets/Editor/HubForceResolve.cs` حذف شده و پوشه‌ی `Assets/Editor/` فعلاً وجود ندارد.
- تصاویر (`*.png` و غیره) طبق `.gitattributes` با Git LFS ذخیره می‌شوند.
- آخرین commit‌ها: `485e18c`، `c439e31`. پوشه‌ی `docs/` و تغییر `agent.md` هنوز commit نشده‌اند.
- **منتظر جواب کاربر به `docs/QUESTIONS.md`** — طبق دستور کاربر تا جواب نیامده هیچ فایلی خارج از `docs/` ساخته یا تغییر داده نمی‌شود.

## کارهای باز / ایده‌ها

- گرفتن جواب سؤال‌ها و تأیید user storyها، سپس نوشتن `docs/TASKS.md` و `docs/DECISIONS.md` و شروع T00.

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
