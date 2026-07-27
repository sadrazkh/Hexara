using System.Security.Cryptography;

namespace Hexara.Web.Infrastructure;

/// <summary>
/// تولید هویت تصادفی برای کاربران مهمان — نام قابل تشخیص و رنگ آواتار متمایز.
/// </summary>
public static class GuestIdentity
{
    // رنگ‌ها با فاصله‌ی روشنایی/فام کافی انتخاب شده‌اند تا روی تم تیره و برای
    // کاربران کوررنگ هم قابل تفکیک باشند.
    private static readonly string[] Palette =
    [
        "#e0533d", "#4f9cf9", "#f2b134", "#3fbf7f", "#a06cd5",
        "#ef7ba8", "#2ec4c4", "#c9a227", "#7f8fa6", "#d95d9a"
    ];

    private static readonly string[] Adjectives =
    [
        "Swift", "Bold", "Quiet", "Clever", "Wandering",
        "Iron", "Amber", "Northern", "Restless", "Lucky"
    ];

    private static readonly string[] Nouns =
    [
        "Settler", "Trader", "Mason", "Sailor", "Shepherd",
        "Miner", "Farmer", "Knight", "Pioneer", "Merchant"
    ];

    public static string NewDisplayName() =>
        $"{Pick(Adjectives)} {Pick(Nouns)} {RandomNumberGenerator.GetInt32(10, 100)}";

    public static string NewAvatarColor() => Pick(Palette);

    public static string ColorForSeed(int seed) => Palette[Math.Abs(seed) % Palette.Length];

    private static string Pick(string[] source) => source[RandomNumberGenerator.GetInt32(source.Length)];
}
