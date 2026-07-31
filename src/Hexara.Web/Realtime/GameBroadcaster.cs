using Hexara.Application.Games;
using Hexara.Domain.Game;
using Microsoft.AspNetCore.SignalR;

namespace Hexara.Web.Realtime;

/// <summary>
/// رساندن وضعیت تازه به بازیکن‌ها.
///
/// از هاب بیرون کشیده شده چون دو فراخوان دارد: خودِ هاب وقتی کسی حرکتی می‌زند، و
/// <see cref="AutoPlayService"/> وقتی بات به جای غایب بازی می‌کند. اگر این کد در
/// هاب می‌ماند، حرکت‌های بات بی‌صدا انجام می‌شدند و هیچ‌کس خبردار نمی‌شد.
/// </summary>
public sealed class GameBroadcaster
{
    private readonly IHubContext<GameHub> _hub;
    private readonly GameViewBuilder _views;
    private readonly GamePresence _presence;
    private readonly ILogger<GameBroadcaster> _logger;

    public GameBroadcaster(
        IHubContext<GameHub> hub,
        GameViewBuilder views,
        GamePresence presence,
        ILogger<GameBroadcaster> logger)
    {
        _hub = hub;
        _views = views;
        _presence = presence;
        _logger = logger;
    }

    /// <summary>گروه تماشاچی‌های یک بازی — جدا از گروه خودِ بازیکن‌ها.</summary>
    public static string WatchersOf(Guid gameId) => $"game:{gameId}:watch";

    /// <summary>
    /// هر بازیکن رویدادهای سانسورشده‌ی خودش و نمای تازه‌ی خودش را می‌گیرد.
    ///
    /// نمای کامل هر بار فرستاده می‌شود چون تنها منبع حقیقت است؛ رویدادها فقط برای
    /// انیمیشن و پیام‌اند.
    ///
    /// تماشاچی‌ها یک نمای مشترک می‌گیرند و **یک بار** ساخته می‌شود: نمای بی‌صندلی
    /// برای همه‌شان یکی است، چون هیچ‌کدام دستی ندارند که مالِ خودش باشد.
    /// </summary>
    public async Task SendAsync(
        StoredGame game,
        IReadOnlyList<GameEvent> events,
        CancellationToken cancellationToken = default)
    {
        var online = _presence.OnlineIn(game.Id);

        for (var seat = 0; seat < game.PlayerIds.Count; seat++)
        {
            var userId = game.PlayerIds[seat];

            try
            {
                var view = await _views.BuildAsync(game, seat, online, cancellationToken);

                await _hub.Clients.User(userId.ToString())
                    .SendAsync("applied", GameEventRedactor.ForSeat(events, seat), view, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // نرسیدن پیام به یک نفر نباید بقیه را از به‌روزرسانی محروم کند؛
                // آن یک نفر با CatchUp خودش را می‌رساند.
                _logger.LogWarning(ex, "ارسال وضعیت بازی {GameId} به بازیکن {UserId} ناموفق بود.", game.Id, userId);
            }
        }

        await SendToWatchersAsync(game, events, online, cancellationToken);
    }

    /// <summary>
    /// نمای تماشاچی‌ها.
    ///
    /// یک بار ساخته و به یک گروه فرستاده می‌شود. صندلیِ تهی یعنی ‎Hand‎ اصلاً
    /// ساخته نمی‌شود و حرکت‌های قانونی خالی‌اند، و رویدادها با کمترین دانش
    /// سانسور می‌شوند — پس تماشاچی هرگز به کارت کسی نمی‌رسد.
    /// </summary>
    private async Task SendToWatchersAsync(
        StoredGame game,
        IReadOnlyList<GameEvent> events,
        IReadOnlySet<Guid> online,
        CancellationToken cancellationToken)
    {
        try
        {
            var view = await _views.BuildAsync(game, viewerSeat: null, online, cancellationToken);

            await _hub.Clients.Group(WatchersOf(game.Id))
                .SendAsync("applied", GameEventRedactor.ForSeat(events, null), view, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // تماشاچی‌ها مهم‌ترین مخاطب نیستند؛ نرسیدنِ پیام به آن‌ها نباید
            // چیزی را متوقف کند و خودشان هم با CatchUp جبران می‌کنند.
            _logger.LogWarning(ex, "ارسال وضعیت بازی {GameId} به تماشاچی‌ها ناموفق بود.", game.Id);
        }
    }
}
