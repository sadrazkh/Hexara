using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Domain.Tests.Game;

public class BuildTests
{
    private static readonly Axial Center = new(0, 0);

    // دو ضلع کنار هم از خانه‌ی مرکزی که در گوشه‌ی شماره‌ی صفر به هم می‌رسند.
    private static readonly EdgeId FirstEdge = EdgeId.Of(Center, 0);
    private static readonly EdgeId SecondEdge = EdgeId.Of(Center, 1);
    private static readonly VertexId SharedVertex = VertexId.Of(Center, 0);

    [Fact]
    public void Road_needs_a_connection()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveRoadCost(state, 0);

        var result = GameEngine.Apply(state, new BuildRoad(0, FirstEdge));

        Assert.False(result.Success);
        Assert.Equal(GameError.RoadNotConnected, result.Error);
    }

    [Fact]
    public void Road_extends_from_an_existing_road()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(FirstEdge, 0);
        Games.StartMainPhase(state, 0);
        Games.GiveRoadCost(state, 0);

        var result = GameEngine.Apply(state, new BuildRoad(0, SecondEdge));

        Assert.True(result.Success);
        Assert.Equal(0, state.RoadAt(SecondEdge));
        Assert.Contains(result.Events, e => e is RoadBuilt);
        Assert.Equal(0, state.Player(0).TotalCards);
    }

    /// <summary>نمی‌توان از «داخل» آبادی حریف رد شد.</summary>
    [Fact]
    public void Opponent_building_blocks_the_road()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(FirstEdge, 0);
        state.PlaceBuilding(SharedVertex, new Building(1, BuildingKind.Settlement));
        Games.StartMainPhase(state, 0);
        Games.GiveRoadCost(state, 0);

        var result = GameEngine.Apply(state, new BuildRoad(0, SecondEdge));

        Assert.False(result.Success);
        Assert.Equal(GameError.RoadNotConnected, result.Error);
    }

    [Fact]
    public void Road_needs_resources()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(FirstEdge, 0);
        Games.StartMainPhase(state, 0);

        var result = GameEngine.Apply(state, new BuildRoad(0, SecondEdge));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotEnoughResources, result.Error);
    }

    [Fact]
    public void Road_cannot_be_built_twice_on_the_same_edge()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(FirstEdge, 0);
        Games.StartMainPhase(state, 0);
        Games.GiveRoadCost(state, 0);

        var result = GameEngine.Apply(state, new BuildRoad(0, FirstEdge));

        Assert.False(result.Success);
        Assert.Equal(GameError.EdgeOccupied, result.Error);
    }

    [Fact]
    public void Road_stops_when_the_player_runs_out_of_pieces()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(FirstEdge, 0);
        state.Player(0).RoadsLeft = 0;
        Games.StartMainPhase(state, 0);
        Games.GiveRoadCost(state, 0);

        var result = GameEngine.Apply(state, new BuildRoad(0, SecondEdge));

        Assert.False(result.Success);
        Assert.Equal(GameError.NoPiecesLeft, result.Error);
    }

    [Fact]
    public void Settlement_needs_one_of_your_own_roads()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveSettlementCost(state, 0);

        var result = GameEngine.Apply(state, new BuildSettlement(0, SharedVertex));

        Assert.False(result.Success);
        Assert.Equal(GameError.SettlementNotConnectedToRoad, result.Error);
    }

    [Fact]
    public void Settlement_on_your_own_road_succeeds()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(FirstEdge, 0);
        Games.StartMainPhase(state, 0);
        Games.GiveSettlementCost(state, 0);

        var result = GameEngine.Apply(state, new BuildSettlement(0, SharedVertex));

        Assert.True(result.Success);
        Assert.Equal(BuildingKind.Settlement, state.BuildingAt(SharedVertex)!.Kind);
        Assert.Equal(1, state.Player(0).VictoryPoints);
        Assert.Equal(4, state.Player(0).SettlementsLeft);
    }

    [Fact]
    public void Settlement_respects_the_distance_rule()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(FirstEdge, 0);
        state.PlaceBuilding(SharedVertex.AdjacentVertices().First(), new Building(1, BuildingKind.Settlement));
        Games.StartMainPhase(state, 0);
        Games.GiveSettlementCost(state, 0);

        var result = GameEngine.Apply(state, new BuildSettlement(0, SharedVertex));

        Assert.False(result.Success);
        Assert.Equal(GameError.TooCloseToAnotherBuilding, result.Error);
    }

    [Fact]
    public void City_upgrades_your_own_settlement()
    {
        var state = Games.New(players: 2);
        state.PlaceBuilding(SharedVertex, new Building(0, BuildingKind.Settlement));
        state.Player(0).SettlementsLeft--;
        state.Player(0).BuildingPoints++;
        Games.StartMainPhase(state, 0);
        Games.GiveCityCost(state, 0);

        var result = GameEngine.Apply(state, new BuildCity(0, SharedVertex));

        Assert.True(result.Success);
        Assert.Equal(BuildingKind.City, state.BuildingAt(SharedVertex)!.Kind);
        Assert.Equal(2, state.Player(0).VictoryPoints);
        Assert.Equal(3, state.Player(0).CitiesLeft);
        Assert.Equal(5, state.Player(0).SettlementsLeft); // آبادی برمی‌گردد به دست بازیکن
    }

    [Fact]
    public void City_cannot_be_built_on_an_opponent_settlement()
    {
        var state = Games.New(players: 2);
        state.PlaceBuilding(SharedVertex, new Building(1, BuildingKind.Settlement));
        Games.StartMainPhase(state, 0);
        Games.GiveCityCost(state, 0);

        var result = GameEngine.Apply(state, new BuildCity(0, SharedVertex));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotYourSettlement, result.Error);
    }

    [Fact]
    public void City_cannot_be_built_on_empty_ground()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveCityCost(state, 0);

        var result = GameEngine.Apply(state, new BuildCity(0, SharedVertex));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotASettlement, result.Error);
    }

    [Fact]
    public void Reaching_the_target_ends_the_game()
    {
        var state = Games.New(players: 2, tweak: o => o with { VictoryPoints = 3 });
        state.PlaceRoad(FirstEdge, 0);
        state.Player(0).BuildingPoints = 2;
        Games.StartMainPhase(state, 0);
        Games.GiveSettlementCost(state, 0);

        var result = GameEngine.Apply(state, new BuildSettlement(0, SharedVertex));

        Assert.True(result.Success);
        Assert.Contains(result.Events, e => e is GameWon { PlayerIndex: 0 });
        Assert.Equal(TurnPhase.GameOver, state.Phase);
        Assert.Equal(0, state.Winner);
    }

    [Fact]
    public void Nothing_can_be_played_after_the_game_is_over()
    {
        var state = Games.New(players: 2);
        state.Phase = TurnPhase.GameOver;

        var result = GameEngine.Apply(state, new EndTurn(0));

        Assert.False(result.Success);
        Assert.Equal(GameError.GameFinished, result.Error);
    }

    /// <summary>حرکت رد شده نباید هیچ اثری روی وضعیت بگذارد.</summary>
    [Fact]
    public void Rejected_move_leaves_the_state_untouched()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveRoadCost(state, 0);

        var version = state.Version;
        var cards = state.Player(0).TotalCards;

        var result = GameEngine.Apply(state, new BuildRoad(0, FirstEdge));

        Assert.False(result.Success);
        Assert.Equal(version, state.Version);
        Assert.Equal(cards, state.Player(0).TotalCards);
        Assert.Empty(state.Roads);
    }

    [Fact]
    public void Building_returns_the_paid_cards_to_the_bank()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(FirstEdge, 0);
        Games.StartMainPhase(state, 0);
        Games.GiveRoadCost(state, 0);

        var bankBefore = state.Bank[Resource.Lumber];
        GameEngine.Apply(state, new BuildRoad(0, SecondEdge));

        Assert.Equal(bankBefore + 1, state.Bank[Resource.Lumber]);
    }

    [Fact]
    public void End_turn_moves_to_the_next_player()
    {
        var state = Games.New(players: 3);
        Games.RunSetup(state);
        Games.StartMainPhase(state, 0);

        var result = GameEngine.Apply(state, new EndTurn(0));

        Assert.True(result.Success);
        Assert.Equal(1, state.CurrentPlayer);
        Assert.Equal(TurnPhase.Roll, state.Phase);
        Assert.Null(state.LastRoll);
        Assert.Contains(result.Events, e => e is TurnStarted { PlayerIndex: 1 });
    }

    [Fact]
    public void The_turn_wraps_around_to_the_first_player()
    {
        var state = Games.New(players: 3);
        Games.RunSetup(state);
        Games.StartMainPhase(state, 2);

        GameEngine.Apply(state, new EndTurn(2));

        Assert.Equal(0, state.CurrentPlayer);
    }

    // ── گرفتن بندر ───────────────────────────────────────────────────────

    /// <summary>
    /// بندر روی برد یک نشانِ کوچک است و اثرش (نرخ معامله) جای دیگری دیده می‌شود.
    /// بدون این اعلام، بازیکن تا وسط یک معامله نمی‌فهمد نرخش عوض شده.
    /// </summary>
    [Fact]
    public void Building_on_a_port_announces_it()
    {
        var state = Games.New(players: 2);
        var port = state.Board.Ports.First(p => !p.IsGeneric);
        var vertex = port.Vertices().First();

        // آبادی باید به جاده‌ی خودت بچسبد، پس یکی از یال‌های همان گوشه را می‌گیریم.
        state.PlaceRoad(vertex.TouchingEdges().First(e => state.Board.ContainsEdge(e)), 0);
        Games.StartMainPhase(state, 0);
        Games.GiveSettlementCost(state, 0);

        var result = GameEngine.Apply(state, new BuildSettlement(0, vertex));

        Assert.True(result.Success);
        var taken = result.Events.OfType<PortTaken>().Single();
        Assert.Equal(0, taken.PlayerIndex);
        Assert.Equal(port.Resource, taken.Resource);
        Assert.Equal(2, taken.Rate);
    }

    [Fact]
    public void A_generic_port_is_announced_without_a_resource()
    {
        var state = Games.New(players: 2);
        var port = state.Board.Ports.First(p => p.IsGeneric);
        var vertex = port.Vertices().First();

        state.PlaceRoad(vertex.TouchingEdges().First(e => state.Board.ContainsEdge(e)), 0);
        Games.StartMainPhase(state, 0);
        Games.GiveSettlementCost(state, 0);

        var taken = GameEngine.Apply(state, new BuildSettlement(0, vertex))
            .Events.OfType<PortTaken>().Single();

        Assert.Null(taken.Resource);
        Assert.Equal(3, taken.Rate);
    }

    [Fact]
    public void Building_away_from_every_port_announces_nothing()
    {
        var state = Games.New(players: 2);
        var onAPort = state.Board.Ports.SelectMany(p => p.Vertices()).ToHashSet();

        var vertex = GameEngine.LegalSettlementVertices(state, 0).First(v => !onAPort.Contains(v));

        state.PlaceRoad(vertex.TouchingEdges().First(e => state.Board.ContainsEdge(e)), 0);
        Games.StartMainPhase(state, 0);
        Games.GiveSettlementCost(state, 0);

        var result = GameEngine.Apply(state, new BuildSettlement(0, vertex));

        Assert.True(result.Success);
        Assert.Empty(result.Events.OfType<PortTaken>());
    }

    /// <summary>چیدمان اولیه هم بندر می‌گیرد — و بیشترِ بندرها همان‌جا گرفته می‌شوند.</summary>
    [Fact]
    public void The_setup_settlement_takes_ports_too()
    {
        var state = Games.New(players: 2);
        var port = state.Board.Ports.First(p => !p.IsGeneric);
        var vertex = port.Vertices().First();

        var result = GameEngine.Apply(state, new PlaceInitialSettlement(0, vertex));

        Assert.True(result.Success);
        Assert.Single(result.Events.OfType<PortTaken>());
    }

    /// <summary>
    /// شهر روی آبادیِ خودت ساخته می‌شود، پس بندری نمی‌آورد که قبلاً نداشته باشی —
    /// اعلامِ دوباره‌اش یعنی دروغ در لاگ.
    /// </summary>
    [Fact]
    public void Upgrading_to_a_city_does_not_announce_the_port_again()
    {
        var state = Games.New(players: 2);
        var port = state.Board.Ports.First(p => !p.IsGeneric);
        var vertex = port.Vertices().First();

        state.PlaceBuilding(vertex, new Building(0, BuildingKind.Settlement));
        Games.StartMainPhase(state, 0);
        Games.GiveCityCost(state, 0);

        var result = GameEngine.Apply(state, new BuildCity(0, vertex));

        Assert.True(result.Success);
        Assert.Empty(result.Events.OfType<PortTaken>());
    }
}
