using Hexara.Application.Games;
using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Application.Tests;

public class GameJsonTests
{
    private static GameState NewGame(int players = 3, ulong seed = 5) =>
        GameState.Create(
            new GameOptions { PlayerCount = players, Seed = seed },
            [.. Enumerable.Range(0, players).Select(_ => Guid.NewGuid())]);

    [Fact]
    public void A_snapshot_survives_the_json_round_trip()
    {
        var state = NewGame();
        var json = GameJson.Serialize(state.ToSnapshot());
        var restored = GameState.Restore(GameJson.Deserialize<GameSnapshot>(json));

        Assert.Equal(state.Board.Tiles.Count, restored.Board.Tiles.Count);
        Assert.Equal(state.Board.Ports.Count, restored.Board.Ports.Count);
        Assert.Equal(state.Robber, restored.Robber);
        Assert.Equal(state.Options, restored.Options);
        Assert.Equal(state.DevelopmentDeckCount, restored.DevelopmentDeckCount);
        Assert.Equal(state.Players.Select(p => p.Id), restored.Players.Select(p => p.Id));
    }

    /// <summary>وضعیت مولد تاس باید عیناً برگردد وگرنه ادامه‌ی بازی قابل بازپخش نیست.</summary>
    [Fact]
    public void The_dice_generator_state_survives_json()
    {
        var state = NewGame();
        var restored = GameState.Restore(GameJson.Deserialize<GameSnapshot>(GameJson.Serialize(state.ToSnapshot())));

        Assert.Equal(state.ToSnapshot().RngState, restored.ToSnapshot().RngState);
    }

    /// <summary>
    /// هر حرکتی که در دامنه هست باید بتواند بنویسد و بخواند. اگر فازهای بعد حرکت
    /// جدیدی اضافه کنند و اینجا نگاشت نشود، همین تست سر و صدا می‌کند.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryActionKind))]
    public void Every_action_kind_survives_the_round_trip(GameAction action)
    {
        var json = GameJson.Serialize(action);
        var restored = GameJson.Deserialize<GameAction>(json);

        Assert.Equal(action.GetType(), restored.GetType());
        Assert.Equal(action.PlayerIndex, restored.PlayerIndex);

        // مقایسه‌ی رکوردها به درد نمی‌خورد چون دیکشنری و فهرست دارند؛ خودِ JSON مقایسه می‌شود.
        Assert.Equal(json, GameJson.Serialize(restored));
    }

    [Fact]
    public void Every_action_type_in_the_domain_is_covered_by_the_tests()
    {
        var declared = typeof(GameAction).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(GameAction).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToHashSet();

        var covered = EveryActionKind()
            .Select(row => ((GameAction)row[0]!).GetType().Name)
            .ToHashSet();

        Assert.Equal(declared.Order(), covered.Order());
    }

    [Fact]
    public void The_type_discriminator_is_the_type_name()
    {
        var json = GameJson.Serialize<GameAction>(new EndTurn(2));

        Assert.Contains("\"$kind\":\"EndTurn\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Canonical_vertex_and_edge_ids_come_back_identical()
    {
        var hex = new Axial(1, -1);

        for (var i = 0; i < 6; i++)
        {
            var vertex = VertexId.Of(hex, i);
            var edge = EdgeId.Of(hex, i);

            Assert.Equal(vertex, GameJson.Deserialize<VertexId>(GameJson.Serialize(vertex)));
            Assert.Equal(edge, GameJson.Deserialize<EdgeId>(GameJson.Serialize(edge)));
        }
    }

    [Fact]
    public void An_event_list_survives_the_round_trip()
    {
        var events = new List<GameEvent>
        {
            new DiceRolled(0, 3, 4),
            new ResourcesProduced([new ResourceGrant(1, Resource.Ore, 2)]),
            new RobberMoved(0, new Axial(0, 0), new Axial(1, 0)),
            new ResourceStolen(0, 1, Resource.Wool),
            new SettlementBuilt(2, VertexId.Of(new Axial(0, 0), 3)),
            new RoadBuilt(2, EdgeId.Of(new Axial(0, 0), 2)),
            new LongestRoadChanged(1, 6),
            new LargestArmyChanged(null, 0),
            new MaritimeTraded(0, Resource.Lumber, 3, Resource.Ore),
            new TradeCompleted(0, 1,
                new Dictionary<Resource, int> { [Resource.Lumber] = 1 },
                new Dictionary<Resource, int> { [Resource.Ore] = 1 }),
            new GameWon(2, 10)
        };

        var json = GameJson.Serialize<IReadOnlyList<GameEvent>>(events);
        var restored = GameJson.Deserialize<IReadOnlyList<GameEvent>>(json);

        Assert.Equal(events.Count, restored.Count);
        Assert.Equal(events.Select(e => e.GetType()), restored.Select(e => e.GetType()));
        Assert.Equal(3, restored.OfType<DiceRolled>().Single().Die1);
        Assert.Equal(Resource.Wool, restored.OfType<ResourceStolen>().Single().Resource);
        Assert.Equal(VertexId.Of(new Axial(0, 0), 3), restored.OfType<SettlementBuilt>().Single().Vertex);
    }

    public static IEnumerable<object[]> EveryActionKind()
    {
        var hex = new Axial(1, 0);
        var vertex = VertexId.Of(hex, 2);
        var edge = EdgeId.Of(hex, 3);

        yield return [new PlaceInitialSettlement(0, vertex)];
        yield return [new PlaceInitialRoad(0, edge)];
        yield return [new RollDice(1)];
        yield return [new BuildRoad(1, edge)];
        yield return [new BuildSettlement(2, vertex)];
        yield return [new BuildCity(2, vertex)];
        yield return [new DiscardCards(0, new Dictionary<Resource, int> { [Resource.Ore] = 2 })];
        yield return [new MoveRobber(0, hex, 1)];
        yield return [new MoveRobber(0, hex, null)];
        yield return [new EndTurn(3)];
        yield return [new BuyDevelopmentCard(0)];
        yield return [new PlayKnight(0, hex, 2)];
        yield return [new PlayRoadBuilding(0, edge, EdgeId.Of(hex, 4))];
        yield return [new PlayRoadBuilding(0, edge, null)];
        yield return [new PlayYearOfPlenty(0, Resource.Ore, Resource.Grain)];
        yield return [new PlayMonopoly(0, Resource.Wool)];
        yield return [new MaritimeTrade(0, Resource.Lumber, Resource.Ore)];
        yield return
        [
            new ProposeTrade(
                0,
                new Dictionary<Resource, int> { [Resource.Lumber] = 2 },
                new Dictionary<Resource, int> { [Resource.Ore] = 1 },
                [1, 2])
        ];
        yield return [new RespondToTrade(1, true)];
        yield return [new ConfirmTrade(0, 1)];
        yield return [new CancelTrade(0)];
    }
}
