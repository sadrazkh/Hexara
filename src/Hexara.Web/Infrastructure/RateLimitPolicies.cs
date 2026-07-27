namespace Hexara.Web.Infrastructure;

/// <summary>نام سیاست‌های محدودیت نرخ — تا رشته‌ی جادویی در کنترلرها پخش نشود.</summary>
public static class RateLimitPolicies
{
    /// <summary>ورود، ثبت‌نام و ورود مهمان — جلوگیری از حدس‌زدن رمز.</summary>
    public const string Auth = "auth";

    /// <summary>نقطه‌های JSON که کلاینت پشت سر هم صدایشان می‌زند.</summary>
    public const string Api = "api";
}
