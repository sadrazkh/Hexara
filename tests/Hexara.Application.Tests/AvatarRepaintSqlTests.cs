using Hexara.Application.Players;
using Hexara.Infrastructure.Identity;
using Hexara.Infrastructure.Persistence.Migrations.Data;
using Microsoft.EntityFrameworkCore;

namespace Hexara.Application.Tests;

/// <summary>
/// خودِ SQL مهاجرت، واقعاً اجرا شده روی یک دیتابیس.
///
/// این تست‌ها بدنه‌ی مهاجرت را می‌سنجند نه فقط نگاشت را: نام جدول، نام ستون، و
/// این‌که ‎lower()‎ درست جا افتاده باشد. اجرا روی SQLite است و مهاجرت واقعی روی
/// Postgres می‌رود، ولی هر دو همین ساختِ استاندارد را می‌فهمند و اشتباهِ تایپی
/// در نام جدول یا ستون همان‌جا لو می‌رود.
/// </summary>
public class AvatarRepaintSqlTests
{
    private static async Task<AppUser> SeedAsync(SqliteFixture fixture, string color)
    {
        await using var db = fixture.NewContext();

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"u-{Guid.NewGuid():N}",
            DisplayName = "Someone",
            AvatarColor = color,
            CreatedAt = fixture.Clock.UtcNow,
            LastSeenAt = fixture.Clock.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    private static async Task RunAsync(SqliteFixture fixture, bool forward)
    {
        await using var db = fixture.NewContext();

        foreach (var statement in AvatarRepaint.Statements(forward))
        {
            await db.Database.ExecuteSqlRawAsync(statement);
        }
    }

    private static async Task<string> ColorOfAsync(SqliteFixture fixture, Guid id)
    {
        await using var db = fixture.NewContext();
        return (await db.Users.AsNoTracking().SingleAsync(u => u.Id == id)).AvatarColor;
    }

    [Fact]
    public async Task Running_it_repaints_every_old_colour()
    {
        using var fixture = new SqliteFixture();

        var users = new List<(Guid Id, string Old)>();
        foreach (var old in AvatarRepaint.Old)
        {
            users.Add(((await SeedAsync(fixture, old)).Id, old));
        }

        await RunAsync(fixture, forward: true);

        foreach (var (id, old) in users)
        {
            var now = await ColorOfAsync(fixture, id);

            Assert.Equal(AvatarRepaint.OldToNew[old], now);
            Assert.Contains(now, AvatarPalette.Colors);
        }
    }

    /// <summary>رنگی که ذخیره‌شده‌اش حرف بزرگ دارد هم باید گرفته شود.</summary>
    [Fact]
    public async Task Uppercase_stored_values_are_caught_too()
    {
        using var fixture = new SqliteFixture();
        var user = await SeedAsync(fixture, "#4F9CF9");

        await RunAsync(fixture, forward: true);

        Assert.Equal(AvatarPalette.Default, await ColorOfAsync(fixture, user.Id));
    }

    /// <summary>رنگ ناشناخته دست‌نخورده می‌ماند — مهاجرت انتخاب کاربر را پاک نمی‌کند.</summary>
    [Fact]
    public async Task An_unknown_colour_is_left_alone()
    {
        using var fixture = new SqliteFixture();
        var user = await SeedAsync(fixture, "#123456");

        await RunAsync(fixture, forward: true);

        Assert.Equal("#123456", await ColorOfAsync(fixture, user.Id));
    }

    /// <summary>
    /// دو بار اجرا شدن نباید چیزی را خراب کند. اهمیتش این است که رنگ‌های تازه
    /// هرگز کلیدِ نگاشتِ رفت نباشند، وگرنه اجرای دوم آن‌ها را دوباره جابه‌جا می‌کرد.
    /// </summary>
    [Fact]
    public async Task Running_it_twice_changes_nothing_more()
    {
        using var fixture = new SqliteFixture();
        var user = await SeedAsync(fixture, "#3fbf7f");

        await RunAsync(fixture, forward: true);
        var once = await ColorOfAsync(fixture, user.Id);

        await RunAsync(fixture, forward: true);

        Assert.Equal(once, await ColorOfAsync(fixture, user.Id));
    }

    /// <summary>برگشت باید کاربر را دقیقاً به رنگ قبلی‌اش برگرداند.</summary>
    [Fact]
    public async Task The_down_migration_puts_the_old_colour_back()
    {
        using var fixture = new SqliteFixture();
        var user = await SeedAsync(fixture, "#a06cd5");

        await RunAsync(fixture, forward: true);
        Assert.Equal("#bfa0d8", await ColorOfAsync(fixture, user.Id));

        await RunAsync(fixture, forward: false);
        Assert.Equal("#a06cd5", await ColorOfAsync(fixture, user.Id));
    }

    [Fact]
    public async Task Users_who_share_a_colour_all_move_together()
    {
        using var fixture = new SqliteFixture();

        var first = await SeedAsync(fixture, "#e0533d");
        var second = await SeedAsync(fixture, "#e0533d");

        await RunAsync(fixture, forward: true);

        Assert.Equal("#e0906b", await ColorOfAsync(fixture, first.Id));
        Assert.Equal("#e0906b", await ColorOfAsync(fixture, second.Id));
    }
}
