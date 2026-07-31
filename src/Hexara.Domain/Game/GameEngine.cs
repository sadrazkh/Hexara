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
    /// <summary>
    /// یک حرکت را اعمال می‌کند.
    ///
    /// ‎now‎ اختیاری است و دامنه هرگز خودش ساعت نمی‌خواند. تنها چیزی که به آن
    /// نیاز دارد مهلتِ پیشنهاد معامله است، و همان هم باید از بیرون بیاید تا
    /// بازپخشِ یک بازیِ ذخیره‌شده همان نتیجه را بدهد — همان دلیلی که ‎Rng‎ را
    /// دست‌ساز نگه داشته. نداشتنش یعنی «بی‌مهلت»، نه «همین حالا».
    /// </summary>
    public static MoveResult Apply(GameState state, GameAction action, DateTimeOffset? now = null)
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
            BuyDevelopmentCard a => BuyCard(state, a),
            PlayKnight a => UseKnight(state, a),
            PlayRoadBuilding a => UseRoadBuilding(state, a),
            PlayYearOfPlenty a => UseYearOfPlenty(state, a),
            PlayMonopoly a => UseMonopoly(state, a),
            MaritimeTrade a => TradeWithBank(state, a),
            ProposeTrade a => OfferTrade(state, a, now),
            RespondToTrade a => AnswerTrade(state, a, now),
            CounterTrade a => OfferCounter(state, a, now),
            ConfirmTrade a => SettleTrade(state, a),
            CancelTrade a => WithdrawTrade(state, a),
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
        player.BuildingPoints++;

        var events = new List<GameEvent> { new SetupSettlementPlaced(action.PlayerIndex, action.Vertex) };
        AnnouncePorts(state, action.PlayerIndex, action.Vertex, events);

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

        RecomputeLongestRoad(state, events);
        return MoveResult.Ok(events);
    }

    /// <summary>
    /// اگر این گوشه روی بندری باشد، گرفتنش را اعلام می‌کند.
    ///
    /// فقط برای آبادی صدا زده می‌شود نه شهر: شهر روی آبادیِ خودت ساخته می‌شود، پس
    /// بندری که با شهر می‌آید همان لحظه‌ی ساختِ آبادی اعلام شده بود.
    /// </summary>
    private static void AnnouncePorts(GameState state, int playerIndex, VertexId vertex, List<GameEvent> events)
    {
        foreach (var port in state.Board.Ports.Where(p => p.Vertices().Contains(vertex)))
        {
            events.Add(new PortTaken(playerIndex, port.Resource, port.Rate));
        }
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

        var events = new List<GameEvent>();
        var error = ExecuteRobber(state, action.PlayerIndex, action.Hex, action.Victim, events);
        if (error != GameError.None)
        {
            return MoveResult.Fail(error);
        }

        state.Phase = TurnPhase.Main;
        return MoveResult.Ok(events);
    }

    /// <summary>
    /// جابه‌جایی دزد و دزدی — هم از تاس ۷ و هم از کارت شوالیه به اینجا می‌رسیم، پس
    /// مرحله‌ی بازی را عمداً دست نمی‌زند و تصمیمش با فراخوان است.
    /// </summary>
    private static GameError ExecuteRobber(
        GameState state,
        int playerIndex,
        Axial hex,
        int? victim,
        List<GameEvent> events)
    {
        if (!state.Board.HasTile(hex))
        {
            return GameError.HexNotOnBoard;
        }

        if (hex == state.Robber)
        {
            return GameError.RobberMustChangeHex;
        }

        var candidates = RobberVictims(state, hex, playerIndex).ToList();

        if (victim is { } chosen)
        {
            if (!candidates.Contains(chosen))
            {
                return GameError.InvalidVictim;
            }
        }
        else if (candidates.Count > 0)
        {
            return GameError.VictimRequired;
        }

        var from = state.Robber;
        state.Robber = hex;
        events.Add(new RobberMoved(playerIndex, from, hex));

        if (victim is { } target)
        {
            var stolen = StealRandomCard(state, playerIndex, target);
            events.Add(new ResourceStolen(playerIndex, target, stolen));
        }

        return GameError.None;
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
                && state.Options.Teams?.AreTeammates(moverIndex, index) != true
                && !IsProtectedByFriendlyRobber(state, index));
    }

    // امتیاز عمومی مبناست، نه امتیاز واقعی: مصونیت نباید کارت‌های پنهان را لو بدهد.
    private static bool IsProtectedByFriendlyRobber(GameState state, int playerIndex) =>
        state.Options.FriendlyRobber
        && state.Player(playerIndex).PublicVictoryPoints <= state.Options.FriendlyRobberThreshold;

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

        var error = ValidateRoadSpot(state, action.PlayerIndex, action.Edge);
        if (error != GameError.None)
        {
            return MoveResult.Fail(error);
        }

        if (!player.CanAfford(BuildCosts.Road))
        {
            return MoveResult.Fail(GameError.NotEnoughResources);
        }

        PayToBank(state, player, BuildCosts.Road);
        state.PlaceRoad(action.Edge, action.PlayerIndex);
        player.RoadsLeft--;

        var events = new List<GameEvent> { new RoadBuilt(action.PlayerIndex, action.Edge) };
        RecomputeLongestRoad(state, events);
        CheckVictory(state, action.PlayerIndex, events);
        return MoveResult.Ok(events);
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
        player.BuildingPoints++;

        var events = new List<GameEvent> { new SettlementBuilt(action.PlayerIndex, action.Vertex) };
        AnnouncePorts(state, action.PlayerIndex, action.Vertex, events);

        // آبادی تازه ممکن است جاده‌ی حریف را از وسط قطع کند.
        RecomputeLongestRoad(state, events);
        CheckVictory(state, action.PlayerIndex, events);
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
        player.BuildingPoints++;

        var events = new List<GameEvent> { new CityBuilt(action.PlayerIndex, action.Vertex) };
        CheckVictory(state, action.PlayerIndex, events);
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

        var player = state.Player(action.PlayerIndex);
        player.ReleaseNewDevelopmentCards();
        player.PlayedDevelopmentCardThisTurn = false;

        state.PendingTrade = null;
        state.Die1 = null;
        state.Die2 = null;
        state.CurrentPlayer = (state.CurrentPlayer + 1) % state.Players.Count;
        state.TurnNumber++;
        state.Phase = TurnPhase.Roll;

        return MoveResult.Ok(new TurnStarted(state.CurrentPlayer, state.TurnNumber));
    }

    // ── کارت توسعه ───────────────────────────────────────────────────────

    private static MoveResult BuyCard(GameState state, BuyDevelopmentCard action)
    {
        if (state.Phase != TurnPhase.Main)
        {
            return MoveResult.Fail(GameError.WrongPhase);
        }

        if (action.PlayerIndex != state.CurrentPlayer)
        {
            return MoveResult.Fail(GameError.NotYourTurn);
        }

        if (state.DevelopmentDeckCount == 0)
        {
            return MoveResult.Fail(GameError.DevelopmentDeckEmpty);
        }

        var player = state.Player(action.PlayerIndex);
        if (!player.CanAfford(BuildCosts.DevelopmentCard))
        {
            return MoveResult.Fail(GameError.NotEnoughResources);
        }

        PayToBank(state, player, BuildCosts.DevelopmentCard);
        var card = state.DrawDevelopmentCard();
        player.AddNewDevelopmentCard(card);

        var events = new List<GameEvent> { new DevelopmentCardBought(action.PlayerIndex, card) };

        // کارت امتیاز بازی نمی‌شود؛ از همان لحظه‌ی خرید امتیاز می‌دهد و می‌تواند بازی را تمام کند.
        if (card == DevelopmentCard.VictoryPoint)
        {
            player.VictoryPointCards++;
            CheckVictory(state, action.PlayerIndex, events);
        }

        return MoveResult.Ok(events);
    }

    private static MoveResult UseKnight(GameState state, PlayKnight action)
    {
        var error = ValidateDevelopmentPlay(state, action.PlayerIndex, DevelopmentCard.Knight);
        if (error != GameError.None)
        {
            return MoveResult.Fail(error);
        }

        var events = new List<GameEvent>();
        error = ExecuteRobber(state, action.PlayerIndex, action.Hex, action.Victim, events);
        if (error != GameError.None)
        {
            return MoveResult.Fail(error);
        }

        var player = state.Player(action.PlayerIndex);
        player.RemoveDevelopmentCard(DevelopmentCard.Knight);
        player.PlayedDevelopmentCardThisTurn = true;
        player.KnightsPlayed++;

        events.Insert(0, new KnightPlayed(action.PlayerIndex, player.KnightsPlayed));
        RecomputeLargestArmy(state, events);
        CheckVictory(state, action.PlayerIndex, events);
        return MoveResult.Ok(events);
    }

    private static MoveResult UseRoadBuilding(GameState state, PlayRoadBuilding action)
    {
        var error = ValidateDevelopmentPlay(state, action.PlayerIndex, DevelopmentCard.RoadBuilding);
        if (error != GameError.None)
        {
            return MoveResult.Fail(error);
        }

        var player = state.Player(action.PlayerIndex);

        error = ValidateRoadSpot(state, action.PlayerIndex, action.First);
        if (error != GameError.None)
        {
            return MoveResult.Fail(error);
        }

        // جاده‌ی دوم ممکن است فقط به لطف جاده‌ی اول قانونی باشد، پس ترتیبی گذاشته می‌شوند.
        state.PlaceRoad(action.First, action.PlayerIndex);
        player.RoadsLeft--;

        var placed = new List<EdgeId> { action.First };

        if (action.Second is { } second)
        {
            error = ValidateRoadSpot(state, action.PlayerIndex, second);
            if (error != GameError.None)
            {
                // برگرداندن جاده‌ی اول تا حرکتِ ردشده هیچ اثری نگذارد.
                state.RemoveRoad(action.First);
                player.RoadsLeft++;
                return MoveResult.Fail(error);
            }

            state.PlaceRoad(second, action.PlayerIndex);
            player.RoadsLeft--;
            placed.Add(second);
        }

        player.RemoveDevelopmentCard(DevelopmentCard.RoadBuilding);
        player.PlayedDevelopmentCardThisTurn = true;

        var events = new List<GameEvent> { new RoadBuildingPlayed(action.PlayerIndex, placed) };
        RecomputeLongestRoad(state, events);
        CheckVictory(state, action.PlayerIndex, events);
        return MoveResult.Ok(events);
    }

    private static MoveResult UseYearOfPlenty(GameState state, PlayYearOfPlenty action)
    {
        var error = ValidateDevelopmentPlay(state, action.PlayerIndex, DevelopmentCard.YearOfPlenty);
        if (error != GameError.None)
        {
            return MoveResult.Fail(error);
        }

        var wanted = new Dictionary<Resource, int> { [action.First] = 1 };
        wanted[action.Second] = wanted.GetValueOrDefault(action.Second) + 1;

        if (wanted.Any(w => state.BankOf(w.Key) < w.Value))
        {
            return MoveResult.Fail(GameError.BankCannotPay);
        }

        var player = state.Player(action.PlayerIndex);
        foreach (var (resource, amount) in wanted)
        {
            state.BankTake(resource, amount);
            player.Add(resource, amount);
        }

        player.RemoveDevelopmentCard(DevelopmentCard.YearOfPlenty);
        player.PlayedDevelopmentCardThisTurn = true;

        return MoveResult.Ok(new YearOfPlentyPlayed(action.PlayerIndex, action.First, action.Second));
    }

    private static MoveResult UseMonopoly(GameState state, PlayMonopoly action)
    {
        var error = ValidateDevelopmentPlay(state, action.PlayerIndex, DevelopmentCard.Monopoly);
        if (error != GameError.None)
        {
            return MoveResult.Fail(error);
        }

        var player = state.Player(action.PlayerIndex);
        var collected = 0;

        foreach (var other in state.Players.Where(p => p.Index != action.PlayerIndex))
        {
            var amount = other[action.Resource];
            if (amount == 0)
            {
                continue;
            }

            other.Remove(action.Resource, amount);
            player.Add(action.Resource, amount);
            collected += amount;
        }

        player.RemoveDevelopmentCard(DevelopmentCard.Monopoly);
        player.PlayedDevelopmentCardThisTurn = true;

        return MoveResult.Ok(new MonopolyPlayed(action.PlayerIndex, action.Resource, collected));
    }

    /// <summary>
    /// کارت‌هایی که این صندلی همین حالا می‌تواند بازی کند.
    ///
    /// عمومی است تا نما همین را بفرستد و رابط قاعده‌ی «کارتِ همین نوبت خریده
    /// نمی‌شود» و «هر نوبت یک کارت» را دوباره پیاده نکند — همان قاعده‌ای که
    /// برای نرخ بندر و جدول هزینه هم گذاشتیم.
    /// </summary>
    public static IReadOnlyList<DevelopmentCard> PlayableDevelopmentCards(GameState state, int playerIndex)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (playerIndex < 0 || playerIndex >= state.Players.Count)
        {
            return [];
        }

        return [.. Enum.GetValues<DevelopmentCard>()
            .Where(card => ValidateDevelopmentPlay(state, playerIndex, card) == GameError.None)];
    }

    private static GameError ValidateDevelopmentPlay(GameState state, int playerIndex, DevelopmentCard card)
    {
        // کارت توسعه را می‌توان قبل یا بعد از تاس بازی کرد، ولی نه وسط ماجرای دزد.
        if (state.Phase is not (TurnPhase.Roll or TurnPhase.Main))
        {
            return GameError.WrongPhase;
        }

        if (playerIndex != state.CurrentPlayer)
        {
            return GameError.NotYourTurn;
        }

        if (card == DevelopmentCard.VictoryPoint)
        {
            return GameError.VictoryPointCardIsNotPlayable;
        }

        var player = state.Player(playerIndex);
        if (player.PlayedDevelopmentCardThisTurn)
        {
            return GameError.AlreadyPlayedADevelopmentCard;
        }

        if (player[card] <= 0)
        {
            return player.NewDevelopmentCards.GetValueOrDefault(card) > 0
                ? GameError.CardBoughtThisTurn
                : GameError.NoSuchDevelopmentCard;
        }

        return GameError.None;
    }

    // ── معامله ───────────────────────────────────────────────────────────

    private static MoveResult TradeWithBank(GameState state, MaritimeTrade action)
    {
        if (state.Phase != TurnPhase.Main)
        {
            return MoveResult.Fail(GameError.WrongPhase);
        }

        if (action.PlayerIndex != state.CurrentPlayer)
        {
            return MoveResult.Fail(GameError.NotYourTurn);
        }

        if (action.Give == action.Take)
        {
            return MoveResult.Fail(GameError.CannotTradeTheSameResource);
        }

        var rate = MaritimeRate(state, action.PlayerIndex, action.Give);
        var player = state.Player(action.PlayerIndex);

        if (player[action.Give] < rate)
        {
            return MoveResult.Fail(GameError.NotEnoughResources);
        }

        if (state.BankOf(action.Take) < 1)
        {
            return MoveResult.Fail(GameError.BankCannotPay);
        }

        player.Remove(action.Give, rate);
        state.BankReturn(action.Give, rate);
        state.BankTake(action.Take, 1);
        player.Add(action.Take, 1);

        return MoveResult.Ok(new MaritimeTraded(action.PlayerIndex, action.Give, rate, action.Take));
    }

    /// <summary>بهترین نرخی که این بازیکن برای دادن این منبع دارد: ۲ با بندر اختصاصی، ۳ با بندر عمومی، وگرنه ۴.</summary>
    public static int MaritimeRate(GameState state, int playerIndex, Resource give)
    {
        var rate = state.Options.BankTradeRate;

        foreach (var port in state.Board.Ports)
        {
            if (!port.Vertices().Any(v => state.BuildingAt(v)?.PlayerIndex == playerIndex))
            {
                continue;
            }

            if (port.Resource is null || port.Resource == give)
            {
                rate = Math.Min(rate, port.Rate);
            }
        }

        return rate;
    }

    /// <summary>
    /// نرخِ همه‌ی منابع، با یک بار گشتن روی بندرها.
    ///
    /// نما نرخِ هر پنج منبع را برای *هر* بازیکن می‌خواهد. با صدا زدنِ
    /// <see cref="MaritimeRate"/> برای هرکدام، بندرها شش بار پشت سر هم پیموده
    /// می‌شدند — و این کار در هر بار ساختنِ نما، برای هر بازیکن، بعد از هر حرکت
    /// تکرار می‌شد. جوابش دقیقاً همان است، فقط یک بار.
    /// </summary>
    public static Dictionary<Resource, int> MaritimeRates(GameState state, int playerIndex)
    {
        ArgumentNullException.ThrowIfNull(state);

        var rates = TerrainExtensions.AllResources.ToDictionary(r => r, _ => state.Options.BankTradeRate);

        foreach (var port in PortsOf(state, playerIndex))
        {
            if (port.Resource is { } only)
            {
                rates[only] = Math.Min(rates[only], port.Rate);
                continue;
            }

            // بندر عمومی روی هر پنج منبع کار می‌کند.
            foreach (var resource in TerrainExtensions.AllResources)
            {
                rates[resource] = Math.Min(rates[resource], port.Rate);
            }
        }

        return rates;
    }

    /// <summary>مهلتِ تازه از روی تنظیماتِ همین بازی. بی‌زمان یعنی بی‌مهلت. */</summary>
    private static DateTimeOffset? Deadline(GameState state, DateTimeOffset? now) =>
        now is { } moment ? moment.AddSeconds(state.Options.TradeWindowSeconds) : null;

    private static MoveResult OfferTrade(GameState state, ProposeTrade action, DateTimeOffset? now)
    {
        if (state.Phase != TurnPhase.Main)
        {
            return MoveResult.Fail(GameError.WrongPhase);
        }

        if (action.PlayerIndex != state.CurrentPlayer)
        {
            return MoveResult.Fail(GameError.NotYourTurn);
        }

        if (state.PendingTrade is not null)
        {
            return MoveResult.Fail(GameError.TradeAlreadyOnTheTable);
        }

        var give = Clean(action.Give);
        var take = Clean(action.Take);

        if (give.Count == 0 || take.Count == 0)
        {
            return MoveResult.Fail(GameError.EmptyTrade);
        }

        if (!state.Player(action.PlayerIndex).CanAfford(give))
        {
            return MoveResult.Fail(GameError.NotEnoughResources);
        }

        var recipients = action.Recipients.Count > 0
            ? action.Recipients.Distinct().Where(i => i != action.PlayerIndex).ToList()
            : state.Players.Select(p => p.Index).Where(i => i != action.PlayerIndex).ToList();

        if (recipients.Count == 0 || recipients.Any(i => i < 0 || i >= state.Players.Count))
        {
            return MoveResult.Fail(GameError.InvalidVictim);
        }

        state.PendingTrade = new TradeOffer(
            action.PlayerIndex, give, take, recipients, Deadline(state, now));

        return MoveResult.Ok(new TradeProposed(action.PlayerIndex, give, take, recipients));
    }

    /// <summary>
    /// پاسخ به پیشنهاد. **اولین پذیرشِ معتبر همان‌جا معامله را می‌بندد.**
    ///
    /// پیش از این پیشنهاددهنده باید بینِ پذیرندگان یکی را انتخاب می‌کرد. حالا
    /// نه: هر کس زودتر بپذیرد و کالا را داشته باشد، معامله همان لحظه انجام
    /// می‌شود. یک کلیک کمتر، و مهم‌تر این‌که دیگر پنجره‌ای نیست که در آن دو نفر
    /// «پذیرفته» باشند و منابعشان بینِ پذیرش و تأیید عوض شود.
    ///
    /// ‎ConfirmTrade‎ برای بازی‌های قدیمی سرِ جایش می‌ماند، ولی دیگر تولید
    /// نمی‌شود — بازپخشِ لاگ‌های قبلی نباید بشکند.
    /// </summary>
    private static MoveResult AnswerTrade(GameState state, RespondToTrade action, DateTimeOffset? now)
    {
        if (state.PendingTrade is not { } offer)
        {
            return MoveResult.Fail(GameError.NoTradeOnTheTable);
        }

        if (offer.HasExpired(now))
        {
            return MoveResult.Fail(GameError.TradeExpired);
        }

        if (!offer.CanRespond(action.PlayerIndex))
        {
            return MoveResult.Fail(GameError.NotInvitedToTrade);
        }

        if (!action.Accept)
        {
            offer.Respond(action.PlayerIndex, TradeResponse.Rejected);
            return MoveResult.Ok(new TradeResponded(action.PlayerIndex, false));
        }

        // پذیرش بدون داشتن کالا یعنی پیشنهاددهنده روی چیزی حساب کند که وجود ندارد.
        if (!state.Player(action.PlayerIndex).CanAfford(offer.Take))
        {
            return MoveResult.Fail(GameError.NotEnoughResources);
        }

        // دستِ پیشنهاددهنده هم ممکن است از لحظه‌ی پیشنهاد تا حالا عوض شده باشد.
        if (!state.Player(offer.Proposer).CanAfford(offer.Give))
        {
            return MoveResult.Fail(GameError.NotEnoughResources);
        }

        offer.Respond(action.PlayerIndex, TradeResponse.Accepted);
        Exchange(state, offer, action.PlayerIndex);
        state.PendingTrade = null;

        return MoveResult.Ok(
            new TradeResponded(action.PlayerIndex, true),
            new TradeCompleted(offer.Proposer, action.PlayerIndex, offer.Give, offer.Take));
    }

    /// <summary>
    /// پیشنهاد متقابل: پیشنهادِ روی میز برداشته می‌شود و جایش شرطِ تازه‌ی گیرنده
    /// می‌نشیند، رو به همان پیشنهاددهنده‌ی قبلی.
    /// </summary>
    private static MoveResult OfferCounter(GameState state, CounterTrade action, DateTimeOffset? now)
    {
        if (state.PendingTrade is not { } offer)
        {
            return MoveResult.Fail(GameError.NoTradeOnTheTable);
        }

        if (offer.HasExpired(now))
        {
            return MoveResult.Fail(GameError.TradeExpired);
        }

        if (!offer.CanRespond(action.PlayerIndex))
        {
            return MoveResult.Fail(GameError.NotInvitedToTrade);
        }

        var give = Clean(action.Give);
        var take = Clean(action.Take);

        if (give.Count == 0 || take.Count == 0)
        {
            return MoveResult.Fail(GameError.EmptyTrade);
        }

        if (!state.Player(action.PlayerIndex).CanAfford(give))
        {
            return MoveResult.Fail(GameError.NotEnoughResources);
        }

        state.PendingTrade = new TradeOffer(
            action.PlayerIndex, give, take, [offer.Proposer], Deadline(state, now));

        return MoveResult.Ok(new TradeCountered(action.PlayerIndex, give, take));
    }

    /// <summary>
    /// جابه‌جاییِ دو بسته. یک‌جا نوشته شده تا مسیر پذیرش و مسیر قدیمیِ تأیید
    /// نتوانند از هم بلغزند.
    /// </summary>
    private static void Exchange(GameState state, TradeOffer offer, int partnerIndex)
    {
        var proposer = state.Player(offer.Proposer);
        var partner = state.Player(partnerIndex);

        foreach (var (resource, amount) in offer.Give)
        {
            proposer.Remove(resource, amount);
            partner.Add(resource, amount);
        }

        foreach (var (resource, amount) in offer.Take)
        {
            partner.Remove(resource, amount);
            proposer.Add(resource, amount);
        }
    }

    /// <summary>
    /// مسیر قدیمیِ «تأیید با یک پذیرنده».
    ///
    /// دیگر تولید نمی‌شود چون پذیرش خودش معامله را می‌بندد، ولی می‌مانَد تا
    /// بازپخشِ لاگ بازی‌هایی که پیش از این تغییر ثبت شده‌اند نشکند.
    /// </summary>
    private static MoveResult SettleTrade(GameState state, ConfirmTrade action)
    {
        if (state.PendingTrade is not { } offer)
        {
            return MoveResult.Fail(GameError.NoTradeOnTheTable);
        }

        if (offer.Proposer != action.PlayerIndex)
        {
            return MoveResult.Fail(GameError.NotYourTrade);
        }

        if (!offer.AcceptedBy.Contains(action.Partner))
        {
            return MoveResult.Fail(GameError.PartnerDidNotAccept);
        }

        // دست‌ها ممکن است از زمان پذیرش عوض شده باشند؛ دوباره بررسی می‌شود.
        if (!state.Player(action.PlayerIndex).CanAfford(offer.Give)
            || !state.Player(action.Partner).CanAfford(offer.Take))
        {
            return MoveResult.Fail(GameError.NotEnoughResources);
        }

        Exchange(state, offer, action.Partner);
        state.PendingTrade = null;

        return MoveResult.Ok(new TradeCompleted(action.PlayerIndex, action.Partner, offer.Give, offer.Take));
    }

    private static MoveResult WithdrawTrade(GameState state, CancelTrade action)
    {
        if (state.PendingTrade is not { } offer)
        {
            return MoveResult.Fail(GameError.NoTradeOnTheTable);
        }

        if (offer.Proposer != action.PlayerIndex)
        {
            return MoveResult.Fail(GameError.NotYourTrade);
        }

        state.PendingTrade = null;
        return MoveResult.Ok(new TradeCancelled(action.PlayerIndex));
    }

    private static Dictionary<Resource, int> Clean(IReadOnlyDictionary<Resource, int> bundle) =>
        bundle.Where(b => b.Value > 0).ToDictionary(b => b.Key, b => b.Value);

    // ── کارت‌های افتخاری ─────────────────────────────────────────────────

    /// <summary>
    /// طول جاده‌ی همه را بازمحاسبه می‌کند و در صورت لزوم کارت را جابه‌جا می‌کند.
    /// در تساوی، دارنده‌ی فعلی کارت را نگه می‌دارد؛ اگر دارنده عقب بیفتد و چند نفر
    /// مساوی جلو باشند، کارت بی‌صاحب می‌ماند تا یک نفر تنها جلو بزند.
    /// </summary>
    private static void RecomputeLongestRoad(GameState state, List<GameEvent> events)
    {
        foreach (var player in state.Players)
        {
            player.LongestRoadLength = RoadNetwork.LongestRoad(state, player.Index);
        }

        var minimum = state.Options.LongestRoadMinimum;
        var holder = state.LongestRoadHolder;
        var best = state.Players.Max(p => p.LongestRoadLength);

        if (best < minimum)
        {
            if (holder is not null)
            {
                state.Player(holder.Value).HasLongestRoad = false;
                events.Add(new LongestRoadChanged(null, best));
            }

            return;
        }

        if (holder is { } current && state.Player(current).LongestRoadLength == best)
        {
            return;
        }

        var leaders = state.Players.Where(p => p.LongestRoadLength == best).ToList();

        if (holder is not null)
        {
            state.Player(holder.Value).HasLongestRoad = false;
        }

        if (leaders.Count == 1)
        {
            leaders[0].HasLongestRoad = true;
            events.Add(new LongestRoadChanged(leaders[0].Index, best));
        }
        else
        {
            events.Add(new LongestRoadChanged(null, best));
        }
    }

    private static void RecomputeLargestArmy(GameState state, List<GameEvent> events)
    {
        var minimum = state.Options.LargestArmyMinimum;
        var best = state.Players.Max(p => p.KnightsPlayed);

        if (best < minimum)
        {
            return;
        }

        var holder = state.LargestArmyHolder;
        if (holder is { } current && state.Player(current).KnightsPlayed >= best)
        {
            return;
        }

        var leaders = state.Players.Where(p => p.KnightsPlayed == best).ToList();
        if (leaders.Count != 1)
        {
            return;
        }

        if (holder is not null)
        {
            state.Player(holder.Value).HasLargestArmy = false;
        }

        leaders[0].HasLargestArmy = true;
        events.Add(new LargestArmyChanged(leaders[0].Index, best));
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

    private static GameError ValidateRoadSpot(GameState state, int playerIndex, EdgeId edge)
    {
        if (!state.Board.ContainsEdge(edge))
        {
            return GameError.EdgeNotOnBoard;
        }

        if (state.RoadAt(edge) is not null)
        {
            return GameError.EdgeOccupied;
        }

        if (state.Player(playerIndex).RoadsLeft <= 0)
        {
            return GameError.NoPiecesLeft;
        }

        return IsRoadConnected(state, playerIndex, edge)
            ? GameError.None
            : GameError.RoadNotConnected;
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

    /// <summary>
    /// در بازی تیمی امتیاز کل تیم شمرده می‌شود — از جمله کارت‌های پیروزی پنهانِ
    /// هم‌تیمی‌ها، چون در تیم اطلاعات مشترک است.
    /// </summary>
    private static void CheckVictory(GameState state, int playerIndex, List<GameEvent> events)
    {
        var score = state.ScoreOf(playerIndex);
        if (score < state.Options.VictoryPoints)
        {
            return;
        }

        state.Winner = playerIndex;
        state.Phase = TurnPhase.GameOver;
        events.Add(new GameWon(playerIndex, score));
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

    /// <summary>
    /// ضلع‌هایی که گذاشتنِ یک جاده آن‌ها را *تازه* قانونی می‌کند.
    ///
    /// کارت جاده‌سازی دو جاده را پشت سر هم می‌گذارد و جاده‌ی دوم را روی وضعیتِ
    /// بعد از اولی می‌سنجد؛ پس فهرستِ «الان قانونی» برای انتخاب دوم کم است و
    /// بازیکن نمی‌تواند زنجیره بسازد.
    ///
    /// جاده واقعاً گذاشته و بی‌درنگ برداشته می‌شود تا همان قاعده‌ی حقیقی
    /// (<c>IsRoadConnected</c>) جواب بدهد، نه یک کپیِ ساده‌شده از آن. نسخه‌ی بازی
    /// دست نمی‌خورد (فقط <c>Apply</c> جلویش می‌برد) و صدا زدنِ این متد روی وضعیتی
    /// که هم‌زمان خوانده می‌شود نیست: نما داخل همان قفلِ بازی ساخته می‌شود.
    ///
    /// **فقط همسایه‌ها پرسیده می‌شوند.** قانونی‌بودنِ یک یال فقط به دو سرِ خودش
    /// نگاه می‌کند، پس گذاشتنِ جاده‌ی <paramref name="placed"/> جوابِ هیچ یالی جز
    /// آن‌هایی که با آن سرِ مشترک دارند را عوض نمی‌کند. پیش از این کلِ برد پیموده
    /// می‌شد — روی برد بزرگ ۲۱۰ یال به‌ازای *هر* انتخابِ اول.
    /// </summary>
    public static IReadOnlyList<EdgeId> RoadsOpenedBy(GameState state, int playerIndex, EdgeId placed)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.RoadAt(placed) is not null)
        {
            return [];
        }

        var neighbours = placed.Endpoints()
            .SelectMany(v => v.TouchingEdges())
            .Distinct()
            .Where(e => e != placed && state.Board.ContainsEdge(e) && state.RoadAt(e) is null)
            .ToList();

        // «تازه» یعنی تازه: همسایه‌ای که از قبل هم قانونی بود در فهرست جداگانه‌ی
        // جاده‌های آزاد هست و دوباره فرستادنش فقط حجم است.
        var alreadyLegal = neighbours.Where(e => IsRoadConnected(state, playerIndex, e)).ToHashSet();

        state.PlaceRoad(placed, playerIndex);

        try
        {
            return
            [
                .. neighbours.Where(e =>
                    !alreadyLegal.Contains(e) && IsRoadConnected(state, playerIndex, e))
            ];
        }
        finally
        {
            state.RemoveRoad(placed);
        }
    }

    /// <summary>ضلع‌هایی که این بازیکن الان می‌تواند روی آن‌ها جاده بسازد.</summary>
    public static IEnumerable<EdgeId> LegalRoadEdges(GameState state, int playerIndex) =>
        state.Board.Edges.Where(e => state.RoadAt(e) is null && IsRoadConnected(state, playerIndex, e));

    /// <summary>بندرهایی که این بازیکن با ساختمان‌هایش در اختیار دارد.</summary>
    public static IEnumerable<Port> PortsOf(GameState state, int playerIndex) =>
        state.Board.Ports.Where(p => p.Vertices().Any(v => state.BuildingAt(v)?.PlayerIndex == playerIndex));
}
