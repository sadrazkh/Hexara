using Hexara.Domain.Board;

namespace Hexara.Domain.Tests.Board;

public class BoardCodeTests
{
    private static BoardLayout Classic(ulong seed = 7) => BoardGenerator.Generate(2, seed);

    [Theory]
    [InlineData(1, 3UL)]
    [InlineData(2, 7UL)]
    [InlineData(3, 99UL)]
    [InlineData(4, 1234UL)]
    public void A_generated_board_survives_the_round_trip(int radius, ulong seed)
    {
        var original = BoardGenerator.Generate(radius, seed);

        Assert.True(BoardCode.TryDecode(BoardCode.Encode(original), out var decoded, out var error));
        Assert.Equal(BoardCodeError.None, error);
        Assert.Equal(Describe(original), Describe(decoded!));
    }

    [Fact]
    public void Encoding_is_stable()
    {
        var board = Classic();

        Assert.Equal(BoardCode.Encode(board), BoardCode.Encode(board));
    }

    /// <summary>کد باید با چشم قابل خواندن بماند — اگر کسی کد خرابی فرستاد بشود دیدش.</summary>
    [Fact]
    public void The_code_is_human_readable()
    {
        var code = BoardCode.Encode(Classic());
        var parts = code.Split('~');

        Assert.Equal("H1", parts[0]);
        Assert.Equal("2", parts[1]);
        Assert.Equal(19, parts[2].Length);
        Assert.Equal(18, parts[3].Length);
        Assert.Equal(9, parts[4].Split('_').Length);
    }

    [Fact]
    public void A_decoded_board_keeps_its_ports()
    {
        var original = Classic(31);
        BoardCode.TryDecode(BoardCode.Encode(original), out var decoded, out _);

        Assert.Equal(original.Ports.Count, decoded!.Ports.Count);
        Assert.Equal(
            original.Ports.Select(p => $"{p.Edge}:{p.Resource}").Order(),
            decoded.Ports.Select(p => $"{p.Edge}:{p.Resource}").Order());
    }

    [Fact]
    public void A_decoded_board_can_start_a_game()
    {
        BoardCode.TryDecode(BoardCode.Encode(Classic(55)), out var decoded, out _);

        var state = Domain.Game.GameState.Create(
            new Domain.Game.GameOptions { PlayerCount = 3, Seed = 1 },
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            decoded);

        Assert.Equal(19, state.Board.Tiles.Count);
        Assert.Equal(Domain.Board.Terrain.Desert, state.Board.TileAt(state.Robber)!.Terrain);
    }

    /// <summary>برد سفارشی باید از seed جلو بیفتد، وگرنه ویرایشگر بی‌اثر است.</summary>
    [Fact]
    public void A_custom_board_wins_over_the_seed()
    {
        var custom = BoardGenerator.Generate(2, 4242);
        var options = new Domain.Game.GameOptions { PlayerCount = 2, Seed = 1 };
        var players = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var generated = Domain.Game.GameState.Create(options, players);
        var explicitBoard = Domain.Game.GameState.Create(options, players, custom);

        Assert.NotEqual(Describe(generated.Board), Describe(explicitBoard.Board));
        Assert.Equal(Describe(custom), Describe(explicitBoard.Board));
    }

    // ── ورودی خراب ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, BoardCodeError.Empty)]
    [InlineData("", BoardCodeError.Empty)]
    [InlineData("   ", BoardCodeError.Empty)]
    [InlineData("H1~2~DDD", BoardCodeError.Malformed)]
    [InlineData("H9~2~D~~", BoardCodeError.UnknownVersion)]
    [InlineData("H1~9~D~~", BoardCodeError.BadRadius)]
    [InlineData("H1~x~D~~", BoardCodeError.BadRadius)]
    [InlineData("H1~1~DDD~~", BoardCodeError.WrongTileCount)]
    public void Broken_codes_are_refused(string? code, BoardCodeError expected)
    {
        Assert.False(BoardCode.TryDecode(code, out var board, out var error));
        Assert.Null(board);
        Assert.Equal(expected, error);
    }

    [Fact]
    public void An_unknown_terrain_letter_is_refused()
    {
        var code = Replace(BoardCode.Encode(Classic()), terrains: t => 'Z' + t[1..]);

        Assert.False(BoardCode.TryDecode(code, out _, out var error));
        Assert.Equal(BoardCodeError.UnknownTerrain, error);
    }

    [Fact]
    public void Too_few_numbers_are_refused()
    {
        var code = Replace(BoardCode.Encode(Classic()), numbers: n => n[..^1]);

        Assert.False(BoardCode.TryDecode(code, out _, out var error));
        Assert.Equal(BoardCodeError.WrongNumberCount, error);
    }

    [Fact]
    public void Too_many_numbers_are_refused()
    {
        var code = Replace(BoardCode.Encode(Classic()), numbers: n => n + "5");

        Assert.False(BoardCode.TryDecode(code, out _, out var error));
        Assert.Equal(BoardCodeError.WrongNumberCount, error);
    }

    /// <summary>۷ سهم دزد است و هرگز روی خانه نمی‌نشیند.</summary>
    [Fact]
    public void A_seven_on_a_tile_is_refused()
    {
        var code = Replace(BoardCode.Encode(Classic()), numbers: n => '7' + n[1..]);

        Assert.False(BoardCode.TryDecode(code, out _, out var error));
        Assert.Equal(BoardCodeError.BadNumber, error);
    }

    [Fact]
    public void A_nonsense_number_letter_is_refused()
    {
        var code = Replace(BoardCode.Encode(Classic()), numbers: n => 'z' + n[1..]);

        Assert.False(BoardCode.TryDecode(code, out _, out var error));
        Assert.Equal(BoardCodeError.BadNumber, error);
    }

    [Theory]
    [InlineData("0.0.9.L")]
    [InlineData("0.0.L")]
    [InlineData("0.0.0.Z")]
    [InlineData("x.0.0.L")]
    public void A_malformed_port_is_refused(string port)
    {
        var code = Replace(BoardCode.Encode(Classic()), ports: _ => port);

        Assert.False(BoardCode.TryDecode(code, out _, out var error));
        Assert.Equal(BoardCodeError.BadPort, error);
    }

    /// <summary>بندری که روی ساحل نباشد وسط خشکی می‌افتد و هیچ آبادی‌ای به آن نمی‌رسد.</summary>
    [Fact]
    public void An_inland_port_is_refused()
    {
        var code = Replace(BoardCode.Encode(Classic()), ports: _ => "0.0.0.L");

        Assert.False(BoardCode.TryDecode(code, out _, out var error));
        Assert.Equal(BoardCodeError.PortNotOnCoast, error);
    }

    [Fact]
    public void A_board_with_no_ports_is_fine()
    {
        var code = Replace(BoardCode.Encode(Classic()), ports: _ => string.Empty);

        Assert.True(BoardCode.TryDecode(code, out var board, out _));
        Assert.Empty(board!.Ports);
    }

    [Fact]
    public void Surrounding_whitespace_is_forgiven()
    {
        Assert.True(BoardCode.TryDecode($"  {BoardCode.Encode(Classic())}\n", out _, out _));
    }

    [Fact]
    public void IsValid_agrees_with_TryDecode()
    {
        Assert.True(BoardCode.IsValid(BoardCode.Encode(Classic())));
        Assert.False(BoardCode.IsValid("nope"));
    }

    private static string Describe(BoardLayout board) =>
        string.Join(
            "|",
            board.Tiles
                .OrderBy(t => t.Position.Q)
                .ThenBy(t => t.Position.R)
                .Select(t => $"{t.Position}:{t.Terrain}:{t.Number}"))
        + "//"
        + string.Join("|", board.Ports.Select(p => $"{p.Edge}:{p.Resource}").Order());

    /// <summary>یک بخش از کد را عوض می‌کند تا ورودی خرابِ واقع‌گرایانه ساخته شود.</summary>
    private static string Replace(
        string code,
        Func<string, string>? terrains = null,
        Func<string, string>? numbers = null,
        Func<string, string>? ports = null)
    {
        var parts = code.Split('~');

        if (terrains is not null) parts[2] = terrains(parts[2]);
        if (numbers is not null) parts[3] = numbers(parts[3]);
        if (ports is not null) parts[4] = ports(parts[4]);

        return string.Join('~', parts);
    }
}
