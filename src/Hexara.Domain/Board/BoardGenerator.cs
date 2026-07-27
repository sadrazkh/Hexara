using Hexara.Domain.Common;

namespace Hexara.Domain.Board;

/// <summary>
/// ساخت برد تصادفی از روی یک seed. با seed یکسان همیشه دقیقاً همان برد ساخته
/// می‌شود، پس می‌توان بردها را با یک کد کوتاه به اشتراک گذاشت (فاز ۷) و بازی‌ها
/// را بازپخش کرد.
/// </summary>
public static class BoardGenerator
{
    /// <summary>نسبت زمین‌ها در برد کلاسیک ۱۹ خانه‌ای — مبنای مقیاس‌دهی به بردهای بزرگ‌تر.</summary>
    private static readonly (Terrain Terrain, int Weight)[] TerrainWeights =
    [
        (Terrain.Forest, 4),
        (Terrain.Pasture, 4),
        (Terrain.Fields, 4),
        (Terrain.Hills, 3),
        (Terrain.Mountains, 3)
    ];

    /// <summary>ژتون‌های عدد برد کلاسیک؛ ۷ وجود ندارد چون سهم دزد است.</summary>
    private static readonly int[] ClassicNumbers =
        [5, 2, 6, 3, 8, 10, 9, 12, 11, 4, 8, 10, 9, 4, 5, 6, 3, 11];

    private const int MaxNumberAttempts = 1000;

    /// <summary>
    /// برد شش‌ضلعی با شعاع داده‌شده می‌سازد. شعاع ۲ همان برد کلاسیک ۱۹ خانه‌ای است.
    /// </summary>
    public static BoardLayout Generate(int radius, ulong seed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(radius, 1);

        var rng = new Rng(seed);
        var positions = Axial.Disc(radius).ToList();

        var terrains = BuildTerrainBag(positions.Count);
        rng.Shuffle(terrains);

        var numbered = positions.Count - terrains.Count(t => t == Terrain.Desert);
        var tiles = AssignNumbers(positions, terrains, BuildNumberBag(numbered), rng);

        var layout = new BoardLayout(tiles, []);
        return new BoardLayout(tiles, PlacePorts(layout, rng));
    }

    /// <summary>سبد زمین‌ها با همان نسبت‌های برد کلاسیک، مقیاس‌شده به تعداد خانه‌ها.</summary>
    private static List<Terrain> BuildTerrainBag(int tileCount)
    {
        var deserts = Math.Max(1, (int)Math.Round(tileCount / 19.0, MidpointRounding.AwayFromZero));
        var remaining = tileCount - deserts;

        var totalWeight = TerrainWeights.Sum(w => w.Weight);
        var bag = new List<Terrain>(tileCount);

        foreach (var (terrain, weight) in TerrainWeights)
        {
            var count = remaining * weight / totalWeight;
            bag.AddRange(Enumerable.Repeat(terrain, count));
        }

        // باقیمانده‌ی تقسیم به ترتیب وزن پخش می‌شود تا مجموع دقیقاً درست دربیاید.
        for (var i = 0; bag.Count < remaining; i++)
        {
            bag.Add(TerrainWeights[i % TerrainWeights.Length].Terrain);
        }

        bag.AddRange(Enumerable.Repeat(Terrain.Desert, deserts));
        return bag;
    }

    /// <summary>ژتون‌های عدد؛ برای بردهای بزرگ‌تر همان الگوی کلاسیک تکرار می‌شود.</summary>
    private static List<int> BuildNumberBag(int count)
    {
        var bag = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            bag.Add(ClassicNumbers[i % ClassicNumbers.Length]);
        }

        return bag;
    }

    /// <summary>
    /// اعداد را روی خانه‌های غیربیابانی می‌چیند و قاعده‌ی «۶ و ۸ کنار هم نه» را رعایت
    /// می‌کند. چون شکست هر تلاش با همان مولد seed‌دار انجام می‌شود، نتیجه همچنان
    /// برای یک seed مشخص قطعی است.
    /// </summary>
    private static List<HexTile> AssignNumbers(
        List<Axial> positions,
        List<Terrain> terrains,
        List<int> numbers,
        Rng rng)
    {
        List<HexTile>? last = null;

        for (var attempt = 0; attempt < MaxNumberAttempts; attempt++)
        {
            rng.Shuffle(numbers);

            var tiles = new List<HexTile>(positions.Count);
            var next = 0;
            for (var i = 0; i < positions.Count; i++)
            {
                var terrain = terrains[i];
                int? number = terrain == Terrain.Desert ? null : numbers[next++];
                tiles.Add(new HexTile(positions[i], terrain, number));
            }

            last = tiles;
            if (!HasAdjacentRedNumbers(tiles))
            {
                return tiles;
            }
        }

        // عملاً هرگز به اینجا نمی‌رسیم؛ اگر رسیدیم بازی باید قابل شروع بماند.
        return last!;
    }

    private static bool HasAdjacentRedNumbers(List<HexTile> tiles)
    {
        var red = tiles
            .Where(t => t.Number is 6 or 8)
            .Select(t => t.Position)
            .ToHashSet();

        return red.Any(hex => hex.Neighbors().Any(red.Contains));
    }

    /// <summary>
    /// بندرها را با فاصله‌ی یکنواخت روی ساحل می‌چیند. ساحل یک دور بسته است، پس با
    /// پیمودن آن و برداشتن هر چند ضلع یک‌بار، هیچ دو بندری گوشه‌ی مشترک پیدا نمی‌کند.
    /// </summary>
    private static List<Port> PlacePorts(BoardLayout layout, Rng rng)
    {
        var coast = WalkCoast(layout);
        if (coast.Count < 6)
        {
            return [];
        }

        // برد کلاسیک (شعاع ۲، ۳۰ ضلع ساحلی) دقیقاً ۹ بندر دارد؛ بردهای دیگر با
        // همان تراکم «یک بندر در هر سه ضلع ساحلی» مقیاس می‌شوند.
        var portCount = layout.Radius == 2 ? 9 : Math.Max(4, coast.Count / 3);

        var kinds = BuildPortKinds(portCount);
        rng.Shuffle(kinds);

        var step = coast.Count / portCount;
        var offset = rng.Next(coast.Count);

        var ports = new List<Port>(portCount);
        for (var i = 0; i < portCount; i++)
        {
            ports.Add(new Port(coast[(offset + (i * step)) % coast.Count], kinds[i]));
        }

        return ports;
    }

    /// <summary>یک بندر اختصاصی برای هر منبع و بقیه عمومی — همان ترکیب ۵ به ۴ کلاسیک.</summary>
    private static List<Resource?> BuildPortKinds(int count)
    {
        var kinds = new List<Resource?>(count);
        var resources = TerrainExtensions.AllResources;

        for (var i = 0; i < count; i++)
        {
            var slot = i % 9;
            kinds.Add(slot < resources.Length ? resources[slot] : null);
        }

        return kinds;
    }

    /// <summary>
    /// ضلع‌های ساحلی را به ترتیب دور زدن ساحل برمی‌گرداند. هر گوشه‌ی ساحلی دقیقاً به
    /// دو ضلع ساحلی می‌رسد، بنابراین ساحل یک دور ساده است و پیمودنش قطعی است.
    /// </summary>
    private static List<EdgeId> WalkCoast(BoardLayout layout)
    {
        var coast = layout.CoastEdges().ToHashSet();
        if (coast.Count == 0)
        {
            return [];
        }

        var byVertex = new Dictionary<VertexId, List<EdgeId>>();
        foreach (var edge in coast)
        {
            foreach (var vertex in edge.Endpoints())
            {
                if (!byVertex.TryGetValue(vertex, out var list))
                {
                    byVertex[vertex] = list = [];
                }

                list.Add(edge);
            }
        }

        // شروع از قطعی‌ترین نقطه‌ی ممکن تا خروجی به ترتیب پیمایش مجموعه وابسته نباشد.
        var start = coast.OrderBy(e => e.Hex.Q).ThenBy(e => e.Hex.R).ThenBy(e => e.Side).First();

        var ordered = new List<EdgeId> { start };
        var visited = new HashSet<EdgeId> { start };
        var current = start;

        while (true)
        {
            EdgeId? next = current.Endpoints()
                .SelectMany(v => byVertex.GetValueOrDefault(v, []))
                .Where(e => !visited.Contains(e))
                .Cast<EdgeId?>()
                .FirstOrDefault();

            if (next is null)
            {
                break;
            }

            ordered.Add(next.Value);
            visited.Add(next.Value);
            current = next.Value;
        }

        return ordered;
    }
}
