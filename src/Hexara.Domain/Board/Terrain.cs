namespace Hexara.Domain.Board;

/// <summary>پنج منبع بازی. بیابان منبعی تولید نمی‌کند و اینجا جایی ندارد.</summary>
public enum Resource
{
    Lumber = 1,
    Brick = 2,
    Wool = 3,
    Grain = 4,
    Ore = 5
}

/// <summary>نوع زمین هر هگز.</summary>
public enum Terrain
{
    Desert = 0,
    Forest = 1,
    Hills = 2,
    Pasture = 3,
    Fields = 4,
    Mountains = 5
}

public static class TerrainExtensions
{
    /// <summary>منبعی که این زمین تولید می‌کند؛ برای بیابان <c>null</c>.</summary>
    public static Resource? Produces(this Terrain terrain) => terrain switch
    {
        Terrain.Forest => Resource.Lumber,
        Terrain.Hills => Resource.Brick,
        Terrain.Pasture => Resource.Wool,
        Terrain.Fields => Resource.Grain,
        Terrain.Mountains => Resource.Ore,
        _ => null
    };

    public static readonly Resource[] AllResources =
    [
        Resource.Lumber,
        Resource.Brick,
        Resource.Wool,
        Resource.Grain,
        Resource.Ore
    ];
}
