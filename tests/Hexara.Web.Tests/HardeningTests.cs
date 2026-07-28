using System.Net;
using System.Text.RegularExpressions;

namespace Hexara.Web.Tests;

/// <summary>
/// سخت‌سازی تولید. این‌ها فقط با بالا آوردن برنامه قابل سنجش‌اند — نه هدر
/// امنیتی تابعی است که بشود صدایش زد، نه محدودیت نرخ.
/// </summary>
public class HardeningTests : IClassFixture<HexaraApp>
{
    private readonly HexaraApp _app;

    public HardeningTests(HexaraApp app) => _app = app;

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(" ", values) : string.Empty;

    // ── هدرهای امنیتی ────────────────────────────────────────────────────

    [Fact]
    public async Task The_home_page_comes_back_with_the_security_headers()
    {
        using var client = _app.NewClient();
        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header(response, "Referrer-Policy"));
        Assert.Contains("camera=()", Header(response, "Permissions-Policy"));
    }

    [Fact]
    public async Task The_content_policy_locks_down_the_dangerous_directives()
    {
        using var client = _app.NewClient();
        using var response = await client.GetAsync("/");

        var csp = Header(response, "Content-Security-Policy");

        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("base-uri 'self'", csp);
        Assert.Contains("form-action 'self'", csp);
    }

    /// <summary>
    /// مهم‌ترین خاصیت CSP اینجا: اسکریپت درون‌خطی فقط با nonce اجرا می‌شود. اگر
    /// روزی کسی ‎unsafe-inline‎ اضافه کند، تمام محافظت در برابر XSS از بین می‌رود.
    /// </summary>
    [Fact]
    public async Task Inline_scripts_need_a_nonce_not_unsafe_inline()
    {
        using var client = _app.NewClient();
        using var response = await client.GetAsync("/");

        var csp = Header(response, "Content-Security-Policy");
        var scriptSrc = csp.Split("; ").Single(part => part.StartsWith("script-src", StringComparison.Ordinal));

        Assert.DoesNotContain("unsafe-inline", scriptSrc);
        Assert.DoesNotContain("unsafe-eval", scriptSrc);
        // الفبای base64url — بدون ‎+‎ و ‎/‎ و ‎=‎ که کدگذار HTML دستکاریشان کند.
        Assert.Matches("'nonce-[A-Za-z0-9_-]{20,}'", scriptSrc);
    }

    /// <summary>nonce تکراری یعنی nonce نیست؛ هر درخواست باید مقدار تازه بگیرد.</summary>
    [Fact]
    public async Task Every_request_gets_a_fresh_nonce()
    {
        using var client = _app.NewClient();

        var nonces = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            using var response = await client.GetAsync("/");
            nonces.Add(NonceOf(Header(response, "Content-Security-Policy")));
        }

        Assert.Equal(3, nonces.Distinct().Count());
    }

    /// <summary>
    /// nonceِ هدر باید همانی باشد که در صفحه نشسته، وگرنه اسکریپت تمِ درون‌خطی
    /// اجرا نمی‌شود و صفحه با تم غلط پلک می‌زند.
    /// </summary>
    [Fact]
    public async Task The_page_carries_the_same_nonce_as_the_header()
    {
        using var client = _app.NewClient();
        using var response = await client.GetAsync("/");

        var nonce = NonceOf(Header(response, "Content-Security-Policy"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains($"<script nonce=\"{nonce}\">", html);
    }

    [Fact]
    public async Task No_page_ships_an_inline_event_handler()
    {
        using var client = _app.NewClient();

        foreach (var path in new[] { "/", "/offline", "/Account/Login", "/Leaderboard" })
        {
            using var response = await client.GetAsync(path);
            var html = await response.Content.ReadAsStringAsync();

            // CSP این‌ها را اجرا نمی‌کند، پس وجودشان یعنی یک دکمه‌ی مرده.
            Assert.DoesNotContain("onclick=", html, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// نشان باید رنگش را از توکن‌های تم بگیرد، نه از یک کد رنگ ثابت. رنگ‌های
    /// فیروزه‌ای/آبیِ نشانِ قبل از بازطراحی تم نباید جایی باقی مانده باشد.
    /// </summary>
    [Fact]
    public async Task The_brand_mark_takes_its_colour_from_the_theme()
    {
        using var client = _app.NewClient();
        using var response = await client.GetAsync("/");

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("class=\"hx-brand__from\"", html);
        Assert.Contains("class=\"hx-brand__to\"", html);
        Assert.DoesNotContain("5ee7c6", html, StringComparison.OrdinalIgnoreCase);
    }

    // ── PWA ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_manifest_is_served_with_the_right_type_and_icons()
    {
        using var client = _app.NewClient();
        using var response = await client.GetAsync("/manifest.webmanifest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/manifest+json", response.Content.Headers.ContentType?.MediaType);

        using var manifest = System.Text.Json.JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = manifest.RootElement;
        Assert.Equal("standalone", root.GetProperty("display").GetString());
        Assert.Equal("/", root.GetProperty("start_url").GetString());
        Assert.Equal(4, root.GetProperty("icons").GetArrayLength());

        // بدون آیکون maskable، اندروید خودش نشان را داخل یک مربع سفید می‌گذارد.
        Assert.Contains(
            root.GetProperty("icons").EnumerateArray(),
            icon => icon.GetProperty("purpose").GetString() == "maskable");
    }

    [Fact]
    public async Task The_offline_page_stands_on_its_own()
    {
        using var client = _app.NewClient();
        using var response = await client.GetAsync("/offline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── محدودیت نرخ ──────────────────────────────────────────────────────

    /// <summary>
    /// بدون این، حدس‌زدن رمز فقط به پهنای باند محدود است. سقف پیش‌فرض ۱۰ در
    /// ۵ دقیقه است، پس درخواست یازدهم باید ۴۲۹ بگیرد.
    /// </summary>
    [Fact]
    public async Task Hammering_the_login_gets_you_throttled()
    {
        using var app = new HexaraApp();
        using var client = app.NewClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++)
        {
            using var response = await client.PostAsync(
                "/Account/Login",
                new FormUrlEncodedContent([new KeyValuePair<string, string>("Email", "a@b.co")]));

            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses.Take(5));
    }

    /// <summary>صفحه‌های عادی نباید محدود شوند — وگرنه یک بازی معمولی قطع می‌شود.</summary>
    [Fact]
    public async Task Ordinary_pages_are_not_throttled()
    {
        using var app = new HexaraApp();
        using var client = app.NewClient();

        for (var i = 0; i < 30; i++)
        {
            using var response = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static string NonceOf(string csp) =>
        Regex.Match(csp, "'nonce-([^']+)'").Groups[1].Value;
}
