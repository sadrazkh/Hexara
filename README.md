# هگزارا

بازی تخته‌ای استراتژیک سه‌بعدی و آنلاین — زمین بگیر، منابع جمع کن و اولین نفری باش
که به امتیاز پیروزی می‌رسد.

ASP.NET Core 10 · Vue 3 + Three.js · SignalR · PostgreSQL · دوزبانه (فارسی/انگلیسی)

## اجرا روی سیستم خودت

```bash
docker compose up -d db
```

```bash
dotnet ef database update --project src/Hexara.Infrastructure --startup-project src/Hexara.Web
```

```bash
cd src/Hexara.Web/ClientApp && npm ci && npm run build
```

```bash
dotnet run --project src/Hexara.Web
```

برای کار روی فرانت با HMR، `Vite:UseDevServer` را در `appsettings.Development.json`
روشن کن و در `ClientApp` دستور `npm run dev` را جدا اجرا کن.

## تست‌ها

```bash
dotnet test Hexara.slnx
```

```bash
cd src/Hexara.Web/ClientApp && npm test
```

سه پروژه‌ی تست دات‌نت و یک مجموعه‌ی تست کلاینت وجود دارد. دو چیز ارزش دانستن دارند:

- **`FullGameSmokeTests`** بازی‌های کامل ۲ تا ۶ نفره را با همان باتی که در تولید
  جای بازیکن غایب را می‌گیرد تا پیروزی جلو می‌برد و بعد از هر حرکت بررسی می‌کند که
  هیچ کارتی ساخته یا گم نشده باشد.
- **اثر انگشت شناسه‌های کانونی** در `CanonicalFingerprintTests.cs` و
  `ClientApp/src/three/hex.test.ts` یک مقدار مشترک است. اگر کانونی‌سازی گوشه و ضلع
  در یک طرف عوض شود هر دو قرمز می‌شوند — تنها چیزی که جلوی «کلیک روی گوشه‌ای که
  سرور جای دیگری می‌شناسد» را می‌گیرد.

## استقرار

ایمیج داکر هر دو نیمه را می‌سازد:

```bash
docker compose --profile full up -d --build
```

تنظیمات مخصوص تولید در بخش `Hardening` در `appsettings.json`. همه پیش‌فرضِ امن
دارند، پس نبودنشان یعنی رفتار امن نه رفتار باز:

| کلید | پیش‌فرض | توضیح |
|---|---|---|
| `BehindReverseProxy` | `false` | اعتماد به `X-Forwarded-*`. **فقط** وقتی روشنش کن که برنامه جز از راه پراکسی قابل دسترسی نباشد؛ وگرنه IP و پروتکل جعل‌شدنی‌اند و محدودیت نرخ دور می‌خورد. |
| `MigrateOnStartup` | `false` | مهاجرت هنگام بالا آمدن. خاموش است چون مهاجرت باید کارِ عمدیِ استقرار باشد نه عارضه‌ی ری‌استارت. |
| `AuthPermitsPerWindow` / `AuthWindowMinutes` | `10` / `5` | سقف ورود و ثبت‌نام از یک IP. |
| `ApiPermitsPerMinute` | `120` | سقف درخواست‌های JSON ویرایشگر برد برای هر کاربر. |

بخش `AutoPlay` هم مهلت‌های جایگزینی بازیکن با بات را تعیین می‌کند
(`AbsentGraceSeconds` برای کسی که قطع شده، `TurnDeadlineSeconds` برای کسی که
حاضر است ولی کاری نمی‌کند).

## ساختار

```
src/
  Hexara.Domain          قوانین بازی — بدون هیچ وابستگی بیرونی
  Hexara.Application     موارد کاربرد، قرارداد ذخیره‌سازی، نمای هر بازیکن
  Hexara.Infrastructure  EF Core، Postgres، Identity
  Hexara.Web             MVC، هاب SignalR، جزیره‌های Vue
```

- **[سیستم طراحی](docs/design-system.md)** — توکن‌ها، دو تم، و قواعدی که قبل از
  دست‌زدن به رابط کاربری باید خوانده شوند.
- وضعیت بازی به‌صورت یک سند JSON در ستون `jsonb` ذخیره می‌شود و کنارش یک جدول
  فقط-افزودنی از حرکت‌ها، که هم بازپخش را ممکن می‌کند و هم رساندن اتفاق‌های
  ازدست‌رفته به بازیکنی که دوباره وصل می‌شود.
- هیچ حرکتی سمت کلاینت اعتبارسنجی نمی‌شود و هیچ وضعیتی خام فرستاده نمی‌شود؛ برای
  هر صندلی نمای خودش ساخته و رویدادهای حاوی اطلاعات پنهان سانسور می‌شوند.

هگزارا یک پروژه‌ی مستقل است و به هیچ ناشر بازی تخته‌ای وابستگی ندارد.
