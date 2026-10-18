# چیدمان صنعتی فاز دوم — ۲۰۲۶-۱۰-۱۸

هشت آرت ارسالی در `Assets/Resources/ArenaArt/Landmarks/ENV_P2_*` قرار دارند. نقشهٔ فاز دوم در `Assets/Resources/Arena/ArenaLayout.json` جای پایه، اندازه و برش آلفای هر کدام را نگه می‌دارد. `ArenaAuthoring` از همین اطلاعات Sprite، ترتیب عمق و `BoxCollider2D` می‌سازد؛ شبیه‌سازی نیز همان خانه‌های پایه را برای حرکت، دش و پرتابه مسدود می‌کند. سقف و بازوهای بلند، مانع زمینی نیستند و هنگام عبور بازیکن از پشتشان محو می‌شوند.

| سازه | خانهٔ شروع | پایه | عرض نمایشی |
|---|---:|---:|---:|
| HaltedLoadingArm | 3,24 | 6×3 | 6.6 |
| BrokenRepairLift | 35,24 | 6×3 | 6.4 |
| CollapsedPrefabStorage | 4,5 | 6×3 | 6.8 |
| CoolingUnit | 37,5 | 5×3 | 5.8 |
| FilterSludgeStation | 7,17 | 5×3 | 5.8 |
| MoldingPress | 29,19 | 5×3 | 5.9 |
| RecyclingFurnace | 10,12 | 5×3 | 5.8 |
| RobotChargingStation | 29,6 | 6×2 | 6.4 |

`FloorFactory.png` طرح پیوستهٔ مسیرهای وسیع روی آسفالت و بتن فرسوده است. `DetailFactory.png` بافت ریزِ سنگ‌دانه و ترک را در مقیاس نزدیک اضافه می‌کند. این دو فقط در فاز دوم مصرف می‌شوند؛ زمین فاز سوم همان `FloorIndustry` است. تصویر کف دوبعدی و تخت می‌ماند تا برخورد شبیه‌سازی تغییر نکند؛ ظاهر سه‌ربع از سطح افقی، پایهٔ درست سازه‌ها و ترتیب عمق حاصل می‌شود.

برای تست در Unity، Play را متوقف و پس از import/compile دوباره اجرا کنید. با F10 به فاز دوم بروید و دور هر سازه حرکت و دش کنید. باید مسیر پهن میان آن‌ها باز باشد، از پایه رد نشوید و از پشت بخش‌های بلند با محوشدن ملایم عبور کنید. در Hierarchy مسیر `Arena > Props_P2` باید هشت نام تازه و `BoxCollider2D` آن‌ها را نشان دهد. اگر بافت قدیمی دیده شد، در حالت Edit منوی `Hollow > Art > Reimport Arena Artwork` را اجرا کنید. چیدمان در داده با اتصال همهٔ ۱۲۲۶ خانهٔ آزاد، نبود گذرگاه یک‌خانه‌ای و بازبودن حریم شروع/فروشگاه/ورود دشمن بررسی شد؛ ظاهر نهایی در Play Mode هنوز بازبینی نشده است.

آرت زمینه با ابزار داخلی imagegen ساخته شد. فایل‌های منبع تولیدشده در `C:/Users/User/.codex/generated_images/01a14a7d-5d9c-7130-9270-5d7fe13723c7` باقی‌اند؛ نسخه‌های مصرفی در پروژه‌اند. تصویر اول ویرایش `FloorIndustry` بود: «Full-arena 3:2 overhead industrial yard, retain broad open lanes, worn asphalt and soot-dark concrete, inset slabs and sparse rails, muted steel grey/dusty brass, fine scale, no machines, obstacles or perspective distortion». تصویر دوم مستقل بود: «Square subtle repeatable close-detail material swatch, aged robot-factory concrete/asphalt, tiny cracks and aggregate, uniform brightness, no paths, symbols, vegetation or objects». هر دو بدون شفافیت تولید شدند. هشت سازه همان فایل‌های ارسالی کاربر هستند و پیکسل‌هایشان تغییر نکرده است.
