using System.Globalization;

namespace Hexara.Domain.Board;

/// <summary>دلیل رد شدن یک کد برد.</summary>
public enum BoardCodeError
{
    None = 0,
    Empty,
    UnknownVersion,
    Malformed,
    BadRadius,
    WrongTileCount,
    UnknownTerrain,
    WrongNumberCount,
    BadNumber,
    BadPort,
    PortNotOnCoast
}

/// <summary>
/// کد کوتاه و قابل اشتراک‌گذاری یک چیدمان برد.
///
/// عمداً خوانا نگه داشته شده و base64 نشده: وقتی کسی کدی می‌فرستد که کار نمی‌کند،
/// باید بشود با چشم دید کجایش ایراد دارد. طول یک برد کلاسیک حدود ۱۲۰ نویسه است.
///
/// قالب: ‎H1~radius~terrains~numbers~ports‎
/// • terrains: یک حرف برای هر خانه، به ترتیب <see cref="Axial.Disc"/>
/// • numbers: یک حرف برای هر خانه‌ی غیربیابانی، به همان ترتیب
/// • ports: گروه‌های ‎q.r.side.kind‎ جداشده با ‎_‎
///
/// حرف‌ها از روی منبع انتخاب شده‌اند نه نام زمین (L برای چوب، B برای آجر…) تا
/// همان حروف در بخش بندرها هم معنا بدهند.
/// </summary>
public static class BoardCode
{
    private const string Version = "H1";
    private const char Section = '~';
    private const char PortSeparator = '_';
    private const char FieldSeparator = '.';
    private const char GenericPort = '-';

    private static readonly Dictionary<Terrain, char> TerrainLetters = new()
    {
        [Terrain.Desert] = 'D',
        [Terrain.Forest] = 'L',
        [Terrain.Hills] = 'B',
        [Terrain.Pasture] = 'W',
        [Terrain.Fields] = 'G',
        [Terrain.Mountains] = 'O'
    };

    private static readonly Dictionary<char, Terrain> LettersToTerrain =
        TerrainLetters.ToDictionary(p => p.Value, p => p.Key);

    private static readonly Dictionary<Resource, char> ResourceLetters = new()
    {
        [Resource.Lumber] = 'L',
        [Resource.Brick] = 'B',
        [Resource.Wool] = 'W',
        [Resource.Grain] = 'G',
        [Resource.Ore] = 'O'
    };

    private static readonly Dictionary<char, Resource> LettersToResource =
        ResourceLetters.ToDictionary(p => p.Value, p => p.Key);

    /// <summary>
    /// عددها در کد برد داده‌اند نه متنِ نمایشی، پس همیشه با فرهنگ ناوابسته نوشته و
    /// خوانده می‌شوند.
    ///
    /// چرا صریح: علامت منفی در فرهنگ فارسی ‎U+200E U+2212‎ است نه ‎U+002D‎. مختصات
    /// بندرها روی ساحل‌اند و منفی می‌شوند، پس با فرهنگ جاری یک کدِ ساخته‌شده در
    /// رابط فارسی در رابط انگلیسی خوانده نمی‌شد — و برعکس. کدِ برد قرار است
    /// دست‌به‌دست بچرخد، پس باید مستقل از زبانِ سازنده‌اش معنا بدهد.
    /// </summary>
    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static bool TryNum(string raw, out int value) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static string Encode(BoardLayout board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var order = Axial.Disc(board.Radius).ToList();
        var terrains = new char[order.Count];
        var numbers = new List<char>();

        for (var i = 0; i < order.Count; i++)
        {
            var tile = board.TileAt(order[i])
                ?? throw new InvalidOperationException($"خانه‌ی {order[i]} در برد نیست.");

            terrains[i] = TerrainLetters[tile.Terrain];

            if (tile.Number is { } number)
            {
                numbers.Add(NumberToChar(number));
            }
        }

        var ports = board.Ports.Select(p =>
            string.Join(
                FieldSeparator,
                Num(p.Edge.Hex.Q),
                Num(p.Edge.Hex.R),
                Num(p.Edge.Side),
                p.Resource is { } resource ? ResourceLetters[resource] : GenericPort));

        return string.Join(
            Section,
            Version,
            Num(board.Radius),
            new string(terrains),
            new string([.. numbers]),
            string.Join(PortSeparator, ports));
    }

    public static bool TryDecode(string? code, out BoardLayout? board, out BoardCodeError error)
    {
        board = null;

        if (string.IsNullOrWhiteSpace(code))
        {
            error = BoardCodeError.Empty;
            return false;
        }

        var parts = code.Trim().Split(Section);
        if (parts.Length != 5)
        {
            error = BoardCodeError.Malformed;
            return false;
        }

        if (parts[0] != Version)
        {
            error = BoardCodeError.UnknownVersion;
            return false;
        }

        if (!TryNum(parts[1], out var radius) || radius is < 1 or > 4)
        {
            error = BoardCodeError.BadRadius;
            return false;
        }

        var order = Axial.Disc(radius).ToList();
        var terrains = parts[2];

        if (terrains.Length != order.Count)
        {
            error = BoardCodeError.WrongTileCount;
            return false;
        }

        var tiles = new List<HexTile>(order.Count);
        var numbers = parts[3];
        var next = 0;

        for (var i = 0; i < order.Count; i++)
        {
            if (!LettersToTerrain.TryGetValue(terrains[i], out var terrain))
            {
                error = BoardCodeError.UnknownTerrain;
                return false;
            }

            if (terrain == Terrain.Desert)
            {
                tiles.Add(new HexTile(order[i], terrain, null));
                continue;
            }

            if (next >= numbers.Length)
            {
                error = BoardCodeError.WrongNumberCount;
                return false;
            }

            if (CharToNumber(numbers[next++]) is not { } number)
            {
                error = BoardCodeError.BadNumber;
                return false;
            }

            tiles.Add(new HexTile(order[i], terrain, number));
        }

        if (next != numbers.Length)
        {
            error = BoardCodeError.WrongNumberCount;
            return false;
        }

        if (!TryReadPorts(parts[4], out var ports))
        {
            error = BoardCodeError.BadPort;
            return false;
        }

        var layout = new BoardLayout(tiles, ports);

        // بندری که روی ضلع ساحلی نباشد وسط خشکی می‌افتد و هیچ آبادی‌ای به آن نمی‌رسد.
        var coast = layout.CoastEdges().ToHashSet();
        if (ports.Any(p => !coast.Contains(p.Edge)))
        {
            error = BoardCodeError.PortNotOnCoast;
            return false;
        }

        board = layout;
        error = BoardCodeError.None;
        return true;
    }

    /// <summary>آیا این کد سالم است؟ برای اعتبارسنجی ورودی بدون نیاز به خودِ برد.</summary>
    public static bool IsValid(string? code) => TryDecode(code, out _, out _);

    private static bool TryReadPorts(string raw, out List<Port> ports)
    {
        ports = [];

        if (raw.Length == 0)
        {
            return true;
        }

        foreach (var group in raw.Split(PortSeparator))
        {
            var fields = group.Split(FieldSeparator);
            if (fields.Length != 4
                || !TryNum(fields[0], out var q)
                || !TryNum(fields[1], out var r)
                || !TryNum(fields[2], out var side)
                || side is < 0 or > 5
                || fields[3].Length != 1)
            {
                return false;
            }

            var kind = fields[3][0];
            if (kind == GenericPort)
            {
                ports.Add(new Port(EdgeId.Of(new Axial(q, r), side), null));
                continue;
            }

            if (!LettersToResource.TryGetValue(kind, out var resource))
            {
                return false;
            }

            ports.Add(new Port(EdgeId.Of(new Axial(q, r), side), resource));
        }

        return true;
    }

    // ۲ تا ۹ خودشان، و ۱۰ تا ۱۲ با a/b/c — تا هر عدد فقط یک نویسه بگیرد.
    private static char NumberToChar(int number) => number switch
    {
        >= 2 and <= 9 => (char)('0' + number),
        10 => 'a',
        11 => 'b',
        12 => 'c',
        _ => throw new ArgumentOutOfRangeException(nameof(number), number, "عدد خانه باید بین ۲ و ۱۲ و غیر از ۷ باشد.")
    };

    private static int? CharToNumber(char value) => value switch
    {
        >= '2' and <= '6' => value - '0',
        '7' => null, // ۷ سهم دزد است و هرگز روی خانه نمی‌نشیند.
        >= '8' and <= '9' => value - '0',
        'a' => 10,
        'b' => 11,
        'c' => 12,
        _ => null
    };
}
