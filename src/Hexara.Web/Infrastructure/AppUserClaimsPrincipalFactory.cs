using System.Security.Claims;
using Hexara.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Hexara.Web.Infrastructure;

/// <summary>
/// نام نمایشی، رنگ آواتار و نشانه‌ی مهمان بودن را داخل کوکی می‌گذارد تا
/// نمایش هدر و لابی بدون رفت‌وبرگشت به دیتابیس انجام شود.
/// </summary>
public sealed class AppUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<AppUser, AppRole>
{
    public const string AvatarColorClaimType = "hexara:color";

    public AppUserClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(ClaimTypes.GivenName, user.DisplayName));
        identity.AddClaim(new Claim(AvatarColorClaimType, user.AvatarColor));

        if (user.IsGuest)
        {
            identity.AddClaim(new Claim(CurrentUser.GuestClaimType, "1"));
        }

        return identity;
    }
}
