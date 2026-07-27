using Hexara.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Hexara.Web.Controllers;

/// <summary>
/// چیزهایی که یک PWA لازم دارد.
///
/// manifest از کنترلر می‌آید نه از یک فایل ثابت، چون نام و توضیح برنامه باید به
/// زبان کاربر باشد و همان فایل‌های ترجمه‌ی مشترک را استفاده کند.
/// </summary>
public class PwaController : Controller
{
    private readonly UiTranslator _t;

    public PwaController(UiTranslator translator) => _t = translator;

    // کش فقط سمت خودِ مرورگر: محتوای manifest به زبانِ انتخابیِ کاربر بستگی دارد
    // و کشِ مشترک، نسخه‌ی یک زبان را به کاربر زبان دیگر می‌داد.
    [HttpGet("manifest.webmanifest")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public IActionResult Manifest()
    {
        var culture = UiTranslator.CurrentCulture();

        return new JsonResult(new
        {
            id = "/",
            name = _t["app.name"],
            short_name = _t["app.name"],
            description = _t["app.description"],
            lang = culture,
            dir = UiTranslator.IsRtl(culture) ? "rtl" : "ltr",
            start_url = "/",
            scope = "/",
            display = "standalone",

            // پس‌زمینه‌ی صفحه‌ی راه‌اندازی عمداً تیره‌ی تم است، نه سفید؛ وگرنه
            // باز کردن برنامه با یک فلاش سفید شروع می‌شود.
            background_color = "#0f0c08",
            theme_color = "#0f0c08",
            orientation = "any",
            categories = new[] { "games", "entertainment" },
            icons = new object[]
            {
                new { src = "/icons/icon-192.png", sizes = "192x192", type = "image/png", purpose = "any" },
                new { src = "/icons/icon-512.png", sizes = "512x512", type = "image/png", purpose = "any" },
                new { src = "/icons/maskable-192.png", sizes = "192x192", type = "image/png", purpose = "maskable" },
                new { src = "/icons/maskable-512.png", sizes = "512x512", type = "image/png", purpose = "maskable" }
            },
            shortcuts = new object[]
            {
                new
                {
                    name = _t["nav.lobby"],
                    url = "/Lobby",
                    icons = new object[] { new { src = "/icons/icon-192.png", sizes = "192x192" } }
                }
            }
        })
        {
            ContentType = "application/manifest+json"
        };
    }

    /// <summary>صفحه‌ای که سرویس‌ورکر وقتی شبکه نیست نشان می‌دهد.</summary>
    [HttpGet("offline")]
    public IActionResult Offline() => View();
}
