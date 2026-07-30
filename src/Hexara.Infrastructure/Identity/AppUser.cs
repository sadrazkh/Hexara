using Hexara.Application.Players;
using Microsoft.AspNetCore.Identity;

namespace Hexara.Infrastructure.Identity;

/// <summary>
/// کاربر برنامه. کاربران مهمان هم رکورد واقعی دارند تا بتوانند در بازی شرکت کنند
/// و در صورت تمایل بعداً حساب خود را ارتقا دهند.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>کاربر بدون ثبت‌نام که فقط با کوکی شناسایی می‌شود.</summary>
    public bool IsGuest { get; set; }

    /// <summary>رنگ آواتار پیش‌فرض به صورت hex — برای نمایش در لابی.</summary>
    public string AvatarColor { get; set; } = AvatarPalette.Default;

    public string PreferredLanguage { get; set; } = "fa";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public int GamesPlayed { get; set; }

    public int GamesWon { get; set; }
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() { }

    public AppRole(string name) : base(name) { }
}
