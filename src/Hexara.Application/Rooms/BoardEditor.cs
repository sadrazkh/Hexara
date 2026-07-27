using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Application.Rooms;

/// <summary>
/// چیدمان برد به شکلی که ویرایشگر با آن کار می‌کند.
///
/// از همان رکوردهای عکس وضعیت استفاده می‌کند تا سرور و کلاینت یک واژگان داشته
/// باشند و ویرایشگر و برد بازی یک شکل داده را بفهمند.
/// </summary>
public sealed record BoardDraft(
    int Radius,
    IReadOnlyList<TileSnapshot> Tiles,
    IReadOnlyList<PortSnapshot> Ports);

/// <summary>
/// پل بین ویرایشگر و <see cref="BoardCode"/>.
///
/// عمداً هیچ منطقی از قالب کد اینجا تکرار نشده: ویرایشگر فقط آرایه‌ی خانه‌ها را
/// دستکاری می‌کند و برای خواندن و نوشتنِ کد به سرور می‌آید. یک پیاده‌سازی از
/// قالب یعنی هرگز دو طرف از هم جدا نمی‌افتند.
/// </summary>
public static class BoardEditor
{
    public static BoardDraft Random(int radius, ulong seed) =>
        ToDraft(BoardGenerator.Generate(Math.Clamp(radius, 1, 4), seed));

    public static bool TryRead(string? code, out BoardDraft? draft, out BoardCodeError error)
    {
        if (!BoardCode.TryDecode(code, out var layout, out error))
        {
            draft = null;
            return false;
        }

        draft = ToDraft(layout!);
        return true;
    }

    /// <summary>
    /// نقشه را به کد تبدیل می‌کند و همان‌جا با خواندنِ دوباره اعتبارش را می‌سنجد،
    /// تا کدی که ذخیره می‌شود قطعاً قابل بازخوانی باشد.
    /// </summary>
    public static bool TryWrite(BoardDraft? draft, out string? code, out BoardCodeError error)
    {
        code = null;

        if (draft is null || draft.Radius is < 1 or > 4)
        {
            error = BoardCodeError.BadRadius;
            return false;
        }

        var expected = Axial.Disc(draft.Radius).ToList();
        var byPosition = new Dictionary<Axial, TileSnapshot>();

        foreach (var tile in draft.Tiles)
        {
            byPosition[new Axial(tile.Q, tile.R)] = tile;
        }

        if (byPosition.Count != expected.Count || expected.Any(h => !byPosition.ContainsKey(h)))
        {
            error = BoardCodeError.WrongTileCount;
            return false;
        }

        var tiles = new List<HexTile>(expected.Count);
        foreach (var position in expected)
        {
            var tile = byPosition[position];

            if (!Enum.IsDefined(tile.Terrain))
            {
                error = BoardCodeError.UnknownTerrain;
                return false;
            }

            // بیابان هرگز عدد ندارد و بقیه همیشه دارند — نه بیشتر و نه کمتر.
            var isDesert = tile.Terrain == Terrain.Desert;
            if (isDesert != (tile.Number is null))
            {
                error = BoardCodeError.WrongNumberCount;
                return false;
            }

            if (tile.Number is { } number && (number is < 2 or > 12 || number == 7))
            {
                error = BoardCodeError.BadNumber;
                return false;
            }

            tiles.Add(new HexTile(position, tile.Terrain, tile.Number));
        }

        var ports = new List<Port>();
        foreach (var port in draft.Ports)
        {
            if (port.Side is < 0 or > 5 || (port.Resource is { } resource && !Enum.IsDefined(resource)))
            {
                error = BoardCodeError.BadPort;
                return false;
            }

            ports.Add(new Port(EdgeId.Of(new Axial(port.Q, port.R), port.Side), port.Resource));
        }

        var written = BoardCode.Encode(new BoardLayout(tiles, ports));

        // رفت‌وبرگشت کامل: هر قاعده‌ای که رمزگشا دارد (مثل ساحلی‌بودن بندر) اینجا هم می‌گیرد.
        if (!BoardCode.TryDecode(written, out _, out error))
        {
            return false;
        }

        code = written;
        return true;
    }

    private static BoardDraft ToDraft(BoardLayout layout) => new(
        layout.Radius,
        [.. Axial.Disc(layout.Radius)
            .Select(layout.TileAt)
            .Where(t => t is not null)
            .Select(t => new TileSnapshot(t!.Position.Q, t.Position.R, t.Terrain, t.Number))],
        [.. layout.Ports.Select(p =>
            new PortSnapshot(p.Edge.Hex.Q, p.Edge.Hex.R, p.Edge.Side, p.Resource))]);
}
