using Hexara.Application.Common.Interfaces;
using Hexara.Infrastructure.Identity;
using Hexara.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexara.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// دیتابیس و هسته‌ی Identity را ثبت می‌کند. <see cref="IdentityBuilder"/> برگردانده
    /// می‌شود تا لایه‌ی وب بتواند اجزای وابسته به ASP.NET (مثل SignInManager) را
    /// اضافه کند بدون اینکه این پروژه به فریم‌ورک وب وابسته شود.
    /// </summary>
    public static IdentityBuilder AddHexaraInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("رشته اتصال 'Default' پیدا نشد.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IPlayerDirectory, PlayerDirectory>();
        services.AddScoped<IPlayerStats, PlayerStats>();

        return services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>();
    }
}
