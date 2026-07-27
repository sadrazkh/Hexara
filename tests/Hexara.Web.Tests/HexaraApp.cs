using Hexara.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hexara.Web.Tests;

/// <summary>
/// برنامه‌ی واقعی، با همان لوله‌ی میان‌افزارها، بالا آمده روی یک سرور تستی.
///
/// این تنها راهی است که می‌شود هدرهای امنیتی، nonce و محدودیت نرخ را واقعاً
/// سنجید: هیچ‌کدامشان در کدِ قابلِ فراخوانی نیستند، فقط در رفتار لوله‌اند.
///
/// دیتابیس به SQLite در حافظه عوض می‌شود و سرویس پس‌زمینه برداشته می‌شود؛ بقیه‌ی
/// برنامه دست‌نخورده می‌ماند تا آنچه تست می‌شود همان چیزی باشد که اجرا می‌شود.
/// </summary>
public sealed class HexaraApp : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Filename=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // فقط برداشتنِ ‎DbContextOptions‎ کافی نیست: ‎AddDbContext‎ چند ثبتِ
            // دیگر هم می‌کند و اگر یکی بماند، هر دو provider روی یک options
            // می‌نشینند و EF شکایت می‌کند که دو تا است. به‌جای حدس‌زدنِ نام‌ها،
            // هرچه به دیتابیس مربوط است برداشته می‌شود.
            foreach (var descriptor in services.Where(IsDatabaseWiring).ToList())
            {
                services.Remove(descriptor);
            }

            _connection.Open();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            // پوشش خودکار هر چند ثانیه به دیتابیس سر می‌زند و در تست فقط سروصداست.
            services.RemoveAll<IHostedService>();
        });
    }

    private static bool IsDatabaseWiring(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(AppDbContext)
        || descriptor.ServiceType.FullName?.Contains("DbContextOptions", StringComparison.Ordinal) == true
        || IsNpgsql(descriptor.ServiceType)
        || IsNpgsql(descriptor.ImplementationType);

    private static bool IsNpgsql(Type? type) =>
        type?.Assembly.GetName().Name?.StartsWith("Npgsql", StringComparison.Ordinal) == true;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // بعد از ساخت میزبان، وگرنه باید یک ظرف سرویس دومِ موازی ساخته می‌شد.
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

        return host;
    }

    /// <summary>کلاینتی که ریدایرکت را دنبال نمی‌کند — برای دیدن خودِ پاسخ.</summary>
    public HttpClient NewClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
