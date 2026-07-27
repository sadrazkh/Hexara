using Hexara.Application.Common.Interfaces;
using Hexara.Application.Games;
using Hexara.Domain.Game;
using Hexara.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hexara.Infrastructure.Persistence;

/// <summary>
/// ذخیره‌سازی بازی روی Postgres: یک سطر برای بازی با عکس JSON وضعیت، یک سطر برای
/// هر صندلی، و یک سطر فقط-افزودنی برای هر حرکت.
/// </summary>
public sealed class GameRepository : IGameRepository
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public GameRepository(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Guid> CreateAsync(
        GameState state,
        IReadOnlyList<Guid> playerIds,
        GameStatus status,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow.UtcDateTime;
        var record = new GameRecord
        {
            Id = Guid.NewGuid(),
            Status = status,
            PlayerCount = playerIds.Count,
            TurnNumber = state.TurnNumber,
            Snapshot = GameJson.Serialize(state.ToSnapshot()),
            Version = state.Version,
            MoveCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Players = [.. playerIds.Select((id, seat) => new GamePlayerRecord { UserId = id, Seat = seat })]
        };

        _db.Games.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        return record.Id;
    }

    public async Task<StoredGame?> LoadAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var record = await _db.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (record is null)
        {
            return null;
        }

        var playerIds = record.Players.OrderBy(p => p.Seat).Select(p => p.UserId).ToList();
        var state = GameState.Restore(GameJson.Deserialize<GameSnapshot>(record.Snapshot));

        return new StoredGame(record.Id, record.Status, playerIds, state);
    }

    public async Task<bool> SaveMoveAsync(
        StoredGame game,
        GameAction action,
        IReadOnlyList<GameEvent> events,
        CancellationToken cancellationToken = default)
    {
        // در همین scope قبلاً بارگذاری شده، پس ردیابی‌شده است و مقدار اصلیِ Version
        // برای بررسی هم‌زمانی دست EF است.
        var record = await _db.Games.FindAsync([game.Id], cancellationToken);
        if (record is null)
        {
            return false;
        }

        var now = _clock.UtcNow.UtcDateTime;

        // به‌روزرسانی بازی و افزودن حرکت در دو مرحله‌ی جدا انجام می‌شود تا داورِ
        // هم‌زمانی فقط و فقط توکن Version باشد. اگر هر دو در یک دسته می‌رفتند، ممکن
        // بود اول یکتایی (GameId, Sequence) بشکند و خطای دیگری بالا بیاید.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        record.Snapshot = GameJson.Serialize(game.State.ToSnapshot());
        record.Version = game.State.Version;
        record.Status = game.Status;
        record.TurnNumber = game.State.TurnNumber;
        record.WinnerId = game.WinnerId;
        record.UpdatedAt = now;
        record.MoveCount++;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // بازی جای دیگری جلو رفته است؛ فراخوان باید دوباره بخواند و تصمیم بگیرد.
            return false;
        }

        _db.GameMoves.Add(new GameMoveRecord
        {
            GameId = game.Id,
            Sequence = record.MoveCount,
            Version = game.State.Version,
            PlayerIndex = action.PlayerIndex,
            Action = GameJson.Serialize(action),
            Events = GameJson.Serialize(events),
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<GameSummary>> ListForPlayerAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Games
            .AsNoTracking()
            .Where(g => g.Players.Any(p => p.UserId == userId))
            .OrderByDescending(g => g.UpdatedAt)
            .Select(g => new
            {
                g.Id,
                g.Status,
                g.PlayerCount,
                g.TurnNumber,
                g.WinnerId,
                g.CreatedAt,
                g.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new GameSummary(
            r.Id,
            r.Status,
            r.PlayerCount,
            r.TurnNumber,
            r.WinnerId,
            Utc(r.CreatedAt),
            Utc(r.UpdatedAt)))];
    }

    public Task<IReadOnlyList<GameMoveLogEntry>> HistoryAsync(
        Guid gameId,
        CancellationToken cancellationToken = default) =>
        HistorySinceAsync(gameId, long.MinValue, cancellationToken);

    public async Task<IReadOnlyList<GameMoveLogEntry>> HistorySinceAsync(
        Guid gameId,
        long sinceVersion,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.GameMoves
            .AsNoTracking()
            .Where(m => m.GameId == gameId && m.Version > sinceVersion)
            .OrderBy(m => m.Sequence)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(m => new GameMoveLogEntry(
            m.Sequence,
            m.Version,
            m.PlayerIndex,
            GameJson.Deserialize<GameAction>(m.Action),
            GameJson.Deserialize<IReadOnlyList<GameEvent>>(m.Events),
            Utc(m.CreatedAt)))];
    }

    /// <summary>زمان‌های ستون‌ها همیشه UTCاند؛ اینجا دوباره برچسب می‌گیرند.</summary>
    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
