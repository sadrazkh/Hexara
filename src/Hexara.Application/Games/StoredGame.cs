using Hexara.Domain.Game;

namespace Hexara.Application.Games;

/// <summary>وضعیت یک بازی از دید ذخیره‌سازی.</summary>
public enum GameStatus
{
    /// <summary>اتاق ساخته شده ولی بازی شروع نشده — استفاده‌اش در فاز ۴ (لابی).</summary>
    Lobby = 0,

    Active = 1,

    Finished = 2,

    Abandoned = 3
}

/// <summary>
/// یک بازی به همراه شناسه‌ی ذخیره‌سازی و نشستن بازیکن‌ها.
///
/// <see cref="PlayerIds"/> به ترتیب صندلی است: اندیس بازیکن در دامنه دقیقاً همان
/// جای این فهرست است. تبدیل «کاربر» به «صندلی» تنها اینجا انجام می‌شود.
/// </summary>
public sealed class StoredGame
{
    public StoredGame(
        Guid id,
        GameStatus status,
        IReadOnlyList<Guid> playerIds,
        GameState state,
        DateTimeOffset updatedAt = default)
    {
        Id = id;
        Status = status;
        PlayerIds = playerIds;
        State = state;
        UpdatedAt = updatedAt;
    }

    /// <summary>آخرین باری که حرکتی روی این بازی ثبت شد — مبنای مهلت نوبت.</summary>
    public DateTimeOffset UpdatedAt { get; }

    public Guid Id { get; }

    public GameStatus Status { get; internal set; }

    public IReadOnlyList<Guid> PlayerIds { get; }

    public GameState State { get; }

    /// <summary>صندلی این کاربر، یا <c>null</c> اگر بازیکن این بازی نباشد.</summary>
    public int? SeatOf(Guid userId)
    {
        for (var i = 0; i < PlayerIds.Count; i++)
        {
            if (PlayerIds[i] == userId)
            {
                return i;
            }
        }

        return null;
    }

    public Guid? WinnerId => State.Winner is { } index ? PlayerIds[index] : null;
}

/// <summary>خلاصه‌ی یک بازی برای فهرست‌ها (لابی و پروفایل).</summary>
public sealed record GameSummary(
    Guid Id,
    GameStatus Status,
    int PlayerCount,
    int TurnNumber,
    Guid? WinnerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// یک سطر از تاریخچه‌ی بازی. <paramref name="Version"/> نسخه‌ی وضعیت بعد از این
/// حرکت است — مبنای رساندن اتفاق‌های ازدست‌رفته به بازیکنی که دوباره وصل می‌شود.
/// </summary>
public sealed record GameMoveLogEntry(
    int Sequence,
    long Version,
    int PlayerIndex,
    GameAction Action,
    IReadOnlyList<GameEvent> Events,
    DateTimeOffset CreatedAt);
