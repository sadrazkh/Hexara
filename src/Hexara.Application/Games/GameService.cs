using Hexara.Application.Common.Interfaces;
using Hexara.Domain.Game;

namespace Hexara.Application.Games;

/// <summary>سرنوشت یک درخواست حرکت.</summary>
public enum MoveStatus
{
    /// <summary>حرکت انجام و ذخیره شد.</summary>
    Applied = 0,

    GameNotFound = 1,

    /// <summary>درخواست‌دهنده بازیکن این بازی نیست یا صندلی دیگری را ادعا کرده است.</summary>
    NotYourSeat = 2,

    /// <summary>قوانین بازی حرکت را رد کردند؛ دلیلش در <c>Error</c> است.</summary>
    Rejected = 3,

    /// <summary>بازی هم‌زمان جای دیگری جلو رفته بود؛ باید دوباره خوانده و تلاش شود.</summary>
    Conflict = 4
}

public sealed record MoveOutcome(
    MoveStatus Status,
    GameError Error,
    IReadOnlyList<GameEvent> Events,
    long Version)
{
    public bool Success => Status == MoveStatus.Applied;

    public static MoveOutcome Fail(MoveStatus status) => new(status, GameError.None, [], 0);

    public static MoveOutcome Rejected(GameError error) => new(MoveStatus.Rejected, error, [], 0);
}

/// <summary>
/// نقطه‌ی ورود لایه‌های بالاتر به بازی. کنترلر (فاز ۴) و هاب (فاز ۵) فقط با این
/// کلاس حرف می‌زنند و هرگز مستقیم <c>GameEngine</c> را صدا نمی‌زنند.
/// </summary>
public sealed class GameService
{
    private readonly IGameRepository _games;

    public GameService(IGameRepository games) => _games = games;

    public async Task<Guid> CreateAsync(
        GameOptions options,
        IReadOnlyList<Guid> playerIds,
        CancellationToken cancellationToken = default)
    {
        var state = GameState.Create(options, playerIds);
        return await _games.CreateAsync(state, playerIds, GameStatus.Active, cancellationToken);
    }

    public Task<StoredGame?> GetAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        _games.LoadAsync(gameId, cancellationToken);

    public Task<IReadOnlyList<GameSummary>> ListForPlayerAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _games.ListForPlayerAsync(userId, cancellationToken);

    public Task<IReadOnlyList<GameMoveLogEntry>> HistoryAsync(
        Guid gameId,
        CancellationToken cancellationToken = default) =>
        _games.HistoryAsync(gameId, cancellationToken);

    /// <summary>
    /// یک حرکت را از طرف یک کاربر اجرا می‌کند.
    ///
    /// صندلی از روی کاربر تعیین می‌شود و با آنچه در حرکت آمده مقایسه می‌شود؛ کلاینت
    /// دستکاری‌شده نمی‌تواند به جای بازیکن دیگری بازی کند.
    /// </summary>
    public async Task<MoveOutcome> PlayAsync(
        Guid gameId,
        Guid userId,
        GameAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var game = await _games.LoadAsync(gameId, cancellationToken);
        if (game is null)
        {
            return MoveOutcome.Fail(MoveStatus.GameNotFound);
        }

        if (game.SeatOf(userId) is not { } seat || seat != action.PlayerIndex)
        {
            return MoveOutcome.Fail(MoveStatus.NotYourSeat);
        }

        var result = GameEngine.Apply(game.State, action);
        if (!result.Success)
        {
            return MoveOutcome.Rejected(result.Error);
        }

        if (game.State.Phase == TurnPhase.GameOver)
        {
            game.Status = GameStatus.Finished;
        }

        if (!await _games.SaveMoveAsync(game, action, result.Events, cancellationToken))
        {
            return MoveOutcome.Fail(MoveStatus.Conflict);
        }

        return new MoveOutcome(MoveStatus.Applied, GameError.None, result.Events, game.State.Version);
    }
}
