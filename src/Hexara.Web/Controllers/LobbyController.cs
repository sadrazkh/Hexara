using Hexara.Application.Common.Interfaces;
using Hexara.Application.Rooms;
using Hexara.Web.Infrastructure;
using Hexara.Web.Realtime;
using Hexara.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexara.Web.Controllers;

/// <summary>
/// لابی: فهرست اتاق‌های باز، ساخت اتاق، پیوستن با کد و صفحه‌ی خود اتاق.
///
/// هر عملی که چیزی را تغییر می‌دهد POST است و توکن ضدجعل دارد؛ ورود لازم است
/// (ورود مهمان هم ورود حساب می‌شود).
/// </summary>
[Authorize]
public class LobbyController : Controller
{
    private readonly RoomService _rooms;
    private readonly ICurrentUser _user;
    private readonly UiTranslator _t;
    private readonly RoomBroadcaster _live;

    public LobbyController(
        RoomService rooms,
        ICurrentUser user,
        UiTranslator translator,
        RoomBroadcaster live)
    {
        _rooms = rooms;
        _user = user;
        _t = translator;
        _live = live;
    }

    private Guid UserId => _user.UserId ?? throw new InvalidOperationException("کاربر وارد نشده است.");

    [HttpGet]
    public async Task<IActionResult> Index(string? code, CancellationToken cancellationToken)
    {
        return View(new LobbyIndexViewModel
        {
            OpenRooms = await _rooms.ListOpenAsync(cancellationToken: cancellationToken),
            MyRooms = await _rooms.ListForUserAsync(UserId, cancellationToken),
            JoinCode = code
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRoomViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await IndexWithError(RoomError.InvalidSettings, cancellationToken);
        }

        var result = await _rooms.CreateAsync(UserId, model.ToSettings(), cancellationToken);
        if (!result.Success)
        {
            return await IndexWithError(result.Error, cancellationToken);
        }

        return RedirectToAction(nameof(Room), new { code = result.Room!.Code });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(string code, CancellationToken cancellationToken)
    {
        var result = await _rooms.JoinAsync(code, UserId, cancellationToken);
        if (!result.Success)
        {
            return await IndexWithError(result.Error, cancellationToken);
        }

        // کسانی که همان اتاق را باز دارند باید همین‌جا صندلی تازه را ببینند.
        await _live.SendAsync(result.Room!, cancellationToken);

        return RedirectToAction(nameof(Room), new { code = result.Room!.Code });
    }

    [HttpGet("Lobby/Room/{code}")]
    public async Task<IActionResult> Room(string code, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindAsync(code, cancellationToken);
        if (room is null)
        {
            return await IndexWithError(RoomError.RoomNotFound, cancellationToken);
        }

        // اگر بازی شروع شده، اتاق فقط تابلوی راهنماست و آدم را به بازی می‌فرستد.
        if (room is { Status: RoomStatus.Started, GameId: { } gameId })
        {
            return RedirectToAction("Play", "Game", new { id = gameId });
        }

        return View(new RoomViewModel { Room = room, CurrentUserId = UserId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(Guid roomId, CancellationToken cancellationToken)
    {
        var result = await _rooms.LeaveAsync(roomId, UserId, cancellationToken);
        if (result.Room is { } room)
        {
            await _live.SendAsync(room, cancellationToken);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(
        Guid roomId,
        CreateRoomViewModel model,
        CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(roomId, cancellationToken);
        if (room is null)
        {
            return await IndexWithError(RoomError.RoomNotFound, cancellationToken);
        }

        // این فرم برد سفارشی و seed را نمی‌شناسد؛ اگر دست‌نخورده منتقلشان نکنیم،
        // عوض‌کردن تعداد بازیکن بی‌صدا برد ساخته‌شده را دور می‌ریزد.
        var settings = model.ToSettings() with
        {
            Seed = room.Settings.Seed,
            BoardCode = room.Settings.BoardCode
        };

        var result = await _rooms.UpdateSettingsAsync(roomId, UserId, settings, cancellationToken);
        if (!result.Success)
        {
            TempData["RoomError"] = Describe(result.Error);
        }
        else if (result.Room is { } updated)
        {
            await _live.SendAsync(updated, cancellationToken);
        }

        return await BackToRoom(roomId, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(Guid roomId, CancellationToken cancellationToken)
    {
        var result = await _rooms.StartAsync(roomId, UserId, cancellationToken);
        if (!result.Success)
        {
            TempData["RoomError"] = Describe(result.Error);
            return await BackToRoom(roomId, cancellationToken);
        }

        // بقیه با همین پیام خودشان به بازی می‌روند؛ کسی لازم نیست رفرش کند.
        if (result.Room is { } started)
        {
            await _live.SendAsync(started, cancellationToken);
        }

        return RedirectToAction("Play", "Game", new { id = result.GameId });
    }

    private async Task<IActionResult> BackToRoom(Guid roomId, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(roomId, cancellationToken);

        return room is null
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Room), new { code = room.Code });
    }

    private async Task<IActionResult> IndexWithError(RoomError error, CancellationToken cancellationToken)
    {
        TempData["RoomError"] = Describe(error);
        return await Index(code: null, cancellationToken);
    }

    /// <summary>خطای کددار دامنه به متن ترجمه‌شده تبدیل می‌شود؛ متن هرگز در Application نیست.</summary>
    private string Describe(RoomError error) => _t[$"lobby.error.{char.ToLowerInvariant(error.ToString()[0])}{error.ToString()[1..]}"];
}
