using Hexara.Domain.Board;

namespace Hexara.Domain.Tests.Board;

public class BoardGeneratorTests
{
    [Fact]
    public void Same_seed_produces_identical_board()
    {
        var a = BoardGenerator.Generate(2, 12345);
        var b = BoardGenerator.Generate(2, 12345);

        Assert.Equal(Describe(a), Describe(b));
    }

    [Fact]
    public void Different_seeds_produce_different_boards()
    {
        var a = BoardGenerator.Generate(2, 1);
        var b = BoardGenerator.Generate(2, 2);

        Assert.NotEqual(Describe(a), Describe(b));
    }

    /// <summary>برد کلاسیک: ۴ جنگل، ۴ چمنزار، ۴ مزرعه، ۳ تپه، ۳ کوه و ۱ بیابان.</summary>
    [Fact]
    public void Classic_board_has_classic_terrain_mix()
    {
        var board = BoardGenerator.Generate(2, 99);
        var counts = board.Tiles.GroupBy(t => t.Terrain).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(4, counts[Terrain.Forest]);
        Assert.Equal(4, counts[Terrain.Pasture]);
        Assert.Equal(4, counts[Terrain.Fields]);
        Assert.Equal(3, counts[Terrain.Hills]);
        Assert.Equal(3, counts[Terrain.Mountains]);
        Assert.Equal(1, counts[Terrain.Desert]);
    }

    [Fact]
    public void Classic_board_has_the_classic_number_tokens()
    {
        var board = BoardGenerator.Generate(2, 7);
        var numbers = board.Tiles.Where(t => t.Number is not null).Select(t => t.Number!.Value).Order().ToArray();

        Assert.Equal([2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12], numbers);
    }

    [Fact]
    public void Desert_never_gets_a_number()
    {
        var board = BoardGenerator.Generate(2, 4242);
        Assert.All(board.Tiles.Where(t => t.Terrain == Terrain.Desert), t => Assert.Null(t.Number));
    }

    [Fact]
    public void Seven_is_never_placed_on_a_tile()
    {
        var board = BoardGenerator.Generate(3, 8);
        Assert.DoesNotContain(board.Tiles, t => t.Number == 7);
    }

    /// <summary>قاعده‌ی خانه: دو عدد قرمز (۶ و ۸) نباید همسایه شوند.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(999UL)]
    [InlineData(1_000_000UL)]
    public void Red_numbers_are_never_adjacent(ulong seed)
    {
        var board = BoardGenerator.Generate(2, seed);
        var red = board.Tiles.Where(t => t.Number is 6 or 8).Select(t => t.Position).ToHashSet();

        Assert.All(red, hex => Assert.DoesNotContain(hex.Neighbors(), red.Contains));
    }

    [Fact]
    public void Classic_board_has_nine_ports_with_the_classic_mix()
    {
        var board = BoardGenerator.Generate(2, 55);

        Assert.Equal(9, board.Ports.Count);
        Assert.Equal(4, board.Ports.Count(p => p.IsGeneric));
        Assert.Equal(5, board.Ports.Count(p => !p.IsGeneric));
        Assert.Equal(5, board.Ports.Where(p => !p.IsGeneric).Select(p => p.Resource).Distinct().Count());
    }

    [Fact]
    public void Ports_sit_on_coast_edges_only()
    {
        var board = BoardGenerator.Generate(2, 31);
        var coast = board.CoastEdges().ToHashSet();

        Assert.All(board.Ports, p => Assert.Contains(p.Edge, coast));
    }

    /// <summary>دو بندر نباید گوشه‌ی مشترک داشته باشند وگرنه یک آبادی هر دو را می‌گیرد.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(77UL)]
    [InlineData(4096UL)]
    public void Ports_never_share_a_vertex(ulong seed)
    {
        var board = BoardGenerator.Generate(2, seed);
        var vertices = board.Ports.SelectMany(p => p.Vertices()).ToList();

        Assert.Equal(vertices.Count, vertices.Distinct().Count());
    }

    /// <summary>ساحل یک دور بسته است: برای شعاع R دقیقاً ‎6×(2R+1)‎ ضلع ساحلی دارد.</summary>
    [Theory]
    [InlineData(1, 18)]
    [InlineData(2, 30)]
    [InlineData(3, 42)]
    public void Coast_edge_count_matches_the_ring_formula(int radius, int expected)
    {
        var board = BoardGenerator.Generate(radius, 5);
        Assert.Equal(expected, board.CoastEdges().Count());
    }

    [Fact]
    public void Larger_boards_keep_their_proportions()
    {
        var board = BoardGenerator.Generate(3, 21);

        Assert.Equal(37, board.Tiles.Count);
        Assert.Equal(2, board.Tiles.Count(t => t.Terrain == Terrain.Desert));
        Assert.Equal(35, board.Tiles.Count(t => t.Number is not null));
    }

    private static string Describe(BoardLayout board) =>
        string.Join(
            "|",
            board.Tiles
                .OrderBy(t => t.Position.Q)
                .ThenBy(t => t.Position.R)
                .Select(t => $"{t.Position}:{t.Terrain}:{t.Number}"))
        + "//"
        + string.Join("|", board.Ports.Select(p => $"{p.Edge}:{p.Resource}"));
}
