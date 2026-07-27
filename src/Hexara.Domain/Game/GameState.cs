using Hexara.Domain.Board;
using Hexara.Domain.Common;

namespace Hexara.Domain.Game;

/// <summary>
/// وضعیت کامل یک بازی.
///
/// وضعیت تغییرپذیر است و فقط <c>GameEngine</c> اجازه‌ی تغییرش را دارد (setterها
/// internal‌اند). موتور در کنار تغییر وضعیت، رویداد هم تولید می‌کند؛ رویدادها در
/// فاز ۳ ذخیره و در فاز ۵ برای همه پخش می‌شوند.
/// </summary>
public sealed class GameState
{
    private readonly Dictionary<VertexId, Building> _buildings = [];
    private readonly Dictionary<EdgeId, int> _roads = [];
    private readonly Dictionary<Resource, int> _bank;
    private readonly Dictionary<int, int> _pendingDiscards = [];

    private GameState(GameOptions options, BoardLayout board, IReadOnlyList<Guid> playerIds, Rng rng)
    {
        Options = options;
        Board = board;
        Rng = rng;
        Players = [.. playerIds.Select((id, i) => new PlayerState(i, id, options))];
        _bank = TerrainExtensions.AllResources.ToDictionary(r => r, _ => options.BankPerResource);

        // دزد از بیابان شروع می‌کند؛ اگر بردی بیابان نداشت از مرکز.
        Robber = board.Tiles.FirstOrDefault(t => t.Terrain == Terrain.Desert)?.Position ?? default;

        SetupOrder = BuildSetupOrder(playerIds.Count);
        CurrentPlayer = SetupOrder[0];
        Phase = TurnPhase.SetupSettlement;
    }

    public GameOptions Options { get; }

    public BoardLayout Board { get; }

    public IReadOnlyList<PlayerState> Players { get; }

    internal Rng Rng { get; }

    public IReadOnlyDictionary<VertexId, Building> Buildings => _buildings;

    /// <summary>جاده‌ها: ضلع ⇐ اندیس بازیکن.</summary>
    public IReadOnlyDictionary<EdgeId, int> Roads => _roads;

    public IReadOnlyDictionary<Resource, int> Bank => _bank;

    public Axial Robber { get; internal set; }

    public TurnPhase Phase { get; internal set; }

    public int CurrentPlayer { get; internal set; }

    /// <summary>شماره‌ی نوبت؛ بعد از پایان چیدمان اولیه از ۱ شروع می‌شود.</summary>
    public int TurnNumber { get; internal set; }

    /// <summary>هر تغییر وضعیت این عدد را جلو می‌برد — پایه‌ی هم‌زمان‌سازی در فاز ۵.</summary>
    public long Version { get; internal set; }

    public int? Die1 { get; internal set; }

    public int? Die2 { get; internal set; }

    public int? LastRoll => Die1 is null || Die2 is null ? null : Die1 + Die2;

    public int? Winner { get; internal set; }

    /// <summary>ترتیب مارپیچی چیدمان اولیه: ‎0..n-1‎ و بعد ‎n-1..0‎.</summary>
    public IReadOnlyList<int> SetupOrder { get; }

    /// <summary>جای فعلی در <see cref="SetupOrder"/>.</summary>
    public int SetupStep { get; internal set; }

    /// <summary>آبادی‌ای که همین الان در چیدمان اولیه گذاشته شد؛ جاده باید به آن بچسبد.</summary>
    public VertexId? LastSetupSettlement { get; internal set; }

    /// <summary>بازیکنانی که بعد از تاس ۷ باید کارت دور بریزند: اندیس ⇐ تعداد.</summary>
    public IReadOnlyDictionary<int, int> PendingDiscards => _pendingDiscards;

    public bool IsSetup => Phase is TurnPhase.SetupSettlement or TurnPhase.SetupRoad;

    public static GameState Create(GameOptions options, IReadOnlyList<Guid> playerIds)
    {
        options.Validate();

        if (playerIds.Count != options.PlayerCount)
        {
            throw new ArgumentException("تعداد شناسه‌های بازیکن با تنظیمات بازی نمی‌خواند.", nameof(playerIds));
        }

        if (playerIds.Distinct().Count() != playerIds.Count)
        {
            throw new ArgumentException("شناسه‌ی بازیکن‌ها باید یکتا باشد.", nameof(playerIds));
        }

        var board = BoardGenerator.Generate(options.BoardRadius, options.Seed);

        // مولد تاس از seed دیگری شروع می‌شود تا چیدمان برد دنباله‌ی تاس‌ها را لو ندهد.
        var rng = new Rng(options.Seed ^ 0xA5A5_5A5A_C3C3_3C3CUL);

        return new GameState(options, board, playerIds, rng);
    }

    public PlayerState Player(int index) => Players[index];

    public Building? BuildingAt(VertexId vertex) => _buildings.GetValueOrDefault(vertex);

    public int? RoadAt(EdgeId edge) => _roads.TryGetValue(edge, out var owner) ? owner : null;

    /// <summary>جاده‌های یک بازیکن — مبنای بررسی اتصال و در فاز ۲ب طولانی‌ترین جاده.</summary>
    public IEnumerable<EdgeId> RoadsOf(int playerIndex) =>
        _roads.Where(r => r.Value == playerIndex).Select(r => r.Key);

    public IEnumerable<KeyValuePair<VertexId, Building>> BuildingsOf(int playerIndex) =>
        _buildings.Where(b => b.Value.PlayerIndex == playerIndex);

    internal void PlaceBuilding(VertexId vertex, Building building) => _buildings[vertex] = building;

    internal void PlaceRoad(EdgeId edge, int playerIndex) => _roads[edge] = playerIndex;

    internal int BankOf(Resource resource) => _bank[resource];

    internal void BankTake(Resource resource, int amount) => _bank[resource] -= amount;

    internal void BankReturn(Resource resource, int amount) => _bank[resource] += amount;

    internal void SetPendingDiscard(int playerIndex, int amount) => _pendingDiscards[playerIndex] = amount;

    internal void ClearPendingDiscard(int playerIndex) => _pendingDiscards.Remove(playerIndex);

    private static int[] BuildSetupOrder(int playerCount)
    {
        var order = new int[playerCount * 2];
        for (var i = 0; i < playerCount; i++)
        {
            order[i] = i;
            order[order.Length - 1 - i] = i;
        }

        return order;
    }
}
