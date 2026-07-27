using System.Security.Claims;
using Hexara.Application.Games;
using Hexara.Domain.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Hexara.Web.Realtime;

/// <summary>پاسخ پیوستن به بازی: نمای وضعیت یا دلیل رد شدن.</summary>
public sealed record JoinResult(bool Success, string? Error, GameView? View);

/// <summary>اتفاق‌هایی که یک بازیکنِ برگشته از دست داده بود.</summary>
public sealed record CatchUpResult(long Version, IReadOnlyList<GameEvent> Events, GameView View);

/// <summary>
/// کانال زنده‌ی بازی.
///
/// دو اصل که همه‌جای این کلاس رعایت شده‌اند:
/// ۱. هیچ حرکتی اینجا اعتبارسنجی نمی‌شود؛ همه به <see cref="GameService"/> می‌روند.
/// ۲. هیچ وضعیتی خام فرستاده نمی‌شود؛ برای هر صندلی نمای خودش ساخته می‌شود.
/// </summary>
[Authorize]
public sealed class GameHub : Hub
{
    private readonly GameService _games;
    private readonly GameViewBuilder _views;
    private readonly GameBroadcaster _broadcaster;
    private readonly GamePresence _presence;
    private readonly GameLocks _locks;

    public GameHub(
        GameService games,
        GameViewBuilder views,
        GameBroadcaster broadcaster,
        GamePresence presence,
        GameLocks locks)
    {
        _games = games;
        _views = views;
        _broadcaster = broadcaster;
        _presence = presence;
        _locks = locks;
    }

    private Guid? UserId =>
        Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static string GroupOf(Guid gameId) => $"game:{gameId}";

    public async Task<JoinResult> Join(Guid gameId)
    {
        if (UserId is not { } userId)
        {
            return new JoinResult(false, "notAuthenticated", null);
        }

        var game = await _games.GetAsync(gameId, Context.ConnectionAborted);
        if (game is null)
        {
            return new JoinResult(false, "gameNotFound", null);
        }

        if (game.SeatOf(userId) is not { } seat)
        {
            // تماشاچی در این فاز پشتیبانی نمی‌شود.
            return new JoinResult(false, "notYourGame", null);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(gameId), Context.ConnectionAborted);

        if (_presence.Add(gameId, Context.ConnectionId, userId))
        {
            await Clients.OthersInGroup(GroupOf(gameId)).SendAsync("presence", userId, true, Context.ConnectionAborted);
        }

        var view = await _views.BuildAsync(game, seat, _presence.OnlineIn(gameId), Context.ConnectionAborted);
        return new JoinResult(true, null, view);
    }

    public async Task Leave(Guid gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupOf(gameId), Context.ConnectionAborted);
        await AnnounceOfflineAsync(gameId, _presence.Remove(gameId, Context.ConnectionId));
    }

    /// <summary>
    /// اجرای یک حرکت. حرکت‌های یک بازی پشت سر هم اجرا می‌شوند تا چند نفر که هم‌زمان
    /// کارت دور می‌ریزند به برخورد نسخه نخورند.
    /// </summary>
    public async Task<MoveOutcome> Play(Guid gameId, GameAction action)
    {
        if (UserId is not { } userId)
        {
            return MoveOutcome.Fail(MoveStatus.NotYourSeat);
        }

        var outcome = await _locks.RunAsync(
            gameId,
            () => _games.PlayAsync(gameId, userId, action, Context.ConnectionAborted),
            Context.ConnectionAborted);

        if (!outcome.Success)
        {
            return outcome;
        }

        await BroadcastAsync(gameId, outcome.Events);

        // سرویس قبلاً تأیید کرده که این صندلی مال همین کاربر است. سانسور برای اسرار
        // خودِ بازیکن بی‌اثر است، ولی اگر روزی حرکتی راز دیگری تولید کند، اینجا هم بسته است.
        return outcome with { Events = GameEventRedactor.ForSeat(outcome.Events, action.PlayerIndex) };
    }

    /// <summary>
    /// بعد از قطعی و وصل دوباره: هرچه از دست رفته را می‌گیرد و وضعیت تازه را هم.
    /// </summary>
    public async Task<CatchUpResult?> CatchUp(Guid gameId, long sinceVersion)
    {
        if (UserId is not { } userId)
        {
            return null;
        }

        var game = await _games.GetAsync(gameId, Context.ConnectionAborted);
        if (game is null || game.SeatOf(userId) is not { } seat)
        {
            return null;
        }

        var missed = await _games.EventsSinceAsync(gameId, sinceVersion, seat, Context.ConnectionAborted);
        var view = await _views.BuildAsync(game, seat, _presence.OnlineIn(gameId), Context.ConnectionAborted);

        return new CatchUpResult(game.State.Version, missed, view);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // سوکت ممکن است بی‌خبر بمیرد، پس همه‌ی بازی‌های این اتصال پاک می‌شوند.
        foreach (var gameId in _presence.GamesOf(Context.ConnectionId))
        {
            await AnnounceOfflineAsync(gameId, _presence.Remove(gameId, Context.ConnectionId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task BroadcastAsync(Guid gameId, IReadOnlyList<GameEvent> events)
    {
        var game = await _games.GetAsync(gameId, Context.ConnectionAborted);
        if (game is not null)
        {
            await _broadcaster.SendAsync(game, events, Context.ConnectionAborted);
        }
    }

    private async Task AnnounceOfflineAsync(Guid gameId, Guid? wentOffline)
    {
        if (wentOffline is { } userId)
        {
            await Clients.Group(GroupOf(gameId)).SendAsync("presence", userId, false);
        }
    }
}
