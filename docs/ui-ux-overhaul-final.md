# گزارش نهایی بازطراحی UI/UX و دارایی‌های Hexara

تاریخ: ۲۰۲۶-۰۷-۳۰

## نتیجه

فازهای B تا G روی نسخهٔ فعلی پروژه اجرا شدند. صفحهٔ بازی اکنون یک میز بازی دیجیتال
با هویت مستقل Hexara دارد: نقشهٔ سه‌بعدیِ بافت‌دار در مرکز، اطلاعات بازیکنان در
ستون کناری، پنل‌های عملیاتی اولویت‌بندی‌شده، نوار وضعیت فشرده و تجربهٔ موبایل مبتنی
بر bottom-sheet سه‌حالته. منطق دامنه، قرارداد `BoardData`، SignalR، ترتیب نوبت،
معامله، تولید برد و نتیجهٔ تاس تغییر نکرده‌اند.

## فاز B — پایهٔ طراحی و رجیستری

- رجیستری مرکزی دارایی‌ها با `TERRAIN_ART` و انتخاب variation قطعی توسعه یافت.
- انتخاب terrain با hash مختصات axial انجام می‌شود؛ refresh ظاهر خانه را تصادفی
  عوض نمی‌کند.
- یک بلوک نهایی و مشخص در انتهای `app.css` اضافه شد تا مسیر rollback روشن باشد.
- سلسله‌مراتب بصری، قاب طلایی، سطح‌های شیشه‌ای، سایه، وضعیت فعال/غیرفعال و هدف‌های
  لمسی در تم روشن و تیره هماهنگ شدند.

## فاز C — تولید دارایی

- ۶ تصویر terrain اصیل با ImageGen داخلی Codex تولید شد.
- برای هر خانواده ۳ variation مستقل و بهینه؛ مجموعاً ۱۸ WebP ۵۱۲×۵۱۲.
- ۵ تصویر کارت منبع portrait برای چوب، آجر، گندم، پشم و سنگ معدن تولید و در قاب
  چندزبانه‌ی رابط ادغام شد.
- تصاویر زمین هیچ Number Token، بندر، ساختمان، راه یا متن bakeشده ندارند.
- prompt، منابع و مسیر خروجی در
  `design-references/asset-generation-prompts.md` ثبت شده است.

## فاز D — ادغام برد

- بافت terrain فقط روی سطح بالایی CylinderGeometry قرار می‌گیرد و بدنه/لبهٔ هر
  hex مستقل باقی می‌ماند.
- لبهٔ طلایی مستقل، Number Tokenهای CanvasTexture، مهره‌ها و markerهای placement
  حفظ شده‌اند.
- بندرهای مخروطی با نشان شش‌ضلعی مستقل و برچسب خوانای `3:1` یا `2:1` جایگزین شدند.
- فاصلهٔ دوربین بر اساس نسبت قاب fit می‌شود و کنترل «بازنشانی نمای نقشه» اضافه شد.
- تعداد اشیای scenery کاهش یافت تا بافت‌ها شلوغ نشوند و draw cost کنترل شود.

## فاز E — پنل‌ها و واکنش‌گرایی

- دسکتاپ عریض: ستون بازیکنان، برد مرکزی و rail عملیات در یک نمای سه‌ستونه.
- ترتیب RTL دسکتاپ مطابق مرجع تثبیت شد: بازیکنان/رویدادها در چپ، برد در مرکز و
  دست/معامله در راست؛ میز فرمان کارت‌ها، تاس و بانک نیز زیر برد اضافه شد.
- دسکتاپ/لپ‌تاپ: برد اولویت اصلی را نگه می‌دارد و rail در کنار آن اسکرول داخلی دارد.
- موبایل: نوار پایین پنج‌گزینه‌ای و sheet با حالت‌های بسته، نیمه‌باز و تمام‌صفحه.
- لمس مجدد tab فعال، sheet را بین حالت‌ها جابه‌جا می‌کند؛ grabber هم قابل‌دسترسی است.
- دست بازیکن اسکرول افقی دارد؛ در landscape برد و rail کنار هم قرار می‌گیرند.
- overflow هدر ۳۹۰ پیکسل رفع و safe-area برای دستگاه‌های بریدگی‌دار حفظ شد.
- فارسی RTL و انگلیسی LTR هر دو بدون fork کردن ساختار DOM پشتیبانی می‌شوند.

## فاز F — عملکرد و دسترس‌پذیری

- تصاویر terrain از PNG ۱۲۵۴ به WebP ۵۱۲ و quality 80 کاهش یافتند؛ حجم کل حدود
  ۱٫۵ مگابایت است.
- textureها cache، mipmap و anisotropy محدود دارند و هنگام dispose آزاد می‌شوند.
- رندر WebGL در tab مخفی pause و پس از بازگشت resume می‌شود.
- روی دستگاه ضعیف، DPR، antialias و shadow طبق مسیر قبلی کاهش می‌یابند.
- تصویرهای DOM lazy-load می‌شوند و fallback خطای بارگذاری دارند.
- کنترل‌ها label/heading/expanded state و focus-visible دارند؛ فقط رنگ حامل معنی نیست.
- `prefers-reduced-motion` انیمیشن تاس و transition sheet را محدود می‌کند.

## فاز G — QA

ماتریس بررسی‌شده:

- ۱۹۲۰×۱۰۸۰ فارسی/تیره
- ۱۳۶۶×۷۶۸ انگلیسی/روشن
- ۸۲۰×۱۱۸۰ تبلت
- ۳۹۰×۸۴۴ فارسی/تیره و انگلیسی/روشن
- ۸۴۴×۳۹۰ landscape
- sheet بسته، نیمه‌باز و تمام‌صفحه
- سناریوی واقعی دو بازیکن و شروع بازی

تصاویر نهایی در `design-references/final` قرار دارند. build فرانت‌اند، تست‌های
Vitest، build دات‌نت و suiteهای دات‌نت باید در آخرین اجرای تحویل بدون شکست باشند؛
عدد دقیق آن‌ها در بخش «نتیجهٔ تست نهایی» این سند تکمیل می‌شود.

## فایل‌های اصلی تغییرکرده

- `src/Hexara.Web/ClientApp/src/assets/registry.ts`
- `src/Hexara.Web/ClientApp/src/assets/Asset.vue`
- `src/Hexara.Web/ClientApp/src/islands/GameLive.vue`
- `src/Hexara.Web/ClientApp/src/islands/GameBoard.vue`
- `src/Hexara.Web/ClientApp/src/islands/BuildPanel.vue`
- `src/Hexara.Web/ClientApp/src/islands/Card.vue`
- `src/Hexara.Web/ClientApp/src/three/board.ts`
- `src/Hexara.Web/ClientApp/src/three/scenery.ts`
- `src/Hexara.Web/ClientApp/src/styles/app.css`
- `src/Hexara.Web/Locales/fa.json`
- `src/Hexara.Web/Locales/en.json`

## rollback

این بازطراحی قرارداد backend یا migration ندارد. برای بازگشت دیداری:

1. بلوک مشخص «میز بازی Hexara — پوستهٔ نهایی» در انتهای `app.css` حذف شود.
2. تغییرات `GameLive.vue`، `GameBoard.vue`، `BuildPanel.vue` و `Card.vue` برگردد.
3. ادغام texture/port در `three/board.ts` و تراکم جدید `three/scenery.ts` برگردد.
4. importهای `generated/terrain` و `TERRAIN_ART` از رجیستری حذف شوند.
5. پوشهٔ `assets/generated` بعد از حذف همهٔ referenceها قابل حذف است.

به‌دلیل ثابت‌ماندن همهٔ DTOها و actionها، rollback به تغییر دیتابیس، پاک‌سازی state
یا دست‌کاری اتاق‌های موجود نیاز ندارد.

## نتیجهٔ تست نهایی

- `npm run build`: موفق؛ ۱۲۳ ماژول، ۵۰ فایل precache. تنها هشدار باقی‌مانده chunk
  اشتراکی Three.js با اندازهٔ ۵۱۳٫۲۴ کیلوبایت است.
- `npm test -- --run`: هر ۸۶ تست در ۶ فایل موفق.
- `dotnet build Hexara.slnx -c Release --no-restore`: موفق، صفر warning و صفر error.
- `Hexara.Domain.Tests`: تعداد ۲۳۹ تست موفق.
- `Hexara.Application.Tests`: تعداد ۱۸۶ تست موفق.
- `Hexara.Web.Tests`: تعداد ۱۰۵ تست موفق.
- مجموع تست‌های دات‌نت: ۵۳۰ تست موفق، بدون شکست و skip.
- QA مرورگر: سناریوی واقعی دو بازیکن، هر دو زبان، هر دو تم، چهار نسبت اصلی و سه
  حالت sheet بدون خطای دیداریِ مسدودکننده بررسی شد.
