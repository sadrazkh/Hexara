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
    private readonly List<DevelopmentCard> _deck;

    private GameState(
        GameOptions options,
        BoardLayout board,
        IReadOnlyList<Guid> playerIds,
        Rng rng,
        List<DevelopmentCard> deck)
    {
        Options = options;
        Board = board;
        Rng = rng;
        _deck = deck;
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

    /// <summary>تعداد کارت باقی‌مانده در دسته‌ی توسعه.</summary>
    public int DevelopmentDeckCount => _deck.Count;

    /// <summary>پیشنهاد معامله‌ی روی میز، اگر باشد.</summary>
    public TradeOffer? PendingTrade { get; internal set; }

    public int? LongestRoadHolder => Players.FirstOrDefault(p => p.HasLongestRoad)?.Index;

    public int? LargestArmyHolder => Players.FirstOrDefault(p => p.HasLargestArmy)?.Index;

    public bool IsSetup => Phase is TurnPhase.SetupSettlement or TurnPhase.SetupRoad;

    /// <summary>
    /// بازی تازه. اگر <paramref name="layout"/> داده شود همان برد استفاده می‌شود
    /// (برد سفارشی، فاز ۷)؛ وگرنه از روی seed تولید می‌شود.
    /// </summary>
    public static GameState Create(
        GameOptions options,
        IReadOnlyList<Guid> playerIds,
        BoardLayout? layout = null)
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

        var board = layout ?? BoardGenerator.Generate(options.BoardRadius, options.Seed);

        // مولد تاس از seed دیگری شروع می‌شود تا چیدمان برد دنباله‌ی تاس‌ها را لو ندهد.
        var rng = new Rng(options.Seed ^ 0xA5A5_5A5A_C3C3_3C3CUL);

        // دسته‌ی توسعه هم مولد جدا دارد تا ترتیبش با تعداد تاس‌های ریخته‌شده جابه‌جا نشود.
        var deck = DevelopmentDeck.Build(board.Tiles.Count, new Rng(options.Seed ^ 0x1234_5678_9ABC_DEF0UL));

        return new GameState(options, board, playerIds, rng, deck);
    }

    /// <summary>عکس کامل وضعیت برای ذخیره‌سازی.</summary>
    public GameSnapshot ToSnapshot() => new()
    {
        Options = Options,
        Tiles = [.. Board.Tiles.Select(t => new TileSnapshot(t.Position.Q, t.Position.R, t.Terrain, t.Number))],
        Ports = [.. Board.Ports.Select(p => new PortSnapshot(p.Edge.Hex.Q, p.Edge.Hex.R, p.Edge.Side, p.Resource))],
        Players = [.. Players.Select(ToSnapshot)],
        Buildings = [.. _buildings.Select(b =>
            new BuildingSnapshot(b.Key.Hex.Q, b.Key.Hex.R, b.Key.Corner, b.Value.PlayerIndex, b.Value.Kind))],
        Roads = [.. _roads.Select(r => new RoadSnapshot(r.Key.Hex.Q, r.Key.Hex.R, r.Key.Side, r.Value))],
        Bank = new Dictionary<Resource, int>(_bank),
        Deck = [.. _deck],
        Robber = new HexSnapshot(Robber.Q, Robber.R),
        Phase = Phase,
        CurrentPlayer = CurrentPlayer,
        TurnNumber = TurnNumber,
        Version = Version,
        Die1 = Die1,
        Die2 = Die2,
        Winner = Winner,
        SetupStep = SetupStep,
        LastSetupSettlement = LastSetupSettlement is { } v ? new VertexSnapshot(v.Hex.Q, v.Hex.R, v.Corner) : null,
        PendingDiscards = new Dictionary<int, int>(_pendingDiscards),
        RngState = Rng.State,
        PendingTrade = PendingTrade is { } trade
            ? new TradeOfferSnapshot
            {
                Proposer = trade.Proposer,
                Give = new Dictionary<Resource, int>(trade.Give),
                Take = new Dictionary<Resource, int>(trade.Take),
                Responses = new Dictionary<int, TradeResponse>(trade.Responses)
            }
            : null
    };

    /// <summary>ساخت دوباره‌ی وضعیت از روی عکس ذخیره‌شده.</summary>
    public static GameState Restore(GameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.SchemaVersion != GameSnapshot.CurrentSchemaVersion)
        {
            throw new NotSupportedException(
                $"نسخه‌ی {snapshot.SchemaVersion} از عکس بازی پشتیبانی نمی‌شود.");
        }

        var board = new BoardLayout(
            snapshot.Tiles.Select(t => new HexTile(new Axial(t.Q, t.R), t.Terrain, t.Number)),
            snapshot.Ports.Select(p => new Port(EdgeId.Of(new Axial(p.Q, p.R), p.Side), p.Resource)));

        var players = snapshot.Players.OrderBy(p => p.Index).ToList();

        var state = new GameState(
            snapshot.Options,
            board,
            [.. players.Select(p => p.Id)],
            Rng.FromState(snapshot.RngState),
            [.. snapshot.Deck]);

        for (var i = 0; i < players.Count; i++)
        {
            state.Players[i].RestoreFrom(players[i]);
        }

        foreach (var building in snapshot.Buildings)
        {
            state._buildings[VertexId.Of(new Axial(building.Q, building.R), building.Corner)] =
                new Building(building.PlayerIndex, building.Kind);
        }

        foreach (var road in snapshot.Roads)
        {
            state._roads[EdgeId.Of(new Axial(road.Q, road.R), road.Side)] = road.PlayerIndex;
        }

        foreach (var (resource, amount) in snapshot.Bank)
        {
            state._bank[resource] = amount;
        }

        foreach (var (playerIndex, amount) in snapshot.PendingDiscards)
        {
            state._pendingDiscards[playerIndex] = amount;
        }

        state.Robber = new Axial(snapshot.Robber.Q, snapshot.Robber.R);
        state.Phase = snapshot.Phase;
        state.CurrentPlayer = snapshot.CurrentPlayer;
        state.TurnNumber = snapshot.TurnNumber;
        state.Version = snapshot.Version;
        state.Die1 = snapshot.Die1;
        state.Die2 = snapshot.Die2;
        state.Winner = snapshot.Winner;
        state.SetupStep = snapshot.SetupStep;
        state.LastSetupSettlement = snapshot.LastSetupSettlement is { } v
            ? VertexId.Of(new Axial(v.Q, v.R), v.Corner)
            : null;

        if (snapshot.PendingTrade is { } trade)
        {
            state.PendingTrade = TradeOffer.Restore(trade);
        }

        return state;
    }

    private static PlayerSnapshot ToSnapshot(PlayerState player) => new()
    {
        Index = player.Index,
        Id = player.Id,
        Resources = new Dictionary<Resource, int>(player.Resources),
        DevelopmentCards = new Dictionary<DevelopmentCard, int>(player.DevelopmentCards),
        NewDevelopmentCards = new Dictionary<DevelopmentCard, int>(player.NewDevelopmentCards),
        SettlementsLeft = player.SettlementsLeft,
        CitiesLeft = player.CitiesLeft,
        RoadsLeft = player.RoadsLeft,
        BuildingPoints = player.BuildingPoints,
        VictoryPointCards = player.VictoryPointCards,
        HasLongestRoad = player.HasLongestRoad,
        HasLargestArmy = player.HasLargestArmy,
        LongestRoadLength = player.LongestRoadLength,
        KnightsPlayed = player.KnightsPlayed,
        PlayedDevelopmentCardThisTurn = player.PlayedDevelopmentCardThisTurn
    };

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

    /// <summary>فقط برای پس‌گرفتن جاده‌ای که وسط یک حرکتِ ردشده گذاشته شده بود.</summary>
    internal void RemoveRoad(EdgeId edge) => _roads.Remove(edge);

    internal int BankOf(Resource resource) => _bank[resource];

    internal void BankTake(Resource resource, int amount) => _bank[resource] -= amount;

    internal void BankReturn(Resource resource, int amount) => _bank[resource] += amount;

    /// <summary>کارت بعدی دسته بدون برداشتنش.</summary>
    internal DevelopmentCard PeekDevelopmentCard() => _deck[^1];

    internal DevelopmentCard DrawDevelopmentCard()
    {
        var card = _deck[^1];
        _deck.RemoveAt(_deck.Count - 1);
        return card;
    }

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
