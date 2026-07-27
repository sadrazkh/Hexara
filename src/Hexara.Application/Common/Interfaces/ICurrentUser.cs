namespace Hexara.Application.Common.Interfaces;

/// <summary>
/// دسترسی به کاربر جاری بدون وابسته شدن لایه‌های داخلی به HttpContext.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    string? DisplayName { get; }

    bool IsAuthenticated { get; }

    bool IsGuest { get; }
}
