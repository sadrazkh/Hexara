using System.Security.Claims;
using Hexara.Application.Common.Interfaces;

namespace Hexara.Web.Infrastructure;

public sealed class CurrentUser : ICurrentUser
{
    public const string GuestClaimType = "hexara:guest";

    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? DisplayName => Principal?.FindFirstValue(ClaimTypes.GivenName) ?? Principal?.Identity?.Name;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool IsGuest => Principal?.FindFirstValue(GuestClaimType) == "1";
}
