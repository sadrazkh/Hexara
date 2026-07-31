using System.Security.Claims;
using Hexara.Application.Common.Interfaces;
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
    private readonly GameChat _chat;
    private readonly IClock _clock;
    private readonly LiveKitTokens _voice;
    private readonly IPlayerDirectory _directory;
    private readonly SpectatorOptions _spectators;

    public GameHub(
        GameService games,
        GameViewBuilder views,
        GameBroadcaster broadcaster,
        GamePresence presence,
        GameLocks locks,
        GameChat chat,
        IClock clock,
        LiveKitTokens voice,
        IPlayerDirectory directory,
        SpectatorOptions spectators)
    {
        _games = games;
        _views = views;
        _broadcaster = broadcaster;
        _presence = presence;
        _locks = locks;
        _chat = chat;
        _clock = clock;
        _voice = voice;
        _directory = directory;
        _spectators = spectators;
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

        var seat = game.SeatOf(userId);

        // تماشاچی گروهِ خودش را دارد و **به گروه بازیکن‌ها راه ندارد**.
        //
        // این فقط تمیزکاری نیست: چت و «حضور» هر دو به گروه بازیکن‌ها می‌روند و
        // اگر تماشاچی آن‌جا بود، حرفِ خصوصیِ سرِ میز («سه گندم دارم») را
        // می‌شنید و می‌توانست به یکی از بازیکن‌ها برساند.
        if (seat is null)
        {
            if (!_spectators.Enabled)
            {
                return new JoinResult(false, "notYourGame", null);
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GameBroadcaster.WatchersOf(gameId),
                Context.ConnectionAborted);

            // «حضور» درباره‌ی بازیکن‌هاست؛ تماشاچی نه کسی را آنلاین می‌کند نه
            // مهلتِ نوبتِ کسی را عقب می‌اندازد.
            var watching = await _views.BuildAsync(
                game, viewerSeat: null, _presence.OnlineIn(gameId), Context.ConnectionAborted);

            return new JoinResult(true, null, watching);
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
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId, GameBroadcaster.WatchersOf(gameId), Context.ConnectionAborted);
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
        if (game is null)
        {
            return null;
        }

        // تماشاچی صندلی ندارد و همین‌جا بیشترین سانسور را می‌گیرد؛ اگر تماشا
        // خاموش باشد اصلاً نباید چیزی بگیرد.
        var seat = game.SeatOf(userId);
        if (seat is null && !_spectators.Enabled)
        {
            return null;
        }

        var missed = await _games.EventsSinceAsync(gameId, sinceVersion, seat, Context.ConnectionAborted);
        var view = await _views.BuildAsync(game, seat, _presence.OnlineIn(gameId), Context.ConnectionAborted);

        return new CatchUpResult(game.State.Version, missed, view);
    }

    // ── چت ───────────────────────────────────────────────────────────────

    /// <summary>
    /// یک پیام چت.
    ///
    /// عمداً چیزی برنمی‌گرداند و هرگز خطا نمی‌دهد: پیامِ ردشده (خالی، یا از حدِ
    /// سرعت گذشته) فقط فرستاده نمی‌شود. خرابیِ چت نباید به بازی سرایت کند.
    ///
    /// نامِ فرستنده فرستاده نمی‌شود، فقط صندلی‌اش — نام را کلاینت از روی همان
    /// نمای بازی درمی‌آورد، پس کسی نمی‌تواند خودش را جای دیگری جا بزند.
    /// </summary>
    public async Task SendChat(Guid gameId, string? text)
    {
        if (!_chat.Enabled || UserId is not { } userId)
        {
            return;
        }

        var game = await _games.GetAsync(gameId, Context.ConnectionAborted);
        if (game?.SeatOf(userId) is not { } seat)
        {
            return;
        }

        if (_chat.Post(gameId, seat, text, _clock.UtcNow) is { } message)
        {
            await Clients.Group(GroupOf(gameId)).SendAsync("chat", message, Context.ConnectionAborted);
        }
    }

    /// <summary>پیام‌های اخیر، برای کسی که تازه رسیده یا صفحه را نو کرده.</summary>
    public async Task<IReadOnlyList<ChatMessage>> ChatHistory(Guid gameId)
    {
        if (!_chat.Enabled || UserId is not { } userId)
        {
            return [];
        }

        var game = await _games.GetAsync(gameId, Context.ConnectionAborted);

        return game?.SeatOf(userId) is null ? [] : _chat.History(gameId);
    }

    // ── صدا و تصویر ──────────────────────────────────────────────────────

    /// <summary>
    /// بلیت ورود به اتاق صوتیِ این بازی.
    ///
    /// تهی یعنی یا صدا و تصویر پیکربندی نشده یا این کاربر سرِ این بازی نیست —
    /// و در هر دو حال کلاینت فقط دکمه‌اش را نشان نمی‌دهد. **نامِ اتاق را همین‌جا
    /// سرور می‌سازد؛** اگر کلاینت آن را می‌فرستاد، هر کسی می‌توانست بلیتِ اتاقِ
    /// یک بازی دیگر بگیرد.
    ///
    /// بلیت کوتاه‌عمر است، پس کلاینت باید هر بار پیش از وصل شدن یکی تازه بگیرد.
    /// </summary>
    public async Task<VoiceTicket?> VoiceTicket(Guid gameId)
    {
        if (!_voice.IsConfigured || UserId is not { } userId)
        {
            return null;
        }

        var game = await _games.GetAsync(gameId, Context.ConnectionAborted);
        if (game?.SeatOf(userId) is null)
        {
            return null;
        }

        var profile = (await _directory.GetAsync([userId], Context.ConnectionAborted)).FirstOrDefault();

        return _voice.Issue(gameId, userId, profile?.DisplayName ?? string.Empty);
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
