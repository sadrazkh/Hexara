using Hexara.Application.Common.Interfaces;
using Hexara.Infrastructure.Identity;
using Hexara.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hexara.Application.Tests;

/// <summary>
/// یک دیتابیس SQLite در حافظه برای هر تست.
///
/// عمداً از InMemory provider استفاده نمی‌کنیم: آن provider کلید یکتا، تراکنش و
/// بررسی هم‌زمانی را جدی نمی‌گیرد و همان چیزهایی که اینجا مهم‌اند تست نمی‌شوند.
/// ستون jsonb هم فقط وقتی Postgres باشد اعمال می‌شود، پس نگاشت روی SQLite کار می‌کند.
/// </summary>
internal sealed class SqliteFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteFixture()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    public FakeClock Clock { get; } = new();

    public AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>یک مخزن با DbContext تازه — هر مخزن نقش یک درخواست وب را بازی می‌کند.</summary>
    public GameRepository NewRepository(out AppDbContext context)
    {
        context = NewContext();
        return new GameRepository(context, Clock);
    }

    /// <summary>کاربران باید واقعاً وجود داشته باشند، چون صندلی‌ها کلید خارجی دارند.</summary>
    public async Task<List<Guid>> SeedUsersAsync(int count)
    {
        await using var db = NewContext();
        var ids = new List<Guid>();

        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            db.Users.Add(new AppUser
            {
                Id = id,
                UserName = $"player-{i}-{id:N}",
                NormalizedUserName = $"PLAYER-{i}-{id:N}".ToUpperInvariant(),
                DisplayName = $"Player {i}",
                CreatedAt = Clock.UtcNow,
                LastSeenAt = Clock.UtcNow
            });
        }

        await db.SaveChangesAsync();
        return ids;
    }

    public void Dispose() => _connection.Dispose();
}

internal sealed class FakeClock : IClock
{
    private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
