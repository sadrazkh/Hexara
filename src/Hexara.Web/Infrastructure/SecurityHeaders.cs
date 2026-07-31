using System.Buffers.Text;
using System.Security.Cryptography;

namespace Hexara.Web.Infrastructure;

/// <summary>
/// هدرهای امنیتی و سیاست محتوا.
///
/// CSP با nonce نوشته شده و نه با <c>unsafe-inline</c> برای اسکریپت: تنها اسکریپت
/// درون‌خطی صفحه، همان تکه‌ی کوچکِ اعمالِ تم در <c>head</c> است و همان یکی nonce
/// می‌گیرد. اگر روزی اسکریپت درون‌خطی دیگری اضافه شود، بدون nonce اجرا نمی‌شود —
/// که دقیقاً همان چیزی است که می‌خواهیم.
/// </summary>
public static class SecurityHeaders
{
    private const string NonceKey = "hexara:csp-nonce";

    /// <summary>nonce همین درخواست؛ ویو از این استفاده می‌کند.</summary>
    public static string CspNonce(this HttpContext context) =>
        context.Items.TryGetValue(NonceKey, out var value) && value is string nonce ? nonce : string.Empty;

    /// <param name="voiceOrigin">
    /// ریشه‌ی سرور صدا و تصویر، یا رشته‌ی خالی اگر خاموش است.
    ///
    /// دسترسیِ میکروفون و دوربین **فقط** وقتی باز می‌شود که واقعاً سروری در کار
    /// باشد؛ در حالت عادی هر دو بسته می‌مانند.
    /// </param>
    public static IApplicationBuilder UseHexaraSecurityHeaders(
        this IApplicationBuilder app,
        bool allowViteDevServer,
        string devServerUrl,
        string voiceOrigin = "")
    {
        return app.Use(async (context, next) =>
        {
            // base64url و نه base64 معمولی: کدگذار HTML رِیزر نویسه‌ی ‎+‎ را به
            // ‎&#x2B;‎ تبدیل می‌کند، پس مقدارِ داخل صفحه دیگر با هدر یکی نبود.
            // مرورگر آن را رمزگشایی می‌کند و عملاً کار می‌کرد، ولی تکیه بر این
            // رفتار شکننده است. الفبای base64url هیچ نویسه‌ی نیازمند کدگذاری ندارد.
            var nonce = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
            context.Items[NonceKey] = nonce;

            var headers = context.Response.Headers;
            headers["Content-Security-Policy"] =
                BuildPolicy(nonce, allowViteDevServer, devServerUrl, voiceOrigin);
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // همراهِ قدیمیِ frame-ancestors برای مرورگرهایی که CSP سطح ۲ ندارند.
            headers["X-Frame-Options"] = "DENY";

            // بقیه‌ی این‌ها در بازی لازم نیستند و بسته می‌مانند. میکروفون و دوربین
            // فقط وقتی به خودِ سایت باز می‌شوند که صدا و تصویر پیکربندی شده باشد —
            // وگرنه ‎getUserMedia‎ همین‌جا و پیش از هر کدی رد می‌شود.
            var media = voiceOrigin.Length > 0 ? "camera=(self), microphone=(self)" : "camera=(), microphone=()";

            headers["Permissions-Policy"] =
                $"accelerometer=(), {media}, geolocation=(), gyroscope=(), payment=(), usb=()";

            // صفحه‌ها هرگز کش نمی‌شوند.
            //
            // بی این، پاسخِ ‎HTML‎ هیچ ‎Cache-Control‎ی نداشت و مرورگر اجازه داشت
            // با حدسِ خودش نگهش دارد. نتیجه‌اش این بود: صفحه‌ی کهنه از کشِ مرورگر
            // می‌آمد، نامِ دارایی‌های قدیمی را داشت، و آن دارایی‌ها هم در کشِ
            // سرویس‌ورکر بودند — پس کلِ برنامه یک نسخه عقب بالا می‌آمد و فقط با
            // رفرشِ دستی درست می‌شد.
            //
            // روی ‎Content-Type‎ سنجیده می‌شود نه روی مسیر، و در ‎OnStarting‎ تا
            // بعد از تصمیمِ فایل‌های ایستا اجرا شود — وگرنه ‎immutable‎ دارایی‌های
            // هش‌دار را پاک می‌کرد.
            context.Response.OnStarting(static state =>
            {
                var response = (HttpResponse)state;

                if (response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
                {
                    response.Headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            }, context.Response);

            await next();
        });
    }

    private static string BuildPolicy(
        string nonce,
        bool allowViteDevServer,
        string devServerUrl,
        string voiceOrigin)
    {
        // در حالت dev، دارایی‌ها و سوکت HMR از سرور ویت می‌آیند و باید مجاز شوند.
        var dev = allowViteDevServer ? $" {devServerUrl}" : string.Empty;
        var devSocket = allowViteDevServer ? $" {devServerUrl.Replace("http", "ws")}" : string.Empty;

        var voice = voiceOrigin.Length > 0
            ? $" {voiceOrigin} {voiceOrigin.Replace("wss://", "https://").Replace("ws://", "http://")}"
            : string.Empty;

        return string.Join(
            "; ",
            "default-src 'self'",
            $"script-src 'self' 'nonce-{nonce}'{dev}",

            // سبک‌های درون‌خطی لازم‌اند: رنگ آواتار و نمونه‌رنگ‌های ویرایشگر با
            // ویژگی style و متغیر CSS ست می‌شوند و nonce به ویژگی تعلق نمی‌گیرد.
            $"style-src 'self' 'unsafe-inline'{dev}",
            $"img-src 'self' data: blob:{dev}",
            $"font-src 'self'{dev}",

            // SignalR روی همین ریشه وب‌سوکت می‌زند و 'self' آن را می‌پوشاند.
            // سرور صدا و تصویر جای دیگری است، پس ریشه‌اش جدا اضافه می‌شود — هم
            // ‎wss‎ برای سیگنالینگ و هم ‎https‎ برای بقیه‌ی درخواست‌هایش.
            $"connect-src 'self'{dev}{devSocket}{voice}",

            // جریانِ صدا و تصویرِ طرف مقابل با ‎srcObject‎ وصل می‌شود، ولی بعضی
            // مسیرهای بازگشتی هنوز از ‎blob:‎ استفاده می‌کنند.
            "media-src 'self' blob:",
            "worker-src 'self'",
            "manifest-src 'self'",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'");
    }
}
