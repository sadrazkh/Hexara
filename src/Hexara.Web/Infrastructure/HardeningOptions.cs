namespace Hexara.Web.Infrastructure;

/// <summary>
/// تنظیمات سخت‌سازی تولید. همه‌شان پیش‌فرضِ امن دارند، پس نبودنِ بخش در
/// ‎appsettings‎ یعنی رفتار امن، نه رفتار باز.
/// </summary>
public sealed class HardeningOptions
{
    public const string Section = "Hardening";

    /// <summary>
    /// آیا برنامه پشت یک پراکسی معکوس (مثل CapRover) اجرا می‌شود؟
    ///
    /// روشن‌کردنش یعنی به هدرهای ‎X-Forwarded-*‎ اعتماد می‌کنیم. **فقط** وقتی
    /// روشنش کن که برنامه از بیرون جز از راه همان پراکسی قابل دسترسی نباشد؛
    /// وگرنه هر کسی می‌تواند IP و پروتکل را جعل کند و محدودیت نرخ را دور بزند.
    /// </summary>
    public bool BehindReverseProxy { get; set; }

    /// <summary>
    /// اجرای مهاجرت دیتابیس هنگام بالا آمدن.
    ///
    /// پیش‌فرض خاموش است: در تولید، مهاجرت باید کارِ عمدیِ استقرار باشد نه
    /// عارضه‌ی جانبیِ ری‌استارت — دو نمونه که هم‌زمان بالا می‌آیند نباید هم‌زمان
    /// اسکیمای یکسانی را جلو ببرند.
    /// </summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>سقف درخواست‌های ورود و ثبت‌نام از یک IP در هر پنجره.</summary>
    public int AuthPermitsPerWindow { get; set; } = 10;

    public int AuthWindowMinutes { get; set; } = 5;

    /// <summary>سقف درخواست‌های JSON ویرایشگر برد برای هر کاربر در دقیقه.</summary>
    public int ApiPermitsPerMinute { get; set; } = 120;
}
