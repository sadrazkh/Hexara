using System.Globalization;
using Hexara.Application;
using Hexara.Application.Common.Interfaces;
using Hexara.Infrastructure;
using Hexara.Infrastructure.Identity;
using Hexara.Infrastructure.Persistence;
using Hexara.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
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
});

builder.Services.AddAuthorization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = UiTranslator.SupportedCultures.Select(c => new CultureInfo(c)).ToList();
    options.DefaultRequestCulture = new RequestCulture(UiTranslator.DefaultCulture);
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
});

builder.Services.AddControllersWithViews();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")!, name: "postgres");

var app = builder.Build();

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
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRequestLocalization();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHealthChecks("/health");

await MigrateAsync(app);

app.Run();

static async Task MigrateAsync(WebApplication app)
{
    // مهاجرت خودکار فقط در توسعه؛ در تولید مهاجرت باید عمدی و کنترل‌شده اجرا شود.
    if (!app.Environment.IsDevelopment())
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "اجرای مهاجرت دیتابیس ناموفق بود. آیا Postgres بالا است؟");
    }
}
