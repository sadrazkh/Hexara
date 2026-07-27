using Hexara.Domain.Game;

namespace Hexara.Domain.Tests.Game;

public class SetupPhaseTests
{
    [Fact]
    public void Setup_order_is_a_snake()
    {
        var state = Games.New(players: 4);
        Assert.Equal([0, 1, 2, 3, 3, 2, 1, 0], state.SetupOrder);
    }

    [Fact]
    public void Game_starts_in_setup_with_the_first_player()
    {
        var state = Games.New();

        Assert.Equal(TurnPhase.SetupSettlement, state.Phase);
        Assert.Equal(0, state.CurrentPlayer);
        Assert.Equal(0, state.TurnNumber);
    }

    [Fact]
    public void Robber_starts_on_the_desert()
    {
        var state = Games.New();
        Assert.Equal(Domain.Board.Terrain.Desert, state.Board.TileAt(state.Robber)!.Terrain);
    }

    [Fact]
    public void Another_player_cannot_place_out_of_turn()
    {
        var state = Games.New();
        var vertex = GameEngine.LegalSettlementVertices(state, 1).First();

        var result = GameEngine.Apply(state, new PlaceInitialSettlement(1, vertex));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotYourTurn, result.Error);
    }

    [Fact]
    public void Settlement_must_respect_the_distance_rule()
    {
        var state = Games.New();
        var first = GameEngine.LegalSettlementVertices(state, 0).First();
        GameEngine.Apply(state, new PlaceInitialSettlement(0, first));

        var road = first.TouchingEdges().First(e => state.Board.ContainsEdge(e));
        GameEngine.Apply(state, new PlaceInitialRoad(0, road));

        var neighbour = first.AdjacentVertices().First(v => state.Board.ContainsVertex(v));
        var result = GameEngine.Apply(state, new PlaceInitialSettlement(state.CurrentPlayer, neighbour));

        Assert.False(result.Success);
        Assert.Equal(GameError.TooCloseToAnotherBuilding, result.Error);
    }

    [Fact]
    public void Setup_road_must_touch_the_settlement_just_placed()
    {
        var state = Games.New();
        var vertex = GameEngine.LegalSettlementVertices(state, 0).First();
        GameEngine.Apply(state, new PlaceInitialSettlement(0, vertex));

        var far = state.Board.Edges.First(e => !e.Endpoints().Contains(vertex));
        var result = GameEngine.Apply(state, new PlaceInitialRoad(0, far));

        Assert.False(result.Success);
        Assert.Equal(GameError.SetupRoadMustTouchSettlement, result.Error);
    }

    [Fact]
    public void Cannot_roll_before_setup_is_over()
    {
        var state = Games.New();
        var result = GameEngine.Apply(state, new RollDice(0));

        Assert.False(result.Success);
        Assert.Equal(GameError.WrongPhase, result.Error);
    }

    [Fact]
    public void Setup_gives_every_player_two_settlements_and_two_roads()
    {
        var state = Games.New(players: 3);
        Games.RunSetup(state);

        foreach (var player in state.Players)
        {
            Assert.Equal(2, state.BuildingsOf(player.Index).Count());
            Assert.Equal(2, state.RoadsOf(player.Index).Count());
            Assert.Equal(3, player.SettlementsLeft);
            Assert.Equal(13, player.RoadsLeft);
            Assert.Equal(2, player.VictoryPoints);
        }
    }

    [Fact]
    public void Setup_ends_on_the_first_players_roll()
    {
        var state = Games.New(players: 3);
        Games.RunSetup(state);

        Assert.Equal(TurnPhase.Roll, state.Phase);
        Assert.Equal(0, state.CurrentPlayer);
        Assert.Equal(1, state.TurnNumber);
        Assert.False(state.IsSetup);
    }

    /// <summary>فقط آبادی دور دوم منبع می‌دهد — نه آبادی دور اول.</summary>
    [Fact]
    public void Only_the_second_settlement_pays_out()
    {
        var state = Games.New(players: 3);

        var placed = new Dictionary<int, List<Domain.Board.VertexId>>();

        while (state.IsSetup)
        {
            var player = state.CurrentPlayer;
            var vertex = GameEngine.LegalSettlementVertices(state, player)
                .OrderBy(v => v.ToString(), StringComparer.Ordinal)
                .First();

            var round = state.SetupStep < state.Options.PlayerCount ? 1 : 2;
            var result = GameEngine.Apply(state, new PlaceInitialSettlement(player, vertex));

            if (round == 1)
            {
                Assert.DoesNotContain(result.Events, e => e is ResourcesProduced);
            }

            if (!placed.TryGetValue(round, out var list))
            {
                placed[round] = list = [];
            }

            list.Add(vertex);

            var edge = vertex.TouchingEdges()
                .Where(e => state.Board.ContainsEdge(e) && state.RoadAt(e) is null)
                .OrderBy(e => e.ToString(), StringComparer.Ordinal)
                .First();
            GameEngine.Apply(state, new PlaceInitialRoad(player, edge));
        }

        // مجموع کارت‌های هر بازیکن = تعداد خانه‌های منبع‌دار کنار آبادی دومش.
        foreach (var player in state.Players)
        {
            var second = placed[2][state.Options.PlayerCount - 1 - player.Index];
            var expected = second.TouchingHexes()
                .Select(state.Board.TileAt)
                .Count(t => t?.Resource is not null);

            Assert.Equal(expected, player.TotalCards);
        }
    }
}
