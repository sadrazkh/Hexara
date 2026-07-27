using Hexara.Application.Rooms;

namespace Hexara.Application.Tests;

public class RoomCodeTests
{
    [Fact]
    public void A_new_code_is_well_formed()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = RoomCode.New();

            Assert.Equal(RoomCode.Length, code.Length);
            Assert.True(RoomCode.IsWellFormed(code), code);
        }
    }

    /// <summary>این کد را آدم‌ها با صدا رد و بدل می‌کنند؛ حروف مبهم نباید در آن باشد.</summary>
    [Fact]
    public void Ambiguous_characters_never_appear()
    {
        var seen = string.Concat(Enumerable.Range(0, 500).Select(_ => RoomCode.New()));

        Assert.DoesNotContain('O', seen);
        Assert.DoesNotContain('0', seen);
        Assert.DoesNotContain('I', seen);
        Assert.DoesNotContain('1', seen);
        Assert.DoesNotContain('L', seen);
    }

    [Fact]
    public void Codes_are_not_all_the_same()
    {
        var codes = Enumerable.Range(0, 100).Select(_ => RoomCode.New()).ToHashSet();

        Assert.True(codes.Count > 90, $"فقط {codes.Count} کد یکتا از ۱۰۰ تا.");
    }

    [Theory]
    [InlineData("abc123", "ABC123")]
    [InlineData("  a b-c 1 2 3 ", "ABC123")]
    [InlineData("ABC123", "ABC123")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void Normalize_forgives_how_people_type(string? input, string expected) =>
        Assert.Equal(expected, RoomCode.Normalize(input));

    [Theory]
    [InlineData("ABC12")]
    [InlineData("ABC1234")]
    [InlineData("ABC1O3")]
    [InlineData("")]
    public void Malformed_codes_are_rejected(string code) =>
        Assert.False(RoomCode.IsWellFormed(code));
}
