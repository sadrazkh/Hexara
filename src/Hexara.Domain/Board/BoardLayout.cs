namespace Hexara.Domain.Board;

/// <summary>
/// چیدمان تغییرناپذیر برد: خانه‌ها، بندرها و مجموعه‌ی گوشه‌ها و ضلع‌های مجاز.
///
/// این کلاس فقط «شکل زمین» است و هیچ چیزی درباره‌ی بازیکن‌ها نمی‌داند؛ وضعیت
/// ساخت‌وسازها در <c>Game/GameState</c> نگهداری می‌شود.
/// </summary>
public sealed class BoardLayout
{
    private readonly Dictionary<Axial, HexTile> _tiles;
    private readonly HashSet<VertexId> _vertices;
    private readonly HashSet<EdgeId> _edges;
    private readonly Dictionary<int, List<HexTile>> _byNumber;
    private readonly Dictionary<VertexId, Port> _portsByVertex;

    public BoardLayout(IEnumerable<HexTile> tiles, IEnumerable<Port> ports)
    {
        _tiles = tiles.ToDictionary(t => t.Position);
        if (_tiles.Count == 0)
        {
            throw new ArgumentException("برد باید حداقل یک خانه داشته باشد.", nameof(tiles));
        }

        Ports = ports.ToList();

        _vertices = [.. _tiles.Values.SelectMany(t => t.Vertices())];
        _edges = [.. _tiles.Values.SelectMany(t => t.Edges())];

        _byNumber = _tiles.Values
            .Where(t => t.Number is not null)
            .GroupBy(t => t.Number!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        _portsByVertex = [];
        foreach (var port in Ports)
        {
            foreach (var vertex in port.Vertices())
            {
                _portsByVertex[vertex] = port;
            }
        }

        Radius = _tiles.Keys.Max(h => Axial.Distance(h, default));
    }

    public IReadOnlyCollection<HexTile> Tiles => _tiles.Values;

    public IReadOnlyList<Port> Ports { get; }

    public IReadOnlyCollection<VertexId> Vertices => _vertices;

    public IReadOnlyCollection<EdgeId> Edges => _edges;

    /// <summary>شعاع برد بر حسب تعداد حلقه‌های هگز حول مرکز.</summary>
    public int Radius { get; }

    public bool HasTile(Axial hex) => _tiles.ContainsKey(hex);

    public HexTile? TileAt(Axial hex) => _tiles.GetValueOrDefault(hex);

    public bool ContainsVertex(VertexId vertex) => _vertices.Contains(vertex);

    public bool ContainsEdge(EdgeId edge) => _edges.Contains(edge);

    /// <summary>خانه‌هایی که با تاس این عدد تولید می‌کنند.</summary>
    public IReadOnlyList<HexTile> TilesWithNumber(int number) =>
        _byNumber.TryGetValue(number, out var list) ? list : [];

    public Port? PortAt(VertexId vertex) => _portsByVertex.GetValueOrDefault(vertex);

    /// <summary>ضلع‌های ساحلی: فقط یک طرفشان خانه‌ی برد است. محل مجاز بندرها.</summary>
    public IEnumerable<EdgeId> CoastEdges() =>
        _edges.Where(e => e.TouchingHexes().Count(HasTile) == 1);

    /// <summary>گوشه‌های داخل برد که به این خانه می‌رسند.</summary>
    public IEnumerable<VertexId> VerticesOf(Axial hex) =>
        _tiles.TryGetValue(hex, out var tile) ? tile.Vertices() : [];
}
