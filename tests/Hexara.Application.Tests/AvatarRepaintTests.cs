using Hexara.Application.Players;
using Hexara.Infrastructure.Persistence.Migrations.Data;

namespace Hexara.Application.Tests;

/// <summary>
/// نگاشتی که مهاجرت ‎RepaintAvatarColors‎ از آن SQL می‌سازد.
/// </summary>
public class AvatarRepaintTests
{
    [Fact]
    public void Every_old_colour_has_somewhere_to_go()
    {
        Assert.Equal(AvatarRepaint.Old.Length, AvatarRepaint.OldToNew.Count);

        foreach (var old in AvatarRepaint.Old)
        {
            Assert.True(
                AvatarRepaint.OldToNew.ContainsKey(old.ToLowerInvariant()),
                $"{old} در نگاشت نیست و بعد از مهاجرت خارج از تم می‌ماند.");
        }
    }

    [Fact]
    public void Nothing_lands_outside_the_new_palette()
    {
        Assert.All(AvatarRepaint.OldToNew.Values, color => Assert.Contains(color, AvatarPalette.Colors));
    }

    /// <summary>دو رنگ قدیمی نباید به یک رنگ تازه برسند، وگرنه آدم‌ها هم‌رنگ می‌شوند.</summary>
    [Fact]
    public void The_mapping_is_one_to_one()
    {
        var destinations = AvatarRepaint.OldToNew.Values.ToList();

        Assert.Equal(destinations.Count, destinations.Distinct().Count());
    }

    /// <summary>
    /// مهم‌ترین خاصیت نگاشت: کسی که آبی بود آبی می‌ماند. اگر ترتیب یکی از دو پالت
    /// جابه‌جا شود، هویت رنگی کاربرها به‌هم می‌ریزد و همین‌جا دیده می‌شود.
    /// </summary>
    [Theory]
    [InlineData("#4f9cf9", "#86b4d6")] // آبی ← آبیِ غبارآلود
    [InlineData("#3fbf7f", "#86bd80")] // سبز ← مریم‌گلی
    [InlineData("#f2b134", "#e6bb52")] // کهربایی ← طلایی
    [InlineData("#a06cd5", "#bfa0d8")] // بنفش ← اسطوخودوس
    [InlineData("#2ec4c4", "#6fbfb0")] // فیروزه‌ای ← فیروزه‌ای
    public void Each_colour_keeps_its_hue_family(string old, string expected)
    {
        Assert.Equal(expected, AvatarRepaint.OldToNew[old]);
    }

    /// <summary>پیش‌فرضِ قبلی آبی بود؛ پیش‌فرض تازه باید همان‌جا برود.</summary>
    [Fact]
    public void The_old_default_maps_onto_the_new_default()
    {
        Assert.Equal(AvatarPalette.Default, AvatarRepaint.OldToNew["#4f9cf9"]);
    }

    [Fact]
    public void The_reverse_mapping_undoes_the_forward_one()
    {
        foreach (var (old, fresh) in AvatarRepaint.OldToNew)
        {
            Assert.Equal(old, AvatarRepaint.NewToOld[fresh]);
        }
    }

    [Fact]
    public void Keys_are_lowercase_so_stored_uppercase_values_still_match()
    {
        Assert.All(AvatarRepaint.OldToNew.Keys, key => Assert.Equal(key.ToLowerInvariant(), key));
    }
}
