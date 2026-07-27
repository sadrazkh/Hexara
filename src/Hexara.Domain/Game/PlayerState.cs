using Hexara.Domain.Board;

namespace Hexara.Domain.Game;

/// <summary>
/// وضعیت یک بازیکن داخل بازی. <see cref="Id"/> شناسه‌ی کاربر است و لایه‌های
/// بالاتر آن را می‌دهند؛ دامنه فقط با <see cref="Index"/> کار می‌کند.
/// </summary>
public sealed class PlayerState
{
    private readonly Dictionary<Resource, int> _resources;
    private readonly Dictionary<DevelopmentCard, int> _development = [];
    private readonly Dictionary<DevelopmentCard, int> _newDevelopment = [];

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

    /// <summary>کارت‌های توسعه‌ی قابل بازی.</summary>
    public IReadOnlyDictionary<DevelopmentCard, int> DevelopmentCards => _development;

    /// <summary>کارت‌هایی که همین نوبت خریده شده‌اند و هنوز قابل بازی نیستند.</summary>
    public IReadOnlyDictionary<DevelopmentCard, int> NewDevelopmentCards => _newDevelopment;

    public int SettlementsLeft { get; internal set; }

    public int CitiesLeft { get; internal set; }

    public int RoadsLeft { get; internal set; }

    /// <summary>امتیاز حاصل از ساخت‌وساز: هر آبادی ۱ و هر شهر ۲.</summary>
    public int BuildingPoints { get; internal set; }

    /// <summary>کارت‌های امتیاز پیروزی — پنهان از بقیه ولی از همان لحظه‌ی خرید حساب می‌شوند.</summary>
    public int VictoryPointCards { get; internal set; }

    public bool HasLongestRoad { get; internal set; }

    public bool HasLargestArmy { get; internal set; }

    /// <summary>طول بلندترین جاده‌ی پیوسته‌ی این بازیکن (بعد از هر تغییر بازمحاسبه می‌شود).</summary>
    public int LongestRoadLength { get; internal set; }

    public int KnightsPlayed { get; internal set; }

    /// <summary>در هر نوبت فقط یک کارت توسعه می‌توان بازی کرد.</summary>
    public bool PlayedDevelopmentCardThisTurn { get; internal set; }

    /// <summary>امتیازی که همه می‌بینند — کارت‌های امتیاز پنهان در آن نیست.</summary>
    public int PublicVictoryPoints =>
        BuildingPoints + (HasLongestRoad ? 2 : 0) + (HasLargestArmy ? 2 : 0);

    /// <summary>امتیاز واقعی؛ مبنای برد.</summary>
    public int VictoryPoints => PublicVictoryPoints + VictoryPointCards;

    public int TotalCards => _resources.Values.Sum();

    public int TotalDevelopmentCards => _development.Values.Sum() + _newDevelopment.Values.Sum();

    public int this[Resource resource] => _resources[resource];

    public int this[DevelopmentCard card] => _development.GetValueOrDefault(card);

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

    internal void AddNewDevelopmentCard(DevelopmentCard card) =>
        _newDevelopment[card] = _newDevelopment.GetValueOrDefault(card) + 1;

    internal void RemoveDevelopmentCard(DevelopmentCard card) => _development[card]--;

    /// <summary>بازگرداندن وضعیت از روی عکس ذخیره‌شده.</summary>
    internal void RestoreFrom(PlayerSnapshot snapshot)
    {
        foreach (var (resource, amount) in snapshot.Resources)
        {
            _resources[resource] = amount;
        }

        _development.Clear();
        foreach (var (card, count) in snapshot.DevelopmentCards)
        {
            _development[card] = count;
        }

        _newDevelopment.Clear();
        foreach (var (card, count) in snapshot.NewDevelopmentCards)
        {
            _newDevelopment[card] = count;
        }

        SettlementsLeft = snapshot.SettlementsLeft;
        CitiesLeft = snapshot.CitiesLeft;
        RoadsLeft = snapshot.RoadsLeft;
        BuildingPoints = snapshot.BuildingPoints;
        VictoryPointCards = snapshot.VictoryPointCards;
        HasLongestRoad = snapshot.HasLongestRoad;
        HasLargestArmy = snapshot.HasLargestArmy;
        LongestRoadLength = snapshot.LongestRoadLength;
        KnightsPlayed = snapshot.KnightsPlayed;
        PlayedDevelopmentCardThisTurn = snapshot.PlayedDevelopmentCardThisTurn;
    }

    /// <summary>پایان نوبت: کارت‌های خریداری‌شده قابل بازی می‌شوند.</summary>
    internal void ReleaseNewDevelopmentCards()
    {
        foreach (var (card, count) in _newDevelopment)
        {
            _development[card] = _development.GetValueOrDefault(card) + count;
        }

        _newDevelopment.Clear();
    }
}
