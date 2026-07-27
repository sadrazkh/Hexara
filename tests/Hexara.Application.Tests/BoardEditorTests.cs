using Hexara.Application.Rooms;
using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Application.Tests;

public class BoardEditorTests
{
    private static BoardDraft Classic(ulong seed = 12) => BoardEditor.Random(2, seed);

    [Fact]
    public void A_random_draft_covers_the_whole_board()
    {
        var draft = Classic();

        Assert.Equal(2, draft.Radius);
        Assert.Equal(19, draft.Tiles.Count);
        Assert.Equal(9, draft.Ports.Count);
        Assert.Equal(19, draft.Tiles.Select(t => (t.Q, t.R)).Distinct().Count());
    }

    [Fact]
    public void The_same_seed_gives_the_same_draft()
    {
        Assert.Equal(Describe(BoardEditor.Random(2, 99)), Describe(BoardEditor.Random(2, 99)));
        Assert.NotEqual(Describe(BoardEditor.Random(2, 99)), Describe(BoardEditor.Random(2, 100)));
    }

    [Fact]
    public void A_draft_survives_the_round_trip_through_a_code()
    {
        var draft = Classic();

        Assert.True(BoardEditor.TryWrite(draft, out var code, out _));
        Assert.True(BoardEditor.TryRead(code, out var back, out _));
        Assert.Equal(Describe(draft), Describe(back!));
    }

    /// <summary>ترتیب خانه‌ها در نقشه نباید مهم باشد — ویرایشگر ممکن است جابه‌جایشان کند.</summary>
    [Fact]
    public void Tile_order_does_not_matter()
    {
        var draft = Classic();
        var shuffled = draft with { Tiles = [.. draft.Tiles.Reverse()] };

        Assert.True(BoardEditor.TryWrite(draft, out var one, out _));
        Assert.True(BoardEditor.TryWrite(shuffled, out var two, out _));
        Assert.Equal(one, two);
    }

    [Fact]
    public void A_missing_tile_is_refused()
    {
        var draft = Classic();
        var short_ = draft with { Tiles = [.. draft.Tiles.Skip(1)] };

        Assert.False(BoardEditor.TryWrite(short_, out _, out var error));
        Assert.Equal(BoardCodeError.WrongTileCount, error);
    }

    [Fact]
    public void A_tile_outside_the_board_is_refused()
    {
        var draft = Classic();
        var moved = draft with
        {
            Tiles = [.. draft.Tiles.Skip(1).Prepend(draft.Tiles[0] with { Q = 9, R = 9 })]
        };

        Assert.False(BoardEditor.TryWrite(moved, out _, out var error));
        Assert.Equal(BoardCodeError.WrongTileCount, error);
    }

    /// <summary>بیابان هرگز عدد ندارد و بقیه همیشه دارند.</summary>
    [Fact]
    public void A_desert_with_a_number_is_refused()
    {
        var draft = Classic();
        var desert = draft.Tiles.First(t => t.Terrain == Terrain.Desert);

        var broken = draft with
        {
            Tiles = [.. draft.Tiles.Select(t => t == desert ? t with { Number = 8 } : t)]
        };

        Assert.False(BoardEditor.TryWrite(broken, out _, out var error));
        Assert.Equal(BoardCodeError.WrongNumberCount, error);
    }

    [Fact]
    public void A_land_tile_without_a_number_is_refused()
    {
        var draft = Classic();
        var land = draft.Tiles.First(t => t.Terrain != Terrain.Desert);

        var broken = draft with
        {
            Tiles = [.. draft.Tiles.Select(t => t == land ? t with { Number = null } : t)]
        };

        Assert.False(BoardEditor.TryWrite(broken, out _, out var error));
        Assert.Equal(BoardCodeError.WrongNumberCount, error);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(1)]
    [InlineData(13)]
    public void An_impossible_number_is_refused(int number)
    {
        var draft = Classic();
        var land = draft.Tiles.First(t => t.Terrain != Terrain.Desert);

        var broken = draft with
        {
            Tiles = [.. draft.Tiles.Select(t => t == land ? t with { Number = number } : t)]
        };

        Assert.False(BoardEditor.TryWrite(broken, out _, out var error));
        Assert.Equal(BoardCodeError.BadNumber, error);
    }

    [Fact]
    public void A_port_on_a_bad_side_is_refused()
    {
        var draft = Classic();
        var broken = draft with { Ports = [draft.Ports[0] with { Side = 9 }] };

        Assert.False(BoardEditor.TryWrite(broken, out _, out var error));
        Assert.Equal(BoardCodeError.BadPort, error);
    }

    /// <summary>بندر وسط خشکی هیچ آبادی‌ای نمی‌بیند — رفت‌وبرگشتِ کد این را می‌گیرد.</summary>
    [Fact]
    public void An_inland_port_is_refused()
    {
        var draft = Classic();
        var broken = draft with { Ports = [new PortSnapshot(0, 0, 0, Resource.Ore)] };

        Assert.False(BoardEditor.TryWrite(broken, out _, out var error));
        Assert.Equal(BoardCodeError.PortNotOnCoast, error);
    }

    [Fact]
    public void A_board_with_no_ports_is_allowed()
    {
        var draft = Classic() with { Ports = [] };

        Assert.True(BoardEditor.TryWrite(draft, out var code, out _));
        Assert.True(BoardEditor.TryRead(code, out var back, out _));
        Assert.Empty(back!.Ports);
    }

    /// <summary>عوض‌کردن نوع بندر — همان کاری که کلیک روی بندر در ویرایشگر می‌کند.</summary>
    [Fact]
    public void Changing_a_port_kind_keeps_the_board_valid()
    {
        var draft = Classic();
        var flipped = draft with
        {
            Ports = [.. draft.Ports.Select((p, i) => i == 0 ? p with { Resource = null } : p)]
        };

        Assert.True(BoardEditor.TryWrite(flipped, out var code, out _));
        Assert.True(BoardEditor.TryRead(code, out var back, out _));
        Assert.Null(back!.Ports.Single(p => p.Q == draft.Ports[0].Q
            && p.R == draft.Ports[0].R
            && p.Side == draft.Ports[0].Side).Resource);
    }

    [Fact]
    public void An_edited_draft_can_start_a_game()
    {
        var draft = Classic();
        var land = draft.Tiles.First(t => t.Terrain != Terrain.Desert);

        var edited = draft with
        {
            Tiles = [.. draft.Tiles.Select(t =>
                t == land ? t with { Terrain = Terrain.Mountains, Number = 11 } : t)]
        };

        Assert.True(BoardEditor.TryWrite(edited, out var code, out _));
        Assert.True(BoardCode.TryDecode(code, out var layout, out _));

        var state = GameState.Create(
            new GameOptions { PlayerCount = 2, Seed = 1 },
            [Guid.NewGuid(), Guid.NewGuid()],
            layout);

        var tile = state.Board.TileAt(new Axial(land.Q, land.R))!;
        Assert.Equal(Terrain.Mountains, tile.Terrain);
        Assert.Equal(11, tile.Number);
    }

    [Fact]
    public void A_broken_code_is_reported_not_thrown()
    {
        Assert.False(BoardEditor.TryRead("not-a-board", out var draft, out var error));
        Assert.Null(draft);
        Assert.NotEqual(BoardCodeError.None, error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void An_impossible_radius_is_refused(int radius)
    {
        var broken = Classic() with { Radius = radius };

        Assert.False(BoardEditor.TryWrite(broken, out _, out var error));
        Assert.Equal(BoardCodeError.BadRadius, error);
    }

    /// <summary>شعاع بیرون از بازه به نزدیک‌ترین مقدار مجاز بسته می‌شود، نه اینکه بترکد.</summary>
    [Fact]
    public void Random_clamps_the_radius()
    {
        Assert.Equal(1, BoardEditor.Random(0, 1).Radius);
        Assert.Equal(4, BoardEditor.Random(9, 1).Radius);
    }

    private static string Describe(BoardDraft draft) =>
        $"r{draft.Radius}|"
        + string.Join(
            ",",
            draft.Tiles.OrderBy(t => t.Q).ThenBy(t => t.R).Select(t => $"{t.Q}.{t.R}.{t.Terrain}.{t.Number}"))
        + "//"
        + string.Join(
            ",",
            draft.Ports.OrderBy(p => p.Q).ThenBy(p => p.R).ThenBy(p => p.Side)
                .Select(p => $"{p.Q}.{p.R}.{p.Side}.{p.Resource}"));
}
