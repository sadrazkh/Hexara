using Hexara.Application.Players;

namespace Hexara.Infrastructure.Persistence.Migrations.Data;

/// <summary>
/// نگاشت پالت قدیمیِ آواتار به پالت تازه.
///
/// جدا از خودِ مهاجرت نگه داشته شده تا بتوان تستش کرد؛ فایلِ مهاجرت فقط SQL
/// می‌سازد. پالتِ قدیمی عمداً اینجا **کپی** شده و از کد زنده خوانده نمی‌شود:
/// مهاجرت باید همیشه همان کاری را بکند که روزِ نوشتنش می‌کرد، حتی اگر پالت
/// امروز باز هم عوض شود.
/// </summary>
public static class AvatarRepaint
{
    /// <summary>پالت پیش از بازطراحی تم — رنگ‌های نشانِ فیروزه‌ای/آبی.</summary>
    public static readonly string[] Old =
    [
        "#e0533d", // قرمزِ مرجانی
        "#4f9cf9", // آبی — پیش‌فرضِ قبلی هم همین بود
        "#f2b134", // کهربایی
        "#3fbf7f", // سبز
        "#a06cd5", // بنفش
        "#ef7ba8", // صورتی
        "#2ec4c4", // فیروزه‌ای
        "#c9a227", // طلاییِ تیره
        "#7f8fa6", // سربی
        "#d95d9a"  // سرخابی
    ];

    /// <summary>
    /// قدیمی ⇐ جدید. ترتیب <see cref="AvatarPalette.Colors"/> عمداً با
    /// <see cref="Old"/> هم‌تراز است تا هر رنگ به هم‌خانواده‌ی خودش برود.
    /// </summary>
    public static IReadOnlyDictionary<string, string> OldToNew { get; } = Build();

    public static IReadOnlyDictionary<string, string> NewToOld { get; } =
        OldToNew.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>
    /// دستورهای SQL مهاجرت. اینجا و نه داخل فایل مهاجرت، تا بشود واقعاً اجراشان
    /// کرد و دید که ستون درست را عوض می‌کنند — نه فقط اینکه کامپایل می‌شوند.
    ///
    /// مقایسه با <c>lower()</c> است تا رنگ‌های ذخیره‌شده‌ی با حرف بزرگ هم بگیرند،
    /// و رنگ‌های ناشناخته دست‌نخورده می‌مانند.
    /// </summary>
    public static IEnumerable<string> Statements(bool forward)
    {
        var mapping = forward ? OldToNew : NewToOld;

        foreach (var (from, to) in mapping)
        {
            yield return $"""
                UPDATE "IdentityUsers"
                SET "AvatarColor" = '{to}'
                WHERE lower("AvatarColor") = '{from.ToLowerInvariant()}';
                """;
        }
    }

    private static Dictionary<string, string> Build()
    {
        if (Old.Length != AvatarPalette.Colors.Length)
        {
            throw new InvalidOperationException(
                "پالت تازه باید هم‌اندازه‌ی پالت قدیمی باشد، وگرنه نگاشت جای‌به‌جا معنا ندارد.");
        }

        // کلیدها با حرف کوچک‌اند و SQL هم با lower() مقایسه می‌کند؛ رنگ‌های
        // ذخیره‌شده‌ی با حرف بزرگ هم باید بگیرند.
        return Old
            .Select((color, index) => (color, index))
            .ToDictionary(x => x.color.ToLowerInvariant(), x => AvatarPalette.Colors[x.index]);
    }
}
