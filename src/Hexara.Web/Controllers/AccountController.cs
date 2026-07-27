using Hexara.Application.Common.Interfaces;
using Hexara.Infrastructure.Identity;
using Hexara.Web.Infrastructure;
using Hexara.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Hexara.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;
    private readonly UiTranslator _t;
    private readonly IClock _clock;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        UiTranslator translator,
        IClock clock,
        ILogger<AccountController> logger)
    {
        _users = users;
        _signIn = signIn;
        _t = translator;
        _clock = clock;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) =>
        View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.FindByEmailAsync(model.Email);
        if (user is null || user.IsGuest)
        {
            ModelState.AddModelError(string.Empty, _t["auth.invalidCredentials"]);
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, _t["auth.invalidCredentials"]);
            return View(model);
        }

        await TouchLastSeenAsync(user);
        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null) =>
        View(new RegisterViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (await _users.FindByEmailAsync(model.Email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), _t["auth.emailTaken"]);
            return View(model);
        }

        var now = _clock.UtcNow;
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName.Trim(),
            AvatarColor = GuestIdentity.NewAvatarColor(),
            PreferredLanguage = UiTranslator.CurrentCulture(),
            IsGuest = false,
            CreatedAt = now,
            LastSeenAt = now
        };

        var created = await _users.CreateAsync(user, model.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signIn.SignInAsync(user, isPersistent: true);
        return RedirectToLocal(model.ReturnUrl);
    }

    /// <summary>
    /// ورود بدون ثبت‌نام. یک کاربر واقعی با پرچم مهمان ساخته می‌شود تا بتواند
    /// در بازی شرکت کند و بعداً حسابش قابل ارتقا باشد.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guest(string? returnUrl = null)
    {
        var now = _clock.UtcNow;
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"guest-{Guid.NewGuid():N}",
            DisplayName = GuestIdentity.NewDisplayName(),
            AvatarColor = GuestIdentity.NewAvatarColor(),
            PreferredLanguage = UiTranslator.CurrentCulture(),
            IsGuest = true,
            CreatedAt = now,
            LastSeenAt = now
        };

        var created = await _users.CreateAsync(user);
        if (!created.Succeeded)
        {
            _logger.LogError("ساخت کاربر مهمان ناموفق بود: {Errors}",
                string.Join(", ", created.Errors.Select(e => e.Description)));
            return RedirectToAction(nameof(Login));
        }

        await _signIn.SignInAsync(user, isPersistent: true);
        return RedirectToLocal(returnUrl);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    private async Task TouchLastSeenAsync(AppUser user)
    {
        user.LastSeenAt = _clock.UtcNow;
        await _users.UpdateAsync(user);
    }

    private IActionResult RedirectToLocal(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl!)
            : RedirectToAction(nameof(HomeController.Index), "Home");
}
