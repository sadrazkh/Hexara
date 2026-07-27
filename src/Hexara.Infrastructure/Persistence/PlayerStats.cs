using Hexara.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Hexara.Infrastructure.Persistence;

/// <summary>
/// شمارنده‌های برد و باخت روی خودِ کاربر.
///
/// با <c>ExecuteUpdate</c> نوشته می‌شوند نه با خواندن و نوشتن، تا دو بازی که
/// هم‌زمان تمام می‌شوند شمارش هم را خراب نکنند.
/// </summary>
public sealed class PlayerStats : IPlayerStats
{
    private readonly AppDbContext _db;

    public PlayerStats(AppDbContext db) => _db = db;

    public async Task RecordFinishAsync(
        IReadOnlyList<Guid> playerIds,
        IReadOnlyList<Guid> winnerIds,
        CancellationToken cancellationToken = default)
    {
        if (playerIds.Count == 0)
        {
            return;
        }

        var everyone = playerIds.Distinct().ToList();
        var winners = winnerIds.Distinct().ToList();

        await _db.Users
            .Where(u => everyone.Contains(u.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.GamesPlayed, u => u.GamesPlayed + 1), cancellationToken);

        if (winners.Count > 0)
        {
            await _db.Users
                .Where(u => winners.Contains(u.Id))
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.GamesWon, u => u.GamesWon + 1), cancellationToken);
        }
    }

    public async Task<PlayerStanding?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new PlayerStanding(
                u.Id, u.DisplayName, u.AvatarColor, u.IsGuest, u.GamesPlayed, u.GamesWon))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// رتبه‌بندی بر اساس برد، و در تساوی کسی که کمتر بازی کرده جلوتر است — یعنی
    /// نسبت برد بهتر. کسی که هیچ بازی‌ای نکرده اصلاً نمی‌آید.
    /// </summary>
    public async Task<IReadOnlyList<PlayerStanding>> LeaderboardAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.GamesPlayed > 0)
            .OrderByDescending(u => u.GamesWon)
            .ThenBy(u => u.GamesPlayed)
            .ThenBy(u => u.DisplayName)
            .Take(limit)
            .Select(u => new PlayerStanding(
                u.Id, u.DisplayName, u.AvatarColor, u.IsGuest, u.GamesPlayed, u.GamesWon))
            .ToListAsync(cancellationToken);
}
