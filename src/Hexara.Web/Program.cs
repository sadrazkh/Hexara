using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hexara.Application;
using Hexara.Application.Common.Interfaces;
using Hexara.Application.Games;
using Hexara.Web.Realtime;
using Hexara.Infrastructure;
using Hexara.Infrastructure.Identity;
using Hexara.Infrastructure.Persistence;
using Hexara.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddHexaraApplication();
// اجزای وابسته به ASP.NET اینجا اضافه می‌شوند تا Infrastructure به فریم‌ورک وب وابسته نشود.
builder.Services.AddHexaraInfrastructure(builder.Configuration)
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<UiTranslator>();

builder.Services.Configure<ViteOptions>(builder.Configuration.GetSection("Vite"));
builder.Services.AddSingleton<ViteManifest>();

// AddIdentityCore کوکی احراز هویت را ثبت نمی‌کند؛ اینجا صریح اضافه می‌شود.
builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, AppUserClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.Cookie.Name = "hexara.auth";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.HttpOnly = true;

    // پشت پراکسی، درخواست به‌صورت HTTP می‌رسد و پیش‌فرض ‎SameAsRequest‎ کوکی را
    // بدون Secure می‌فرستد. جز در توسعه، همیشه Secure.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddAuthorization();

builder.Services.Configure<HardeningOptions>(builder.Configuration.GetSection(HardeningOptions.Section));
var hardening = builder.Configuration.GetSection(HardeningOptions.Section).Get<HardeningOptions>()
    ?? new HardeningOptions();

// محدودیت نرخ عمداً فقط روی نقطه‌های حساس است و نه سراسری: هاب SignalR اتصال
// بلندمدت دارد و یک محدودکننده‌ی سراسری وسط بازی قطعش می‌کرد.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // ورود و ثبت‌نام بر اساس IP بسته می‌شوند، چون هنوز کاربری در کار نیست.
    options.AddPolicy(RateLimitPolicies.Auth, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = hardening.AuthPermitsPerWindow,
                Window = TimeSpan.FromMinutes(hardening.AuthWindowMinutes),
                QueueLimit = 0
            }));

    // نقطه‌های JSON بر اساس کاربر، و اگر ناشناس بود بر اساس IP.
    options.AddPolicy(RateLimitPolicies.Api, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.IsAuthenticated == true
                ? context.User.Identity.Name ?? "user"
                : context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = hardening.ApiPermitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = UiTranslator.SupportedCultures.Select(c => new CultureInfo(c)).ToList();
    options.DefaultRequestCulture = new RequestCulture(UiTranslator.DefaultCulture);
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
});

builder.Services.AddControllersWithViews()
    // نقطه‌های JSON ویرایشگر باید enum را با نام بفرستند: کلاینت روی 'Forest'
    // حساب می‌کند نه روی عددی که با اضافه‌شدن یک عضو جابه‌جا می‌شود.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ویرایشگر برد با fetch کار می‌کند و توکن ضدجعل را در هدر می‌فرستد، نه در فرم.
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services.AddSingleton<GamePresence>();
builder.Services.AddSingleton<GameLocks>();
builder.Services.AddScoped<GameViewBuilder>();
builder.Services.AddScoped<GameBroadcaster>();
builder.Services.AddScoped<RoomBroadcaster>();

// چت در حافظه است و تکی، پس تنظیماتش هم همان‌جا خوانده و تزریق می‌شود.
builder.Services.AddSingleton(builder.Configuration.GetSection(ChatOptions.Section).Get<ChatOptions>()
    ?? new ChatOptions());
builder.Services.AddSingleton<GameChat>();

// صدا و تصویر. کلید و رمز از متغیر محیطی می‌آیند نه از فایلِ کامیت‌شده؛ نبودنشان
// یعنی خاموش، و بازی هیچ فرقی نمی‌کند.
builder.Services.AddSingleton(builder.Configuration.GetSection(LiveKitOptions.Section).Get<LiveKitOptions>()
    ?? new LiveKitOptions());
builder.Services.AddSingleton<LiveKitTokens>();

builder.Services.Configure<AutoPlayOptions>(builder.Configuration.GetSection(AutoPlayOptions.Section));
builder.Services.AddHostedService<AutoPlayService>();

// همان مهلت‌ها به نمای بازی هم می‌روند تا کلاینت بتواند شمارش معکوس نشان دهد.
// اگر پوشش خودکار خاموش باشد مهلت صفر می‌شود و کلاینت چیزی نشان نمی‌دهد.
builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<AutoPlayOptions>>().Value;
    return options.Enabled ? options.ToPolicy() : new AutoPlayPolicy(TimeSpan.Zero, TimeSpan.Zero);
});

// هاب همان قالب JSON بازی را می‌گیرد: بدون آن، چندریختی حرکت‌ها و شناسه‌های
// کانونی گوشه و ضلع روی سیم قابل خواندن نیستند.
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.TypeInfoResolver = GameJson.Options.TypeInfoResolver;

    foreach (var converter in GameJson.Options.Converters)
    {
        options.PayloadSerializerOptions.Converters.Add(converter);
    }

    // قرارداد سیم عمداً با قالب ذخیره‌سازی فرق دارد: کلاینت JavaScript است.
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")!, name: "postgres");

var app = builder.Build();

// باید پیش از هر چیزی بیاید که به پروتکل یا IP نگاه می‌کند — هدایت به HTTPS،
// کوکی Secure و محدودیت نرخ همه به همین وابسته‌اند.
if (hardening.BehindReverseProxy)
{
    var forwarded = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };

    // پراکسیِ CapRover آدرس ثابتی ندارد؛ فهرست پیش‌فرض باید خالی شود وگرنه
    // هدرها نادیده گرفته می‌شوند. امن بودنش به همان فرضِ گزینه بستگی دارد:
    // برنامه نباید جز از راه پراکسی قابل دسترسی باشد.
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();

    app.UseForwardedHeaders(forwarded);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();

var vite = app.Services.GetRequiredService<IOptions<ViteOptions>>().Value;
// ریشه‌ی سرور صدا و تصویر باید در CSP و Permissions-Policy باز شود، وگرنه
// ‎getUserMedia‎ پیش از رسیدن به هر کدی رد می‌شود.
var liveKit = app.Services.GetRequiredService<LiveKitOptions>();

app.UseHexaraSecurityHeaders(
    vite.UseDevServer,
    vite.DevServerUrl,
    liveKit.IsConfigured ? liveKit.Url : string.Empty);

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        // سرویس‌ورکر هرگز نباید کش شود، وگرنه نسخه‌ی تازه هیچ‌وقت جایگزین قبلی
        // نمی‌شود و کاربر برای همیشه با دارایی‌های کهنه می‌ماند.
        if (context.File.Name.Equals("sw.js", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        }
    }
});

app.UseRequestLocalization();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<GameHub>("/hubs/game");
app.MapHub<RoomHub>("/hubs/room");
app.MapHealthChecks("/health");

await MigrateAsync(app, hardening);

app.Run();

/// <summary>
/// مهاجرت دیتابیس.
///
/// در توسعه خودکار است و خطایش فقط لاگ می‌شود، تا بشود بدون Postgres هم برنامه را
/// بالا آورد. در تولید عمدی است و **با شکست، برنامه بالا نمی‌آید** — بالا آمدنِ
/// برنامه روی اسکیمای ناقص یعنی خطاهای عجیب در زمان اجرا به‌جای یک شکستِ صریح.
/// </summary>
static async Task MigrateAsync(WebApplication app, HardeningOptions hardening)
{
    var development = app.Environment.IsDevelopment();
    if (!development && !hardening.MigrateOnStartup)
    {
        app.Logger.LogInformation(
            "مهاجرت خودکار خاموش است. اسکیما را با «dotnet ef database update» به‌روز کن.");
        return;
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex) when (development)
    {
        app.Logger.LogError(ex, "اجرای مهاجرت دیتابیس ناموفق بود. آیا Postgres بالا است؟");
    }
}

/// <summary>
/// برنامه با دستورهای سطح بالا نوشته شده و کلاس ‎Program‎ خودکار ساخته می‌شود.
/// این اعلانِ جزئی فقط آن را عمومی می‌کند تا ‎WebApplicationFactory‎ در تست‌ها
/// بتواند همین برنامه — با همان میان‌افزارها — را بالا بیاورد.
/// </summary>
public partial class Program;
