using System.Globalization;
using Hexara.Application.Players;

namespace Hexara.Application.Tests;

/// <summary>
/// پالت آواتار. اعداد کنتراست در سیستم طراحی «اندازه‌گیری‌شده» هستند نه
/// هدف‌گذاری‌شده، پس این پالت هم اندازه‌گیری می‌شود و نه چشمی تأیید.
/// </summary>
public class AvatarPaletteTests
{
    /// <summary>
    /// حرف اول نام روی آواتار نوشته می‌شود، پس هر رنگ باید حد ۴.۵:۱ متن معمولی
    /// را بدهد. اگر رنگی اضافه شد که نمی‌دهد، همین‌جا قرمز می‌شود.
    /// </summary>
    [Fact]
    public void Every_colour_carries_the_ink_at_the_text_threshold()
    {
        foreach (var color in AvatarPalette.Colors)
        {
            var contrast = Contrast(color, AvatarPalette.Ink);

            Assert.True(contrast >= 4.5, $"{color} فقط {contrast:F2} با مرکب می‌دهد.");
        }
    }

    /// <summary>حد سنجیده‌شده‌ی امروز؛ اگر پایین آمد یعنی کسی پالت را ضعیف کرده.</summary>
    [Fact]
    public void The_measured_floor_is_where_it_should_be()
    {
        var floor = AvatarPalette.Colors.Min(c => Contrast(c, AvatarPalette.Ink));

        Assert.True(floor >= 7.0, $"کف کنتراست به {floor:F2} افتاده است.");
    }

    /// <summary>
    /// رنگ آواتار برای تشخیص آدم‌ها از هم است، پس دو رنگ نباید به هم شبیه باشند.
    /// فاصله در فضای Lab سنجیده می‌شود نه RGB، چون RGB با چشم نمی‌خواند.
    /// </summary>
    [Fact]
    public void No_two_colours_look_alike()
    {
        var colors = AvatarPalette.Colors;

        for (var i = 0; i < colors.Length; i++)
        {
            for (var j = i + 1; j < colors.Length; j++)
            {
                var gap = Distance(colors[i], colors[j]);

                Assert.True(gap >= 18, $"{colors[i]} و {colors[j]} فقط {gap:F1} فاصله دارند.");
            }
        }
    }

    [Fact]
    public void The_palette_has_no_duplicates_and_is_well_formed()
    {
        Assert.Equal(AvatarPalette.Colors.Length, AvatarPalette.Colors.Distinct().Count());
        Assert.All(AvatarPalette.Colors, c => Assert.Matches("^#[0-9a-f]{6}$", c));
        Assert.Matches("^#[0-9a-f]{6}$", AvatarPalette.Ink);
    }

    [Fact]
    public void The_default_is_one_of_the_palette_colours()
    {
        Assert.Contains(AvatarPalette.Default, AvatarPalette.Colors);
    }

    /// <summary>رنگِ مشتق از شناسه باید پایدار و همیشه از داخل پالت باشد.</summary>
    [Fact]
    public void A_colour_derived_from_an_id_is_stable_and_in_the_palette()
    {
        var id = Guid.Parse("8f14e45f-ea4b-4c1a-9a2b-3d4e5f607182");

        Assert.Equal(AvatarPalette.For(id), AvatarPalette.For(id));
        Assert.Contains(AvatarPalette.For(id), AvatarPalette.Colors);

        // روی صد شناسه‌ی تصادفی هم هرگز از پالت بیرون نمی‌زند.
        for (var i = 0; i < 100; i++)
        {
            Assert.Contains(AvatarPalette.For(Guid.NewGuid()), AvatarPalette.Colors);
        }
    }

    /// <summary>
    /// پالت صندلی‌ها عمداً جدا است. این تست همان دلیل را ثابت نگه می‌دارد: قرمز و
    /// آبیِ صندلی زیر حد متن‌اند، چون مهره‌ی سه‌بعدی هیچ متنی حمل نمی‌کند.
    /// </summary>
    [Fact]
    public void The_seat_palette_could_not_be_reused_for_avatars()
    {
        string[] seats = ["#c0392b", "#2b6ca3", "#8a5a3b"];

        Assert.All(seats, seat => Assert.True(
            Contrast(seat, AvatarPalette.Ink) < 4.5,
            $"{seat} حالا حد متن را می‌دهد — دلیلِ جدا بودنِ دو پالت را دوباره بررسی کن."));
    }

    // ── سنجش ─────────────────────────────────────────────────────────────

    private static double Contrast(string a, string b)
    {
        var (high, low) = (Luminance(a), Luminance(b));
        if (high < low)
        {
            (high, low) = (low, high);
        }

        return (high + 0.05) / (low + 0.05);
    }

    private static double Luminance(string hex)
    {
        var (r, g, b) = Channels(hex);
        return (0.2126 * Linear(r)) + (0.7152 * Linear(g)) + (0.0722 * Linear(b));
    }

    private static double Linear(int channel)
    {
        var value = channel / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    /// <summary>فاصله‌ی CIE76 در فضای Lab — تقریب ساده‌ای از تفاوتِ دیده‌شده.</summary>
    private static double Distance(string a, string b)
    {
        var (l1, a1, b1) = Lab(a);
        var (l2, a2, b2) = Lab(b);

        return Math.Sqrt(((l1 - l2) * (l1 - l2)) + ((a1 - a2) * (a1 - a2)) + ((b1 - b2) * (b1 - b2)));
    }

    private static (double L, double A, double B) Lab(string hex)
    {
        var (r8, g8, b8) = Channels(hex);
        var (r, g, b) = (Linear(r8), Linear(g8), Linear(b8));

        var x = ((0.4124 * r) + (0.3576 * g) + (0.1805 * b)) / 0.95047;
        var y = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        var z = ((0.0193 * r) + (0.1192 * g) + (0.9505 * b)) / 1.08883;

        static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : (7.787 * t) + (16.0 / 116);

        var (fx, fy, fz) = (F(x), F(y), F(z));
        return ((116 * fy) - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    private static (int R, int G, int B) Channels(string hex)
    {
        var raw = hex.TrimStart('#');

        return (
            int.Parse(raw[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(raw[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(raw[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
