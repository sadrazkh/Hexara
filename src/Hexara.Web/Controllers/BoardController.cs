using System.Security.Cryptography;
using Hexara.Application.Common.Interfaces;
using Hexara.Application.Rooms;
using Hexara.Domain.Board;
using Hexara.Web.Infrastructure;
using Hexara.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hexara.Web.Controllers;

/// <summary>
/// ویرایشگر برد سفارشی.
///
/// صفحه‌اش Razor است و بقیه‌ی رفت‌وبرگشت‌ها JSON — چون ویرایشگر پیوسته کار می‌کند
/// و بارگذاری دوباره‌ی صفحه با هر تغییر بی‌معنا است. قالبِ کد فقط سمت سرور
/// پیاده شده، پس هر بار که کد لازم است کلاینت همین‌جا می‌آید.
/// </summary>
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
public class BoardController : Controller
{
    private readonly RoomService _rooms;
    private readonly ICurrentUser _user;
    private readonly UiTranslator _t;

    public BoardController(RoomService rooms, ICurrentUser user, UiTranslator translator)
    {
        _rooms = rooms;
        _user = user;
        _t = translator;
    }

    private Guid UserId => _user.UserId ?? throw new InvalidOperationException("کاربر وارد نشده است.");

    [HttpGet("Board/Edit/{code}")]
    public async Task<IActionResult> Edit(string code, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindAsync(code, cancellationToken);
        if (room is null)
        {
            return NotFound();
        }

        if (!room.IsHost(UserId))
        {
            return Forbid();
        }

        if (room.Status != RoomStatus.Open)
        {
            return RedirectToAction(nameof(LobbyController.Room), "Lobby", new { code = room.Code });
        }

        // اگر اتاق هنوز برد سفارشی ندارد، یکی تصادفی نشان می‌دهیم تا صفحه خالی نباشد.
        var draft = room.Settings.HasCustomBoard
            && BoardEditor.TryRead(room.Settings.BoardCode, out var stored, out _)
                ? stored!
                : BoardEditor.Random(room.Settings.BoardRadius, NewSeed());

        BoardEditor.TryWrite(draft, out var draftCode, out _);

        return View(new BoardEditViewModel
        {
            RoomId = room.Id,
            RoomCode = room.Code,
            Draft = draft,
            Code = draftCode ?? string.Empty,
            IsSaved = room.Settings.HasCustomBoard
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Random([FromBody] RandomBoardRequest request)
    {
        // seed رشته می‌آید چون ‎ulong‎ از محدوده‌ی عدد امن جاوااسکریپت بیرون است.
        var seed = ulong.TryParse(request.Seed, out var parsed) ? parsed : NewSeed();
        var draft = BoardEditor.Random(request.Radius, seed);

        return BoardEditor.TryWrite(draft, out var code, out var error)
            ? Ok(new BoardResponse(draft, code!))
            : BadRequest(new BoardErrorResponse(Describe(error)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Read([FromBody] ReadBoardRequest request)
    {
        if (!BoardEditor.TryRead(request.Code, out var draft, out var error))
        {
            return BadRequest(new BoardErrorResponse(Describe(error)));
        }

        BoardEditor.TryWrite(draft, out var code, out _);
        return Ok(new BoardResponse(draft!, code!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        [FromBody] SaveBoardRequest request,
        CancellationToken cancellationToken)
    {
        if (!BoardEditor.TryWrite(request.Draft, out var code, out var error))
        {
            return BadRequest(new BoardErrorResponse(Describe(error)));
        }

        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken);
        if (room is null)
        {
            return NotFound();
        }

        var settings = room.Settings with { BoardCode = code, BoardRadius = request.Draft!.Radius };
        var result = await _rooms.UpdateSettingsAsync(room.Id, UserId, settings, cancellationToken);

        // میزبان‌بودن و باز بودن اتاق را همان سرویس بررسی می‌کند.
        return result.Success
            ? Ok(new BoardResponse(request.Draft, code!))
            : BadRequest(new BoardErrorResponse(_t[$"lobby.error.{Camel(result.Error.ToString())}"]));
    }

    /// <summary>برداشتن برد سفارشی و برگشتن به برد تصادفی.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(Guid roomId, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(roomId, cancellationToken);
        if (room is null)
        {
            return NotFound();
        }

        await _rooms.UpdateSettingsAsync(
            roomId,
            UserId,
            room.Settings with { BoardCode = null },
            cancellationToken);

        return RedirectToAction(nameof(LobbyController.Room), "Lobby", new { code = room.Code });
    }

    private string Describe(BoardCodeError error) => _t[$"board.error.{Camel(error.ToString())}"];

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static ulong NewSeed()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToUInt64(bytes);
    }
}

public sealed record RandomBoardRequest(int Radius, string? Seed);

public sealed record ReadBoardRequest(string Code);

public sealed record SaveBoardRequest(Guid RoomId, BoardDraft? Draft);

public sealed record BoardResponse(BoardDraft Draft, string Code);

public sealed record BoardErrorResponse(string Error);
