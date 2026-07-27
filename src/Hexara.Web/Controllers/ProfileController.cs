using Hexara.Application.Common.Interfaces;
using Hexara.Application.Games;
using Hexara.Infrastructure.Identity;
using Hexara.Web.Infrastructure;
using Hexara.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Hexara.Web.Controllers;

/// <summary>
/// پروفایل کاربر: کارنامه، بازی‌های اخیر، و ارتقای حساب مهمان به حساب واقعی.
/// </summary>
[Authorize]
public class ProfileController : Controller
{
    private const int RecentGames = 10;

    private readonly IPlayerStats _stats;
    private readonly GameService _games;
    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;
    private readonly ICurrentUser _user;
    private readonly UiTranslator _t;

    public ProfileController(
        IPlayerStats stats,
        GameService games,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        ICurrentUser user,
        UiTranslator translator)
    {
        _stats = stats;
        _games = games;
        _users = users;
        _signIn = signIn;
        _user = user;
        _t = translator;
    }

    private Guid UserId => _user.UserId ?? throw new InvalidOperationException("کاربر وارد نشده است.");

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await BuildAsync(new UpgradeAccountViewModel(), cancellationToken));
    }

    /// <summary>
    /// ارتقای حساب مهمان: همان کاربر می‌ماند، فقط ایمیل و رمز می‌گیرد. این‌طور
    /// تاریخچه‌ی بازی‌ها و کارنامه‌اش دست‌نخورده باقی می‌ماند.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upgrade(UpgradeAccountViewModel model, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(UserId.ToString());
        if (user is null)
        {
            return Forbid();
        }

        if (!user.IsGuest)
        {
            ModelState.AddModelError(string.Empty, _t["profile.alreadyFull"]);
            return View(nameof(Index), await BuildAsync(model, cancellationToken));
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildAsync(model, cancellationToken));
        }

        if (await _users.FindByEmailAsync(model.Email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), _t["auth.emailTaken"]);
            return View(nameof(Index), await BuildAsync(model, cancellationToken));
        }

        user.Email = model.Email;
        user.UserName = model.Email;
        user.IsGuest = false;

        if (!string.IsNullOrWhiteSpace(model.DisplayName))
        {
            user.DisplayName = model.DisplayName.Trim();
        }

        var password = await _users.AddPasswordAsync(user, model.Password);
        if (!password.Succeeded)
        {
            foreach (var error in password.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(nameof(Index), await BuildAsync(model, cancellationToken));
        }

        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            foreach (var error in updated.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(nameof(Index), await BuildAsync(model, cancellationToken));
        }

        // کوکی هنوز نشانه‌ی مهمان و نام قدیمی را دارد؛ باید از نو ساخته شود.
        await _signIn.RefreshSignInAsync(user);

        TempData["ProfileMessage"] = _t["profile.upgraded"];
        return RedirectToAction(nameof(Index));
    }

    private async Task<ProfileViewModel> BuildAsync(
        UpgradeAccountViewModel upgrade,
        CancellationToken cancellationToken)
    {
        var standing = await _stats.GetAsync(UserId, cancellationToken);
        var games = await _games.ListForPlayerAsync(UserId, cancellationToken);

        return new ProfileViewModel
        {
            Standing = standing,
            IsGuest = _user.IsGuest,
            RecentGames = [.. games.Take(RecentGames)],
            Upgrade = upgrade,
            Message = TempData["ProfileMessage"] as string
        };
    }
}

/// <summary>جدول رده‌بندی — باز برای همه، حتی کسی که وارد نشده.</summary>
public class LeaderboardController : Controller
{
    private const int Top = 25;

    private readonly IPlayerStats _stats;

    public LeaderboardController(IPlayerStats stats) => _stats = stats;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _stats.LeaderboardAsync(Top, cancellationToken));
}
