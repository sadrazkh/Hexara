namespace Hexara.Application.Common.Interfaces;

/// <summary>کارنامه‌ی یک بازیکن.</summary>
public sealed record PlayerStanding(
    Guid UserId,
    string DisplayName,
    string AvatarColor,
    bool IsGuest,
    int GamesPlayed,
    int GamesWon)
{
    /// <summary>درصد برد؛ برای کسی که هنوز بازی نکرده تهی است، نه صفر.</summary>
    public int? WinRate => GamesPlayed == 0 ? null : (int)Math.Round(100.0 * GamesWon / GamesPlayed);
}

/// <summary>
/// شمارش بردها و بازی‌ها. جدا از <see cref="IPlayerDirectory"/> نگه داشته شده چون
/// این یکی می‌نویسد و آن یکی فقط می‌خواند.
/// </summary>
public interface IPlayerStats
{
    /// <summary>
    /// پایان یک بازی: به همه یک بازی و به برنده‌ها یک برد اضافه می‌شود. در بازی
    /// تیمی همه‌ی اعضای تیم برنده حساب می‌شوند.
    /// </summary>
    Task RecordFinishAsync(
        IReadOnlyList<Guid> playerIds,
        IReadOnlyList<Guid> winnerIds,
        CancellationToken cancellationToken = default);

    Task<PlayerStanding?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlayerStanding>> LeaderboardAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
