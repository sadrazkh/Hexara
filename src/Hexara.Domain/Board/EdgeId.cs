namespace Hexara.Domain.Board;

/// <summary>
/// شناسه‌ی یک ضلع برد (محل جاده).
///
/// هر ضلع بین دو هگز مشترک است: ‎(H, i) ≡ (H+d_i, i+3)‎. مثل <see cref="VertexId"/>
/// نمایش کانونی نگه داشته می‌شود تا مقایسه و ذخیره‌سازی بدون ابهام باشد.
/// </summary>
public readonly record struct EdgeId
{
    private EdgeId(Axial hex, int side)
    {
        Hex = hex;
        Side = side;
    }

    public Axial Hex { get; }

    public int Side { get; }

    public static EdgeId Of(Axial hex, int side)
    {
        var s = Axial.NormalizeDirection(side);
        var other = hex.Neighbor(s);
        var otherSide = Axial.NormalizeDirection(s + 3);

        if (other.Q < hex.Q || (other.Q == hex.Q && (other.R < hex.R || (other.R == hex.R && otherSide < s))))
        {
            return new EdgeId(other, otherSide);
        }

        return new EdgeId(hex, s);
    }

    /// <summary>دو هگز طرفین این ضلع؛ در لبه‌ی برد یکی از آن‌ها بیرون از برد است.</summary>
    public IEnumerable<Axial> TouchingHexes()
    {
        yield return Hex;
        yield return Hex.Neighbor(Side);
    }

    /// <summary>دو سر این ضلع.</summary>
    public IEnumerable<VertexId> Endpoints()
    {
        yield return VertexId.Of(Hex, Side);
        yield return VertexId.Of(Hex, Side - 1);
    }

    public override string ToString() => $"E{Hex.Q},{Hex.R},{Side}";
}
