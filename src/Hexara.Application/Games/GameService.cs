using Hexara.Application.Common.Interfaces;
using Hexara.Domain.Common;
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
/// <summary>
/// چه وقت بات جای یک بازیکن را بگیرد.
///
/// دو مهلت جدا لازم است: کسی که اتصالش قطع شده زود باید پوشش داده شود وگرنه بقیه
/// معطل می‌مانند، ولی کسی که حاضر است و دارد فکر می‌کند نباید با یک تایمر کوتاه
/// از بازی بیرون رانده شود.
/// </summary>
public sealed record AutoPlayPolicy(TimeSpan AbsentGrace, TimeSpan TurnDeadline)
{
    public static AutoPlayPolicy Default { get; } = new(TimeSpan.FromSeconds(25), TimeSpan.FromMinutes(3));

    public TimeSpan Shortest => AbsentGrace < TurnDeadline ? AbsentGrace : TurnDeadline;
}

public sealed class GameService
{
    private readonly IGameRepository _games;
    private readonly IClock _clock;
    private readonly IPlayerStats? _stats;

    public GameService(IGameRepository games, IClock clock, IPlayerStats? stats = null)
    {
        _games = games;
        _clock = clock;
        _stats = stats;
    }

    /// <summary>
    /// بازی تازه. اگر <paramref name="layout"/> داده شود همان برد سفارشی استفاده
    /// می‌شود؛ وگرنه برد از روی seed تولید می‌شود.
    /// </summary>
    public async Task<Guid> CreateAsync(
        GameOptions options,
        IReadOnlyList<Guid> playerIds,
        Domain.Board.BoardLayout? layout = null,
        CancellationToken cancellationToken = default)
    {
        var state = GameState.Create(options, playerIds, layout);
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

    public Task<IReadOnlyList<Guid>> ListIdleAsync(
        TimeSpan idleFor,
        int limit,
        CancellationToken cancellationToken = default) =>
        _games.ListIdleAsync(_clock.UtcNow - idleFor, limit, cancellationToken);

    /// <summary>
    /// اگر کسی که نوبتش است غایب یا از مهلت گذشته باشد، بات یک حرکت به جایش می‌زند.
    /// <c>null</c> یعنی کاری لازم نبود.
    ///
    /// انتخاب بات از روی نسخه‌ی وضعیت seed می‌گیرد، پس روی یک وضعیت مشخص همیشه
    /// همان حرکت درمی‌آید — که دیباگ کردنِ «چرا بات این را زد» را ممکن می‌کند.
    /// </summary>
    public async Task<MoveOutcome?> AutoPlayAsync(
        Guid gameId,
        IReadOnlySet<Guid> onlineUserIds,
        AutoPlayPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var game = await _games.LoadAsync(gameId, cancellationToken);
        if (game is null || game.Status != GameStatus.Active)
        {
            return null;
        }

        var idle = _clock.UtcNow - game.UpdatedAt;

        foreach (var seat in BotPlayer.SeatsToAct(game.State))
        {
            var absent = !onlineUserIds.Contains(game.PlayerIds[seat]);
            if (idle < (absent ? policy.AbsentGrace : policy.TurnDeadline))
            {
                continue;
            }

            if (BotPlayer.NextAction(game.State, seat, RngFor(game)) is not { } action)
            {
                continue;
            }

            var result = GameEngine.Apply(game.State, action);
            if (!result.Success)
            {
                // نباید پیش بیاید — تست‌های دود بازی‌های کامل را با همین بات می‌برند.
                return null;
            }

            var finished = game.State.Phase == TurnPhase.GameOver;
            if (finished)
            {
                game.Status = GameStatus.Finished;
            }

            if (!await _games.SaveMoveAsync(game, action, result.Events, cancellationToken))
            {
                return null;
            }

            await RecordFinishAsync(game, finished, cancellationToken);

            return new MoveOutcome(MoveStatus.Applied, GameError.None, result.Events, game.State.Version);
        }

        return null;
    }

    private static Rng RngFor(StoredGame game)
    {
        Span<byte> bytes = stackalloc byte[16];
        game.Id.TryWriteBytes(bytes);

        return new Rng(BitConverter.ToUInt64(bytes) ^ (ulong)game.State.Version);
    }

    /// <summary>
    /// اتفاق‌هایی که یک بازیکنِ قطع‌شده از دست داده، سانسورشده برای صندلی خودش.
    /// </summary>
    public async Task<IReadOnlyList<GameEvent>> EventsSinceAsync(
        Guid gameId,
        long sinceVersion,
        int? viewerSeat,
        CancellationToken cancellationToken = default)
    {
        var moves = await _games.HistorySinceAsync(gameId, sinceVersion, cancellationToken);

        return [.. moves.SelectMany(m => GameEventRedactor.ForSeat(m.Events, viewerSeat))];
    }

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

        var finished = game.State.Phase == TurnPhase.GameOver;
        if (finished)
        {
            game.Status = GameStatus.Finished;
        }

        if (!await _games.SaveMoveAsync(game, action, result.Events, cancellationToken))
        {
            return MoveOutcome.Fail(MoveStatus.Conflict);
        }

        await RecordFinishAsync(game, finished, cancellationToken);

        return new MoveOutcome(MoveStatus.Applied, GameError.None, result.Events, game.State.Version);
    }

    /// <summary>
    /// کارنامه فقط در همان لحظه‌ی تمام‌شدن به‌روز می‌شود. دوباره شمرده نمی‌شود چون
    /// بعد از آن نه موتور حرکتی می‌پذیرد و نه پوشش خودکار بازیِ تمام‌شده را برمی‌دارد.
    /// </summary>
    private async Task RecordFinishAsync(StoredGame game, bool finished, CancellationToken cancellationToken)
    {
        if (!finished || _stats is null)
        {
            return;
        }

        var winners = game.State.WinningSeats().Select(seat => game.PlayerIds[seat]).ToList();
        await _stats.RecordFinishAsync(game.PlayerIds, winners, cancellationToken);
    }
}
