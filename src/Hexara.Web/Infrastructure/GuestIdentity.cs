using System.Security.Cryptography;
using Hexara.Application.Players;

namespace Hexara.Web.Infrastructure;

/// <summary>
/// تولید هویت تصادفی برای کاربران مهمان — نام قابل تشخیص و رنگ آواتار متمایز.
/// </summary>
public static class GuestIdentity
{
    // پالت در Application زندگی می‌کند چون پیش‌فرضِ ستون و جایگزینِ نمای بازی هم
    // همان را لازم دارند؛ سه نسخه‌ی جدا از یک فهرست رنگ دقیقاً همان چیزی است که
    // باعث شد رنگ‌های نشانِ قدیمی جا بمانند.

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

    public static string NewAvatarColor() => Pick(AvatarPalette.Colors);

    public static string ColorForSeed(int seed) =>
        AvatarPalette.Colors[Math.Abs(seed) % AvatarPalette.Colors.Length];

    private static string Pick(string[] source) => source[RandomNumberGenerator.GetInt32(source.Length)];
}
