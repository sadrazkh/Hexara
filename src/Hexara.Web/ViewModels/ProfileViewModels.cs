using System.ComponentModel.DataAnnotations;
using Hexara.Application.Common.Interfaces;
using Hexara.Application.Games;

namespace Hexara.Web.ViewModels;

public class ProfileViewModel
{
    public PlayerStanding? Standing { get; init; }

    public bool IsGuest { get; init; }

    public IReadOnlyList<GameSummary> RecentGames { get; init; } = [];

    public UpgradeAccountViewModel Upgrade { get; init; } = new();

    public string? Message { get; init; }
}

/// <summary>
/// ارتقای حساب مهمان. نام نمایشی اختیاری است — اگر خالی بماند همان نام تصادفیِ
/// مهمان می‌ماند و کسی مجبور نیست چیزی عوض کند.
/// </summary>
public class UpgradeAccountViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password), Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    [StringLength(60, MinimumLength = 2)]
    public string? DisplayName { get; set; }
}
