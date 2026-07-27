namespace Hexara.Domain.Board;

/// <summary>
/// یک خانه‌ی برد. <see cref="Number"/> برای بیابان <c>null</c> است و برای بقیه
/// عددی بین ۲ تا ۱۲ (به جز ۷) که با تاس همان عدد منبع تولید می‌کند.
/// </summary>
public sealed record HexTile(Axial Position, Terrain Terrain, int? Number)
{
    public Resource? Resource => Terrain.Produces();

    /// <summary>شش گوشه‌ی این خانه.</summary>
    public IEnumerable<VertexId> Vertices()
    {
        for (var c = 0; c < 6; c++)
        {
            yield return VertexId.Of(Position, c);
        }
    }

    /// <summary>شش ضلع این خانه.</summary>
    public IEnumerable<EdgeId> Edges()
    {
        for (var s = 0; s < 6; s++)
        {
            yield return EdgeId.Of(Position, s);
        }
    }
}
