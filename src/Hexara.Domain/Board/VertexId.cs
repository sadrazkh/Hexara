namespace Hexara.Domain.Board;

/// <summary>
/// شناسه‌ی یک گوشه‌ی برد (محل آبادی و شهر).
///
/// هر گوشه بین سه هگز مشترک است، پس سه نمایش هم‌ارز دارد. برای اینکه سرور و
/// کلاینت و دیتابیس همیشه یک رشته‌ی یکسان ببینند، نمایش «کانونی» را نگه می‌داریم:
/// کوچک‌ترین ‎(q, r, corner)‎ از نظر ترتیب واژه‌نامه‌ای.
///
/// گوشه‌ی شماره‌ی <c>i</c> از هگز ‎H‎ گوشه‌ای است که بین همسایه‌های ‎i‎ و ‎i+1‎ قرار دارد،
/// بنابراین: ‎(H, i) ≡ (H+d_i, i+2) ≡ (H+d_{i+1}, i+4)‎.
/// </summary>
public readonly record struct VertexId
{
    private VertexId(Axial hex, int corner)
    {
        Hex = hex;
        Corner = corner;
    }

    public Axial Hex { get; }

    public int Corner { get; }

    public static VertexId Of(Axial hex, int corner)
    {
        var c = Axial.NormalizeDirection(corner);

        var a = (hex, c);
        var b = (hex.Neighbor(c), Axial.NormalizeDirection(c + 2));
        var d = (hex.Neighbor(c + 1), Axial.NormalizeDirection(c + 4));

        var best = Min(Min(a, b), d);
        return new VertexId(best.Item1, best.Item2);
    }

    /// <summary>سه هگزی که این گوشه را در بر گرفته‌اند — مبنای توزیع منابع بعد از تاس.</summary>
    public IEnumerable<Axial> TouchingHexes()
    {
        yield return Hex;
        yield return Hex.Neighbor(Corner);
        yield return Hex.Neighbor(Corner + 1);
    }

    /// <summary>سه یالی که به این گوشه می‌رسند — مبنای بررسی اتصال جاده.</summary>
    public IEnumerable<EdgeId> TouchingEdges()
    {
        yield return EdgeId.Of(Hex, Corner);
        yield return EdgeId.Of(Hex, Corner + 1);
        yield return EdgeId.Of(Hex.Neighbor(Corner), Corner + 2);
    }

    /// <summary>سه گوشه‌ی مجاور — مبنای قاعده‌ی فاصله (فاصله‌ی حداقل دو گوشه).</summary>
    public IEnumerable<VertexId> AdjacentVertices()
    {
        yield return Of(Hex, Corner + 1);
        yield return Of(Hex, Corner - 1);
        yield return Of(Hex.Neighbor(Corner), Corner + 1);
    }

    private static (Axial, int) Min((Axial Hex, int Corner) a, (Axial Hex, int Corner) b)
    {
        if (a.Hex.Q != b.Hex.Q) return a.Hex.Q < b.Hex.Q ? a : b;
        if (a.Hex.R != b.Hex.R) return a.Hex.R < b.Hex.R ? a : b;
        return a.Corner <= b.Corner ? a : b;
    }

    public override string ToString() => $"V{Hex.Q},{Hex.R},{Corner}";
}
