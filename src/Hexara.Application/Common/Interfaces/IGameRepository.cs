using Hexara.Application.Games;
using Hexara.Domain.Game;

namespace Hexara.Application.Common.Interfaces;

/// <summary>
/// ذخیره و بازیابی بازی‌ها. پیاده‌سازی در لایه‌ی Infrastructure است تا Application
/// به EF و Postgres وابسته نشود.
/// </summary>
public interface IGameRepository
{
    Task<Guid> CreateAsync(
        GameState state,
        IReadOnlyList<Guid> playerIds,
        GameStatus status,
        CancellationToken cancellationToken = default);

    Task<StoredGame?> LoadAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// وضعیت جدید و حرکت انجام‌شده را ذخیره می‌کند. اگر همین بازی هم‌زمان جای دیگری
    /// جلو رفته باشد <c>false</c> برمی‌گرداند و چیزی نوشته نمی‌شود.
    /// </summary>
    Task<bool> SaveMoveAsync(
        StoredGame game,
        GameAction action,
        IReadOnlyList<GameEvent> events,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameSummary>> ListForPlayerAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// بازی‌های در جریانی که از زمان داده‌شده جلوتر نرفته‌اند — یعنی کسی معطلشان کرده.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListIdleAsync(
        DateTimeOffset idleSince,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameMoveLogEntry>> HistoryAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>حرکت‌هایی که وضعیت را از نسخه‌ی داده‌شده جلوتر برده‌اند.</summary>
    Task<IReadOnlyList<GameMoveLogEntry>> HistorySinceAsync(
        Guid gameId,
        long sinceVersion,
        CancellationToken cancellationToken = default);
}
