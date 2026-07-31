using Hexara.Application.Common.Interfaces;
using Hexara.Application.Games;
using Hexara.Web.Realtime;
using Hexara.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexara.Web.Controllers;

/// <summary>
/// صفحه‌ی بازی.
///
/// در این فاز فقط نشان می‌دهد بازی ساخته و ذخیره شده و چه کسی کجا نشسته است؛
/// برد سه‌بعدی در فاز ۶ و به‌روزرسانی زنده در فاز ۵ اینجا می‌نشینند.
/// </summary>
[Authorize]
public class GameController : Controller
{
    private readonly GameService _games;
    private readonly ICurrentUser _user;
    private readonly GameChat _chat;
    private readonly LiveKitTokens _voice;
    private readonly SpectatorOptions _spectators;

    public GameController(
        GameService games,
        ICurrentUser user,
        GameChat chat,
        LiveKitTokens voice,
        SpectatorOptions spectators)
    {
        _games = games;
        _user = user;
        _chat = chat;
        _voice = voice;
        _spectators = spectators;
    }

    [HttpGet]
    public async Task<IActionResult> Play(Guid id, CancellationToken cancellationToken)
    {
        var game = await _games.GetAsync(id, cancellationToken);
        if (game is null)
        {
            return NotFound();
        }

        var userId = _user.UserId;
        if (userId is null)
        {
            return Forbid();
        }

        // تماشاچی صندلی ندارد. اگر تماشا خاموش باشد، کسی که سرِ بازی نیست همین‌جا
        // برمی‌گردد و اصلاً به هاب نمی‌رسد.
        var seat = game.SeatOf(userId.Value);
        if (seat is null && !_spectators.Enabled)
        {
            return Forbid();
        }

        return View(new GamePlayViewModel
        {
            Game = game,
            Seat = seat,
            ChatEnabled = _chat.Enabled && seat is not null,
            VoiceEnabled = _voice.IsConfigured && seat is not null
        });
    }
}
