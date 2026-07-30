using System.Security.Claims;
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

    public RoomHub(RoomService rooms, RoomBroadcaster broadcaster)
    {
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

        return await ApplyAsync(await _rooms.StartAsync(room.Id, userId, Context.ConnectionAborted));
    }

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
