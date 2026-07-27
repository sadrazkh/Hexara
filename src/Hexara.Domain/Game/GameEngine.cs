using Hexara.Domain.Board;

namespace Hexara.Domain.Game;

/// <summary>
/// تنها جایی که وضعیت بازی تغییر می‌کند.
///
/// هر حرکت اول کامل اعتبارسنجی می‌شود؛ اگر رد شود هیچ تغییری روی وضعیت اعمال
/// نشده است، پس فراخوان می‌تواند بی‌خیال شود و وضعیت همچنان سالم است.
/// </summary>
public static class GameEngine
{
    public static MoveResult Apply(GameState state, GameAction action)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(action);

        if (state.Phase == TurnPhase.GameOver)
        {
            return MoveResult.Fail(GameError.GameFinished);
        }

        if (action.PlayerIndex < 0 || action.PlayerIndex >= state.Players.Count)
        {
            return MoveResult.Fail(GameError.NotYourTurn);
        }

        var result = action switch
        {
            PlaceInitialSettlement a => PlaceSetupSettlement(state, a),
            PlaceInitialRoad a => PlaceSetupRoad(state, a),
            RollDice a => Roll(state, a),
            DiscardCards a => Discard(state, a),
            MoveRobber a => MoveTheRobber(state, a),
            BuildRoad a => Build(state, a),
            BuildSettlement a => Build(state, a),
            BuildCity a => Build(state, a),
            EndTurn a => FinishTurn(state, a),
            _ => MoveResult.Fail(GameError.WrongPhase)
        };

        if (result.Success)
        {
            state.Version++;
        }

        return result;
    }

    // ── چیدمان اولیه ─────────────────────────────────────────────────────

    private static MoveResult PlaceSetupSettlement(GameState state, PlaceInitialSettlement action)
    {
        if (state.Phase != TurnPhase.SetupSettlement)
        {
            return MoveResult.Fail(GameError.WrongPhase);
        }

        if (action.PlayerIndex != state.CurrentPlayer)
        {
            return MoveResult.Fail(GameError.NotYourTurn);
        }

        var error = ValidateSettlementSpot(state, action.Vertex);
        if (error != GameError.None)
        {
            return MoveResult.Fail(error);
        }

        var player = state.Player(action.PlayerIndex);
        state.PlaceBuilding(action.Vertex, new Building(action.PlayerIndex, BuildingKind.Settlement));
        player.SettlementsLeft--;
        player.VictoryPoints++;

        var events = new List<GameEvent> { new SetupSettlementPlaced(action.PlayerIndex, action.Vertex) };

        // آبادی دور دوم منابع خانه‌های مجاورش را همان لحظه می‌دهد.
        if (state.SetupStep >= state.Options.PlayerCount)
        {
            var grants = GrantStartingResources(state, action.PlayerIndex, action.Vertex);
            if (grants.Count > 0)
            {
                events.Add(new ResourcesProduced(grants));
            }
        }

        state.LastSetupSettlement = action.Vertex;
        state.Phase = TurnPhase.SetupRoad;
        return MoveResult.Ok(events);
    }

    private static MoveResult PlaceSetupRoad(GameState state, PlaceInitialRoad action)
    {
        if (state.Phase != TurnPhase.SetupRoad)
        {
            return MoveResult.Fail(GameError.WrongPhase);
        }

        if (action.PlayerIndex != state.CurrentPlayer)
        {
            return MoveResult.Fail(GameError.NotYourTurn);
        }

        if (!state.Board.ContainsEdge(action.Edge))
        {
            return MoveResult.Fail(GameError.EdgeNotOnBoard);
        }

        if (state.RoadAt(action.Edge) is not null)
        {
            return MoveResult.Fail(GameError.EdgeOccupied);
        }

        if (state.LastSetupSettlement is not { } settlement
            || !action.Edge.Endpoints().Contains(settlement))
        {
            return MoveResult.Fail(GameError.SetupRoadMustTouchSettlement);
        }

        state.PlaceRoad(action.Edge, action.PlayerIndex);
        state.Player(action.PlayerIndex).RoadsLeft--;

        var events = new List<GameEvent> { new SetupRoadPlaced(action.PlayerIndex, action.Edge) };

        state.LastSetupSettlement = null;
        state.SetupStep++;

        if (state.SetupStep >= state.SetupOrder.Count)
        {
            state.Phase = TurnPhase.Roll;
            state.CurrentPlayer = 0;
            state.TurnNumber = 1;
            events.Add(new SetupCompleted());
            events.Add(new TurnStarted(0, 1));
        }
        else
        {
            state.CurrentPlayer = state.SetupOrder[state.SetupStep];
            state.Phase = TurnPhase.SetupSettlement;
        }

        return MoveResult.Ok(events);
    }

    private static List<ResourceGrant> GrantStartingResources(GameState state, int playerIndex, VertexId vertex)
    {
        var grants = new List<ResourceGrant>();
        var player = state.Player(playerIndex);

        foreach (var hex in vertex.TouchingHexes())
        {
            if (state.Board.TileAt(hex) is not { } tile || tile.Resource is not { } resource)
            {
                continue;
            }

            if (state.BankOf(resource) <= 0)
            {
                continue;
            }

            state.BankTake(resource, 1);
            player.Add(resource, 1);
            grants.Add(new ResourceGrant(playerIndex, resource, 1));
        }

        return grants;
    }

    // ── تاس و تولید ──────────────────────────────────────────────────────

    private static MoveResult Roll(GameState state, RollDice action)
    {
        if (state.Phase != TurnPhase.Roll)
        {
            return MoveResult.Fail(GameError.WrongPhase);
        }

        if (action.PlayerIndex != state.CurrentPlayer)
        {
            return MoveResult.Fail(GameError.NotYourTurn);
        }

        var die1 = state.Rng.RollDie();
        var die2 = state.Rng.RollDie();
        state.Die1 = die1;
        state.Die2 = die2;

        var events = new List<GameEvent> { new DiceRolled(action.PlayerIndex, die1, die2) };

        if (die1 + die2 == 7)
        {
            events.AddRange(StartRobberSequence(state));
            return MoveResult.Ok(events);
        }

        events.AddRange(Produce(state, die1 + die2));
        state.Phase = TurnPhase.Main;
        return MoveResult.Ok(events);
    }

    /// <summary>
    /// توزیع منابع یک تاس. اگر بانک برای یک منبع کم بیاورد و بیش از یک بازیکن
    /// طلبکار باشد، طبق قانون هیچ‌کس آن منبع را نمی‌گیرد.
    /// </summary>
    private static List<GameEvent> Produce(GameState state, int roll)
    {
        var demand = new Dictionary<Resource, Dictionary<int, int>>();

        foreach (var tile in state.Board.TilesWithNumber(roll))
        {
            if (tile.Position == state.Robber || tile.Resource is not { } resource)
            {
                continue;
            }

            foreach (var vertex in tile.Vertices())
            {
                if (state.BuildingAt(vertex) is not { } building)
                {
                    continue;
                }

                var perPlayer = demand.TryGetValue(resource, out var existing)
                    ? existing
                    : demand[resource] = [];

                perPlayer[building.PlayerIndex] = perPlayer.GetValueOrDefault(building.PlayerIndex) + building.Yield;
            }
        }

        var events = new List<GameEvent>();
        var grants = new List<ResourceGrant>();

        foreach (var (resource, perPlayer) in demand)
        {
            var total = perPlayer.Values.Sum();
            var available = state.BankOf(resource);

            if (total > available && perPlayer.Count > 1)
            {
                events.Add(new ProductionSkippedForBank(resource));
                continue;
            }

            foreach (var (playerIndex, requested) in perPlayer)
            {
                var amount = Math.Min(requested, state.BankOf(resource));
                if (amount <= 0)
                {
                    continue;
                }

                state.BankTake(resource, amount);
                state.Player(playerIndex).Add(resource, amount);
                grants.Add(new ResourceGrant(playerIndex, resource, amount));
            }
        }

        if (grants.Count > 0)
        {
            events.Insert(0, new ResourcesProduced(grants));
        }

        return events;
    }

    // ── دزد ──────────────────────────────────────────────────────────────

    private static List<GameEvent> StartRobberSequence(GameState state)
    {
        var events = new List<GameEvent>();
        var amounts = new Dictionary<int, int>();

        foreach (var player in state.Players)
        {
            if (player.TotalCards > state.Options.DiscardLimit)
            {
                var amount = player.TotalCards / 2;
                amounts[player.Index] = amount;
                state.SetPendingDiscard(player.Index, amount);
            }
        }

        if (amounts.Count > 0)
        {
            state.Phase = TurnPhase.Discard;
            events.Add(new DiscardRequired(amounts));
        }
        else
        {
            state.Phase = TurnPhase.MoveRobber;
        }

        return events;
    }

    private static MoveResult Discard(GameState state, DiscardCards action)
    {
        if (state.Phase != TurnPhase.Discard)
        {
            return MoveResult.Fail(GameError.WrongPhase);
        }

        if (!state.PendingDiscards.TryGetValue(action.PlayerIndex, out var required))
        {
            return MoveResult.Fail(GameError.NothingToDiscard);
        }

        var cards = action.Cards.Where(c => c.Value > 0).ToDictionary(c => c.Key, c => c.Value);
        if (cards.Values.Sum() != required)
        {
            return MoveResult.Fail(GameError.WrongDiscardAmount);
        }

        var player = state.Player(action.PlayerIndex);
        if (cards.Any(c => player[c.Key] < c.Value))
        {
            return MoveResult.Fail(GameError.NotEnoughCardsToDiscard);
        }

        foreach (var (resource, amount) in cards)
        {
            player.Remove(resource, amount);
            state.BankReturn(resource, amount);
        }

        state.ClearPendingDiscard(action.PlayerIndex);

        var events = new List<GameEvent> { new CardsDiscarded(action.PlayerIndex, cards) };
        if (state.PendingDiscards.Count == 0)
        {
            state.Phase = TurnPhase.MoveRobber;
        }

        return MoveResult.Ok(events);
    }

    private static MoveResult MoveTheRobber(GameState state, MoveRobber action)
    {
        if (state.Phase != TurnPhase.MoveRobber)
        {
            return MoveResult.Fail(GameError.WrongPhase);
        }

        if (action.PlayerIndex != state.CurrentPlayer)
        {
            return MoveResult.Fail(GameError.NotYourTurn);
        }

        if (!state.Board.HasTile(action.Hex))
        {
            return MoveResult.Fail(GameError.HexNotOnBoard);
        }

        if (action.Hex == state.Robber)
        {
            return MoveResult.Fail(GameError.RobberMustChangeHex);
        }

        var victims = RobberVictims(state, action.Hex, action.PlayerIndex).ToList();

        if (action.Victim is { } victim)
        {
            if (!victims.Contains(victim))
            {
                return MoveResult.Fail(GameError.InvalidVictim);
            }
        }
        else if (victims.Count > 0)
        {
            return MoveResult.Fail(GameError.VictimRequired);
        }

        var from = state.Robber;
        state.Robber = action.Hex;

        var events = new List<GameEvent> { new RobberMoved(action.PlayerIndex, from, action.Hex) };

        if (action.Victim is { } target)
        {
            var stolen = StealRandomCard(state, action.PlayerIndex, target);
            events.Add(new ResourceStolen(action.PlayerIndex, target, stolen));
        }

        state.Phase = TurnPhase.Main;
        return MoveResult.Ok(events);
    }

    /// <summary>بازیکنانی که ساختمانی کنار این خانه دارند، کارت دارند و مصون نیستند.</summary>
    public static IEnumerable<int> RobberVictims(GameState state, Axial hex, int moverIndex)
    {
        if (state.Board.TileAt(hex) is not { } tile)
        {
            return [];
        }

        return tile.Vertices()
            .Select(state.BuildingAt)
            .Where(b => b is not null)
            .Select(b => b!.PlayerIndex)
            .Distinct()
            .Where(index => index != moverIndex
                && state.Player(index).TotalCards > 0
                && !IsProtectedByFriendlyRobber(state, index));
    }

    private static bool IsProtectedByFriendlyRobber(GameState state, int playerIndex) =>
        state.Options.FriendlyRobber
        && state.Player(playerIndex).VictoryPoints <= state.Options.FriendlyRobberThreshold;

    private static Resource StealRandomCard(GameState state, int thiefIndex, int victimIndex)
    {
        var victim = state.Player(victimIndex);

        // یک کارت تصادفی از دست قربانی: انتخاب به نسبت تعداد هر منبع.
        var pick = state.Rng.Next(victim.TotalCards);
        foreach (var resource in TerrainExtensions.AllResources)
        {
            pick -= victim[resource];
            if (pick < 0)
            {
                victim.Remove(resource, 1);
                state.Player(thiefIndex).Add(resource, 1);
                return resource;
            }
        }

        throw new InvalidOperationException("دست قربانی با تعداد کارت‌هایش نمی‌خواند.");
    }

    // ── ساخت‌وساز ────────────────────────────────────────────────────────

    private static MoveResult Build(GameState state, GameAction action)
    {
        if (state.Phase != TurnPhase.Main)
        {
            return MoveResult.Fail(GameError.WrongPhase);
        }

        if (action.PlayerIndex != state.CurrentPlayer)
        {
            return MoveResult.Fail(GameError.NotYourTurn);
        }

        return action switch
        {
            BuildRoad a => BuildRoadAt(state, a),
            BuildSettlement a => BuildSettlementAt(state, a),
            BuildCity a => BuildCityAt(state, a),
            _ => MoveResult.Fail(GameError.WrongPhase)
        };
    }

    private static MoveResult BuildRoadAt(GameState state, BuildRoad action)
    {
        var player = state.Player(action.PlayerIndex);

        if (!state.Board.ContainsEdge(action.Edge))
        {
            return MoveResult.Fail(GameError.EdgeNotOnBoard);
        }

        if (state.RoadAt(action.Edge) is not null)
        {
            return MoveResult.Fail(GameError.EdgeOccupied);
        }

        if (player.RoadsLeft <= 0)
        {
            return MoveResult.Fail(GameError.NoPiecesLeft);
        }

        if (!IsRoadConnected(state, action.PlayerIndex, action.Edge))
        {
            return MoveResult.Fail(GameError.RoadNotConnected);
        }

        if (!player.CanAfford(BuildCosts.Road))
        {
            return MoveResult.Fail(GameError.NotEnoughResources);
        }

        PayToBank(state, player, BuildCosts.Road);
        state.PlaceRoad(action.Edge, action.PlayerIndex);
        player.RoadsLeft--;

        return MoveResult.Ok(new RoadBuilt(action.PlayerIndex, action.Edge));
    }

    private static MoveResult BuildSettlementAt(GameState state, BuildSettlement action)
    {
        var player = state.Player(action.PlayerIndex);

        var error = ValidateSettlementSpot(state, action.Vertex);
        if (error != GameError.None)
        {
            return MoveResult.Fail(error);
        }

        if (player.SettlementsLeft <= 0)
        {
            return MoveResult.Fail(GameError.NoPiecesLeft);
        }

        // بیرون از چیدمان اولیه، آبادی باید به جاده‌ی خود بازیکن بچسبد.
        if (!action.Vertex.TouchingEdges().Any(e => state.RoadAt(e) == action.PlayerIndex))
        {
            return MoveResult.Fail(GameError.SettlementNotConnectedToRoad);
        }

        if (!player.CanAfford(BuildCosts.Settlement))
        {
            return MoveResult.Fail(GameError.NotEnoughResources);
        }

        PayToBank(state, player, BuildCosts.Settlement);
        state.PlaceBuilding(action.Vertex, new Building(action.PlayerIndex, BuildingKind.Settlement));
        player.SettlementsLeft--;
        player.VictoryPoints++;

        var events = new List<GameEvent> { new SettlementBuilt(action.PlayerIndex, action.Vertex) };
        AddVictoryIfWon(state, action.PlayerIndex, events);
        return MoveResult.Ok(events);
    }

    private static MoveResult BuildCityAt(GameState state, BuildCity action)
    {
        var player = state.Player(action.PlayerIndex);

        if (!state.Board.ContainsVertex(action.Vertex))
        {
            return MoveResult.Fail(GameError.VertexNotOnBoard);
        }

        if (state.BuildingAt(action.Vertex) is not { } building)
        {
            return MoveResult.Fail(GameError.NotASettlement);
        }

        if (building.PlayerIndex != action.PlayerIndex)
        {
            return MoveResult.Fail(GameError.NotYourSettlement);
        }

        if (building.Kind != BuildingKind.Settlement)
        {
            return MoveResult.Fail(GameError.NotASettlement);
        }

        if (player.CitiesLeft <= 0)
        {
            return MoveResult.Fail(GameError.NoPiecesLeft);
        }

        if (!player.CanAfford(BuildCosts.City))
        {
            return MoveResult.Fail(GameError.NotEnoughResources);
        }

        PayToBank(state, player, BuildCosts.City);
        state.PlaceBuilding(action.Vertex, new Building(action.PlayerIndex, BuildingKind.City));
        player.CitiesLeft--;
        player.SettlementsLeft++; // آبادی به بازیکن برمی‌گردد.
        player.VictoryPoints++;

        var events = new List<GameEvent> { new CityBuilt(action.PlayerIndex, action.Vertex) };
        AddVictoryIfWon(state, action.PlayerIndex, events);
        return MoveResult.Ok(events);
    }

    private static MoveResult FinishTurn(GameState state, EndTurn action)
    {
        if (state.Phase != TurnPhase.Main)
        {
            return MoveResult.Fail(GameError.WrongPhase);
        }

        if (action.PlayerIndex != state.CurrentPlayer)
        {
            return MoveResult.Fail(GameError.NotYourTurn);
        }

        state.Die1 = null;
        state.Die2 = null;
        state.CurrentPlayer = (state.CurrentPlayer + 1) % state.Players.Count;
        state.TurnNumber++;
        state.Phase = TurnPhase.Roll;

        return MoveResult.Ok(new TurnStarted(state.CurrentPlayer, state.TurnNumber));
    }

    // ── قواعد مشترک ──────────────────────────────────────────────────────

    /// <summary>گوشه باید روی برد، خالی و دور از هر ساختمان دیگری باشد (قاعده‌ی فاصله).</summary>
    private static GameError ValidateSettlementSpot(GameState state, VertexId vertex)
    {
        if (!state.Board.ContainsVertex(vertex))
        {
            return GameError.VertexNotOnBoard;
        }

        if (state.BuildingAt(vertex) is not null)
        {
            return GameError.VertexOccupied;
        }

        if (vertex.AdjacentVertices().Any(v => state.BuildingAt(v) is not null))
        {
            return GameError.TooCloseToAnotherBuilding;
        }

        return GameError.None;
    }

    /// <summary>
    /// جاده وقتی مجاز است که از یکی از دو سرش به دارایی خود بازیکن وصل شود؛ اما
    /// نمی‌توان از «داخل» آبادی یا شهر حریف رد شد.
    /// </summary>
    private static bool IsRoadConnected(GameState state, int playerIndex, EdgeId edge)
    {
        foreach (var vertex in edge.Endpoints())
        {
            var building = state.BuildingAt(vertex);
            if (building is not null)
            {
                if (building.PlayerIndex == playerIndex)
                {
                    return true;
                }

                continue; // ساختمان حریف مسیر را می‌بندد.
            }

            if (vertex.TouchingEdges().Any(e => e != edge && state.RoadAt(e) == playerIndex))
            {
                return true;
            }
        }

        return false;
    }

    private static void PayToBank(GameState state, PlayerState player, IReadOnlyDictionary<Resource, int> cost)
    {
        player.Pay(cost);
        foreach (var (resource, amount) in cost)
        {
            state.BankReturn(resource, amount);
        }
    }

    private static void AddVictoryIfWon(GameState state, int playerIndex, List<GameEvent> events)
    {
        var player = state.Player(playerIndex);
        if (player.VictoryPoints < state.Options.VictoryPoints)
        {
            return;
        }

        state.Winner = playerIndex;
        state.Phase = TurnPhase.GameOver;
        events.Add(new GameWon(playerIndex, player.VictoryPoints));
    }

    // ── پرس‌وجوهای کمکی (رابط کاربری فاز ۶ و بات فاز ۸) ──────────────────

    /// <summary>گوشه‌هایی که این بازیکن الان می‌تواند روی آن‌ها آبادی بسازد.</summary>
    public static IEnumerable<VertexId> LegalSettlementVertices(GameState state, int playerIndex)
    {
        var requiresRoad = !state.IsSetup;

        return state.Board.Vertices.Where(v =>
            ValidateSettlementSpot(state, v) == GameError.None
            && (!requiresRoad || v.TouchingEdges().Any(e => state.RoadAt(e) == playerIndex)));
    }

    /// <summary>ضلع‌هایی که این بازیکن الان می‌تواند روی آن‌ها جاده بسازد.</summary>
    public static IEnumerable<EdgeId> LegalRoadEdges(GameState state, int playerIndex) =>
        state.Board.Edges.Where(e => state.RoadAt(e) is null && IsRoadConnected(state, playerIndex, e));
}
