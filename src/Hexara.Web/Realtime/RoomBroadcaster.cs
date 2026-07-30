using Hexara.Application.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace Hexara.Web.Realtime;

/// <summary>
/// رساندن وضعیت تازه‌ی اتاق به همه‌ی کسانی که آن صفحه را باز دارند.
///
/// از هاب بیرون کشیده شده به همان دلیلی که <see cref="GameBroadcaster"/> بیرون است:
/// دو فراخوان دارد — خودِ هاب، و <c>LobbyController</c> برای مسیرِ بدونِ
/// جاوااسکریپت. اگر فقط در هاب می‌ماند، ‎POST‎های فرمِ ساده بی‌صدا انجام می‌شدند و
/// بقیه تا رفرش‌کردن خبردار نمی‌شدند — یعنی همان اشکالی که قرار بود حل شود.
/// </summary>
public sealed class RoomBroadcaster
{
    private readonly IHubContext<RoomHub> _hub;
    private readonly ILogger<RoomBroadcaster> _logger;

    public RoomBroadcaster(IHubContext<RoomHub> hub, ILogger<RoomBroadcaster> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    /// <summary>نامِ گروه از کد اتاق می‌آید؛ کد همیشه بزرگ‌حرف است.</summary>
    public static string GroupOf(string code) => $"room:{code.ToUpperInvariant()}";

    /// <summary>
    /// وضعیت تازه‌ی اتاق. اگر بازی شروع شده باشد همین پیام کافی است: کلاینت از
    /// <see cref="RoomView.GameId"/> می‌فهمد که باید برود.
    /// </summary>
    public async Task SendAsync(Room room, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(room);

        try
        {
            await _hub.Clients
                .Group(GroupOf(room.Code))
                .SendAsync("room", RoomView.Of(room), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // نرسیدن خبر نباید خودِ عمل را بشکند؛ صفحه با پیوستنِ دوباره خودش را می‌رساند.
            _logger.LogWarning(ex, "ارسال وضعیت اتاق {Code} ناموفق بود.", room.Code);
        }
    }

    /// <summary>اتاقی که بسته شد و دیگر ‎Room‎ای برایش نمانده.</summary>
    public async Task SendClosedAsync(string code, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients.Group(GroupOf(code)).SendAsync("closed", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "اعلام بسته شدن اتاق {Code} ناموفق بود.", code);
        }
    }
}
