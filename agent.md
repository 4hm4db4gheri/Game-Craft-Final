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

## ساختار پروژه

| مسیر | توضیح |
|---|---|
| `Assets/Scenes/SampleScene.unity` | تنها صحنه؛ در Build Settings فعال است |
| `Assets/Settings/` | تنظیمات URP، Renderer2D، Volume Profile، Input Actions، Scene Template |
| `Assets/Welcome/` | فایل‌های صفحه‌ی خوش‌آمد تمپلیت (`Welcome2DScript.cs` و asmdef) — مربوط به گیم‌پلی نیست |
| `Packages/manifest.json` | لیست پکیج‌ها |
| `ProjectSettings/` | تنظیمات پروژه |

### پکیج‌های مهم
2D Animation، Aseprite Importer، PSD Importer، Sprite Shape، Tilemap + Tilemap Extras، Input System، URP 17.6، Timeline، uGUI، Visual Scripting، Test Framework، AI Assistant / AI Inference.

## قراردادها (Conventions)

- اسکریپت‌های گیم‌پلی جدید را در `Assets/Scripts/` قرار بده (هنوز ساخته نشده).
- اسکریپت‌های Editor در `Assets/Editor/`.
- فایل‌های `Library/`، `Temp/`، `Logs/`، `UserSettings/` و `*.csproj` تولیدی هستند؛ آن‌ها را نخوان و ویرایش نکن.

## وضعیت فعلی

- هیچ سیستم گیم‌پلی‌ای پیاده‌سازی نشده است.
- تغییرات commit‌نشده در شروع کار (از قبل وجود داشت): حذف `Assets/Editor/HubForceResolve.cs`، تغییر در چند فایل ProjectSettings و پوشه‌ی جدید `ProjectSettings/Packages/com.unity.ai.assistant/`.

## کارهای باز / ایده‌ها

- (خالی)

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
