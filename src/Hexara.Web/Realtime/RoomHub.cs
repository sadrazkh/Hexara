using System.Security.Claims;
using Hexara.Application.Common.Interfaces;
using Hexara.Application.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Hexara.Web.Realtime;

/// <summary>پاسخ هر کاری در اتاق: وضعیت تازه، یا کدِ دلیلِ رد شدن.</summary>
public sealed record RoomActionResult(bool Success, string? Error, RoomView? Room);

/// <summary>
/// کانال زنده‌ی اتاق — از لحظه‌ی ورود به اتاق تا رفتن به بازی.
///
/// پیش از این، صفحه‌ی اتاق ‎Razor‎ی ساده بود و هیچ کانال زنده‌ای نداشت: پیوستن یک
/// نفر، عوض شدن تنظیمات و حتی شروع بازی تا وقتی کسی صفحه را رفرش نمی‌کرد دیده
/// نمی‌شد. حالا همان قواعد از راه هاب هم در دسترس‌اند.
///
/// دو اصل، مثل <see cref="GameHub"/>:
/// ۱. هیچ اجازه‌ای اینجا سنجیده نمی‌شود؛ همه به <see cref="RoomService"/> می‌رود.
/// ۲. اتاق خام فرستاده نمی‌شود؛ فقط <see cref="RoomView"/> که ‎seed‎ در آن نیست.
/// </summary>
[Authorize]
public sealed class RoomHub : Hub
{
    private readonly RoomService _rooms;
    private readonly RoomBroadcaster _broadcaster;
    private readonly GameChat _chat;
    private readonly IClock _clock;
    private readonly LiveKitTokens _voice;

    public RoomHub(
        RoomService rooms,
        RoomBroadcaster broadcaster,
        GameChat chat,
        IClock clock,
        LiveKitTokens voice)
    {
        _chat = chat;
        _clock = clock;
        _voice = voice;
        _rooms = rooms;
        _broadcaster = broadcaster;
    }

    private Guid? UserId =>
        Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>
    /// تماشای یک اتاق. عضویت شرط نیست: هر کسی که کد را دارد صفحه‌ی اتاق را می‌بیند،
    /// پس باید تغییرهایش را هم زنده ببیند — از جمله لحظه‌ای که میزبان شروع می‌کند.
    /// </summary>
    public async Task<RoomActionResult> Join(string code)
    {
        if (UserId is null)
        {
            return new RoomActionResult(false, "notAuthenticated", null);
        }

        var room = await _rooms.FindAsync(code, Context.ConnectionAborted);
        if (room is null)
        {
            return new RoomActionResult(false, "roomNotFound", null);
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RoomBroadcaster.GroupOf(room.Code),
            Context.ConnectionAborted);

        return new RoomActionResult(true, null, RoomView.Of(room));
    }

    public Task Leave(string code) =>
        Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RoomBroadcaster.GroupOf(code),
            Context.ConnectionAborted);

    /// <summary>
    /// گرفتن یک صندلی. جدا از <see cref="Join"/> است چون آن یکی فقط تماشا کردنِ
    /// اتاق است و این یکی واقعاً عضو می‌کند.
    /// </summary>
    public async Task<RoomActionResult> TakeSeat(string code)
    {
        if (UserId is not { } userId)
        {
            return new RoomActionResult(false, "notAuthenticated", null);
        }

        return await ApplyAsync(await _rooms.JoinAsync(code, userId, Context.ConnectionAborted));
    }

    public async Task<RoomActionResult> UpdateSettings(string code, RoomSettingsInput settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (UserId is not { } userId)
        {
            return new RoomActionResult(false, "notAuthenticated", null);
        }

        var room = await _rooms.FindAsync(code, Context.ConnectionAborted);
        if (room is null)
        {
            return new RoomActionResult(false, "roomNotFound", null);
        }

        // ‎seed‎ و بردِ سفارشی در این فرم نیستند؛ اگر دست‌نخورده منتقلشان نکنیم،
        // عوض کردن تعداد بازیکن بی‌صدا بردِ ساخته‌شده را دور می‌ریزد.
        var wanted = room.Settings with
        {
            MaxPlayers = settings.MaxPlayers,
            VictoryPoints = settings.VictoryPoints,
            BoardRadius = settings.BoardRadius,
            FriendlyRobber = settings.FriendlyRobber,
            Teams = settings.Teams
        };

        return await ApplyAsync(
            await _rooms.UpdateSettingsAsync(room.Id, userId, wanted, Context.ConnectionAborted));
    }

    /// <summary>دور ریختن بردِ سفارشی و برگشتن به بردِ تصادفی.</summary>
    public async Task<RoomActionResult> ClearBoard(string code)
    {
        if (UserId is not { } userId)
        {
            return new RoomActionResult(false, "notAuthenticated", null);
        }

        var room = await _rooms.FindAsync(code, Context.ConnectionAborted);
        if (room is null)
        {
            return new RoomActionResult(false, "roomNotFound", null);
        }

        return await ApplyAsync(await _rooms.UpdateSettingsAsync(
            room.Id,
            userId,
            room.Settings with { BoardCode = null },
            Context.ConnectionAborted));
    }

    public async Task<RoomActionResult> Start(string code)
    {
        if (UserId is not { } userId)
        {
            return new RoomActionResult(false, "notAuthenticated", null);
        }

        var room = await _rooms.FindAsync(code, Context.ConnectionAborted);
        if (room is null)
        {
            return new RoomActionResult(false, "roomNotFound", null);
        }

        var started = await _rooms.StartAsync(room.Id, userId, Context.ConnectionAborted);

        // گفت‌وگوی اتاق با بازی می‌آید. تا یک ثانیه پیش داشتند هماهنگ می‌کردند و
        // اگر تاریخچه جا می‌ماند، صفحه‌ی بازی با چتِ خالی باز می‌شد.
        if (started.GameId is { } gameId)
        {
            _chat.Move(room.Id, gameId);
        }

        return await ApplyAsync(started);
    }

    // ── گفت‌وگو و صدا ────────────────────────────────────────────────────

    /// <summary>
    /// یک پیام در اتاق انتظار.
    ///
    /// **فقط کسانی که صندلی دارند می‌فرستند و فقط همان‌ها می‌گیرند.** اتاق را هر
    /// کسی که کد را دارد می‌تواند تماشا کند (‎Join‎ عضویت نمی‌خواهد)، و همان
    /// قاعده‌ای که در بازی گذاشتیم اینجا هم برقرار است: تماشاچی حرفِ سرِ میز را
    /// نمی‌شنود.
    ///
    /// به‌جای گروه، مستقیم به کاربرهای صندلی‌دار فرستاده می‌شود — عضویتِ گروه
    /// باید با هر گرفتن و رها کردنِ صندلی به‌روز می‌ماند و یک بار جا ماندنش یعنی
    /// نشتی؛ فهرست اعضا همیشه تازه خوانده می‌شود.
    /// </summary>
    public async Task SendChat(string code, string? text)
    {
        if (!_chat.Enabled || UserId is not { } userId)
        {
            return;
        }

        var room = await _rooms.FindAsync(code, Context.ConnectionAborted);
        if (SeatOf(room, userId) is not { } seat)
        {
            return;
        }

        if (_chat.Post(room!.Id, seat, text, _clock.UtcNow) is { } message)
        {
            await Clients.Users(Members(room)).SendAsync("chat", message, Context.ConnectionAborted);
        }
    }

    /// <summary>پیام‌های اخیرِ همین اتاق؛ تماشاچی چیزی نمی‌گیرد.</summary>
    public async Task<IReadOnlyList<ChatMessage>> ChatHistory(string code)
    {
        if (!_chat.Enabled || UserId is not { } userId)
        {
            return [];
        }

        var room = await _rooms.FindAsync(code, Context.ConnectionAborted);

        return SeatOf(room, userId) is null ? [] : _chat.History(room!.Id);
    }

    /// <summary>
    /// بلیت صدای اتاق انتظار.
    ///
    /// اتاق صوتی‌اش از اتاق صوتیِ بازی جداست، پس تماشاچیِ اتاق — حتی اگر روزی
    /// بلیت بگیرد — به صدای خودِ بازی نمی‌رسد.
    /// </summary>
    public async Task<VoiceTicket?> VoiceTicket(string code)
    {
        if (!_voice.IsConfigured || UserId is not { } userId)
        {
            return null;
        }

        var room = await _rooms.FindAsync(code, Context.ConnectionAborted);
        if (SeatOf(room, userId) is null)
        {
            return null;
        }

        var name = room!.Members.First(m => m.UserId == userId).DisplayName;

        return _voice.IssueForLobby(room.Id, userId, name);
    }

    /// <summary>صندلیِ این کاربر در این اتاق؛ تهی یعنی فقط تماشاچی است.</summary>
    private static int? SeatOf(Room? room, Guid userId) =>
        room?.Members.FirstOrDefault(m => m.UserId == userId)?.Seat;

    /// <summary>شناسه‌ی همه‌ی صندلی‌نشین‌ها — گیرندگانِ چتِ اتاق.</summary>
    private static IReadOnlyList<string> Members(Room room) =>
        [.. room.Members.Select(m => m.UserId.ToString())];

    /// <summary>ترک اتاق — نه فقط بیرون رفتن از گروهِ پیام، خودِ صندلی خالی می‌شود.</summary>
    public async Task<RoomActionResult> LeaveRoom(string code)
    {
        if (UserId is not { } userId)
        {
            return new RoomActionResult(false, "notAuthenticated", null);
        }

        var room = await _rooms.FindAsync(code, Context.ConnectionAborted);
        if (room is null)
        {
            return new RoomActionResult(false, "roomNotFound", null);
        }

        var result = await _rooms.LeaveAsync(room.Id, userId, Context.ConnectionAborted);

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RoomBroadcaster.GroupOf(room.Code),
            Context.ConnectionAborted);

        return await ApplyAsync(result);
    }

    /// <summary>
    /// نتیجه‌ی سرویس را به همه پخش می‌کند و به فراخوان‌کننده هم برمی‌گرداند.
    ///
    /// پخش شامل خودِ فرستنده هم می‌شود و این عمدی است: یک منبعِ حقیقت برای همه، تا
    /// صفحه‌ی میزبان راهِ دیگری برای تازه شدن نداشته باشد که با بقیه فرق کند.
    /// </summary>
    private async Task<RoomActionResult> ApplyAsync(RoomResult result)
    {
        if (!result.Success)
        {
            return new RoomActionResult(false, Camel(result.Error.ToString()), null);
        }

        if (result.Room is { } room)
        {
            await _broadcaster.SendAsync(room, Context.ConnectionAborted);
            return new RoomActionResult(true, null, RoomView.Of(room));
        }

        return new RoomActionResult(true, null, null);
    }

    /// <summary>کلیدهای ترجمه در کلاینت با حرف کوچک شروع می‌شوند.</summary>
    private static string Camel(string name) =>
        string.Concat(char.ToLowerInvariant(name[0]), name[1..]);
}
