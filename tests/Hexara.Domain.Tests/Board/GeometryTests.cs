using Hexara.Domain.Board;

namespace Hexara.Domain.Tests.Board;

public class GeometryTests
{
    [Theory]
    [InlineData(1, 7)]
    [InlineData(2, 19)]
    [InlineData(3, 37)]
    public void Disc_has_expected_tile_count(int radius, int expected) =>
        Assert.Equal(expected, Axial.Disc(radius).Count());

    [Fact]
    public void Disc_has_no_duplicates() =>
        Assert.Equal(19, Axial.Disc(2).Distinct().Count());

    [Fact]
    public void Neighbors_are_all_at_distance_one()
    {
        var hex = new Axial(2, -1);
        Assert.All(hex.Neighbors(), n => Assert.Equal(1, Axial.Distance(hex, n)));
    }

    [Fact]
    public void Opposite_directions_cancel_out()
    {
        var hex = new Axial(-1, 3);
        for (var d = 0; d < 6; d++)
        {
            Assert.Equal(hex, hex.Neighbor(d).Neighbor(d + 3));
        }
    }

    [Fact]
    public void Cube_components_always_sum_to_zero()
    {
        foreach (var hex in Axial.Disc(3))
        {
            Assert.Equal(0, hex.Q + hex.R + hex.S);
        }
    }

    /// <summary>هر گوشه سه نمایش هم‌ارز دارد و هر سه باید به یک شناسه برسند.</summary>
    [Fact]
    public void Vertex_representations_are_canonical()
    {
        foreach (var hex in Axial.Disc(2))
        {
            for (var c = 0; c < 6; c++)
            {
                var canonical = VertexId.Of(hex, c);

                Assert.Equal(canonical, VertexId.Of(hex.Neighbor(c), c + 2));
                Assert.Equal(canonical, VertexId.Of(hex.Neighbor(c + 1), c + 4));
            }
        }
    }

    [Fact]
    public void Edge_representations_are_canonical()
    {
        foreach (var hex in Axial.Disc(2))
        {
            for (var s = 0; s < 6; s++)
            {
                Assert.Equal(EdgeId.Of(hex, s), EdgeId.Of(hex.Neighbor(s), s + 3));
            }
        }
    }

    [Fact]
    public void Negative_and_large_directions_wrap_around()
    {
        var hex = new Axial(0, 0);
        Assert.Equal(VertexId.Of(hex, 1), VertexId.Of(hex, -5));
        Assert.Equal(VertexId.Of(hex, 1), VertexId.Of(hex, 7));
        Assert.Equal(EdgeId.Of(hex, 2), EdgeId.Of(hex, -4));
    }

    [Fact]
    public void Vertex_touches_three_hexes_and_three_edges()
    {
        var vertex = VertexId.Of(new Axial(0, 0), 3);

        Assert.Equal(3, vertex.TouchingHexes().Distinct().Count());
        Assert.Equal(3, vertex.TouchingEdges().Distinct().Count());
        Assert.Equal(3, vertex.AdjacentVertices().Distinct().Count());
    }

    /// <summary>هر ضلعی که به یک گوشه می‌رسد باید همان گوشه را جزو دو سرش بشناسد.</summary>
    [Fact]
    public void Vertex_and_edge_incidence_agree()
    {
        foreach (var hex in Axial.Disc(2))
        {
            for (var c = 0; c < 6; c++)
            {
                var vertex = VertexId.Of(hex, c);
                Assert.All(vertex.TouchingEdges(), edge => Assert.Contains(vertex, edge.Endpoints()));
            }
        }
    }

    [Fact]
    public void Adjacent_vertices_share_an_edge()
    {
        var vertex = VertexId.Of(new Axial(1, -1), 2);

        foreach (var other in vertex.AdjacentVertices())
        {
            Assert.Contains(other, vertex.TouchingEdges().SelectMany(e => e.Endpoints()));
            Assert.Contains(vertex, other.AdjacentVertices());
        }
    }

    /// <summary>برد کلاسیک ۱۹ خانه‌ای دقیقاً ۵۴ گوشه و ۷۲ ضلع دارد.</summary>
    [Fact]
    public void Classic_board_has_54_vertices_and_72_edges()
    {
        var board = BoardGenerator.Generate(radius: 2, seed: 1);

        Assert.Equal(19, board.Tiles.Count);
        Assert.Equal(54, board.Vertices.Count);
        Assert.Equal(72, board.Edges.Count);
    }
}
