using Hexara.Domain.Board;

namespace Hexara.Domain.Game;

/// <summary>
/// وضعیت یک بازیکن داخل بازی. <see cref="Id"/> شناسه‌ی کاربر است و لایه‌های
/// بالاتر آن را می‌دهند؛ دامنه فقط با <see cref="Index"/> کار می‌کند.
/// </summary>
public sealed class PlayerState
{
    private readonly Dictionary<Resource, int> _resources;

    public PlayerState(int index, Guid id, GameOptions options)
    {
        Index = index;
        Id = id;
        SettlementsLeft = options.SettlementsPerPlayer;
        CitiesLeft = options.CitiesPerPlayer;
        RoadsLeft = options.RoadsPerPlayer;
        _resources = TerrainExtensions.AllResources.ToDictionary(r => r, _ => 0);
    }

    public int Index { get; }

    public Guid Id { get; }

    public IReadOnlyDictionary<Resource, int> Resources => _resources;

    public int SettlementsLeft { get; internal set; }

    public int CitiesLeft { get; internal set; }

    public int RoadsLeft { get; internal set; }

    /// <summary>امتیاز حاصل از ساخت‌وساز: هر آبادی ۱ و هر شهر ۲.</summary>
    public int VictoryPoints { get; internal set; }

    public int TotalCards => _resources.Values.Sum();

    public int this[Resource resource] => _resources[resource];

    internal void Add(Resource resource, int amount) => _resources[resource] += amount;

    internal void Remove(Resource resource, int amount) => _resources[resource] -= amount;

    public bool CanAfford(IReadOnlyDictionary<Resource, int> cost) =>
        cost.All(c => _resources[c.Key] >= c.Value);

    internal void Pay(IReadOnlyDictionary<Resource, int> cost)
    {
        foreach (var (resource, amount) in cost)
        {
            _resources[resource] -= amount;
        }
    }
}
