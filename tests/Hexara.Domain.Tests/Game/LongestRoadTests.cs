using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Domain.Tests.Game;

public class LongestRoadTests
{
    private static readonly Axial Center = new(0, 0);

    // شش ضلع خانه‌ی مرکزی یک حلقه‌ی بسته‌اند؛ ضلع s و s+1 در گوشه‌ی s به هم می‌رسند.
    private static EdgeId Ring(int side) => EdgeId.Of(Center, side);

    private static VertexId Corner(int corner) => VertexId.Of(Center, corner);

    [Fact]
    public void No_roads_means_no_length()
    {
        var state = Games.New(players: 2);
        Assert.Equal(0, RoadNetwork.LongestRoad(state, 0));
    }

    [Fact]
    public void A_straight_chain_counts_every_segment()
    {
        var state = Games.New(players: 2);
        for (var s = 0; s < 5; s++)
        {
            state.PlaceRoad(Ring(s), 0);
        }

        Assert.Equal(5, RoadNetwork.LongestRoad(state, 0));
    }

    [Fact]
    public void A_closed_loop_counts_each_edge_once()
    {
        var state = Games.New(players: 2);
        for (var s = 0; s < 6; s++)
        {
            state.PlaceRoad(Ring(s), 0);
        }

        Assert.Equal(6, RoadNetwork.LongestRoad(state, 0));
    }

    /// <summary>در یک انشعاب سه‌شاخه فقط دو شاخه می‌توانند در یک مسیر بیایند.</summary>
    [Fact]
    public void A_fork_does_not_add_to_the_length()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(Ring(1), 0);
        state.PlaceRoad(Ring(2), 0);
        state.PlaceRoad(EdgeId.Of(Center.Neighbor(1), 3), 0); // شاخه‌ی سوم از همان گوشه

        Assert.Equal(2, RoadNetwork.LongestRoad(state, 0));
    }

    [Fact]
    public void Only_your_own_roads_count()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(Ring(0), 0);
        state.PlaceRoad(Ring(1), 1);
        state.PlaceRoad(Ring(2), 1);

        Assert.Equal(1, RoadNetwork.LongestRoad(state, 0));
        Assert.Equal(2, RoadNetwork.LongestRoad(state, 1));
    }

    /// <summary>آبادی حریف وسط مسیر، جاده را دو تکه می‌کند.</summary>
    [Fact]
    public void An_opponent_building_cuts_the_road()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(Ring(0), 0);
        state.PlaceRoad(Ring(1), 0);
        state.PlaceRoad(Ring(2), 0);

        Assert.Equal(3, RoadNetwork.LongestRoad(state, 0));

        state.PlaceBuilding(Corner(1), new Building(1, BuildingKind.Settlement));

        Assert.Equal(2, RoadNetwork.LongestRoad(state, 0));
    }

    [Fact]
    public void Your_own_building_does_not_cut_the_road()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(Ring(0), 0);
        state.PlaceRoad(Ring(1), 0);
        state.PlaceRoad(Ring(2), 0);
        state.PlaceBuilding(Corner(1), new Building(0, BuildingKind.Settlement));

        Assert.Equal(3, RoadNetwork.LongestRoad(state, 0));
    }

    // ── جابه‌جایی کارت ───────────────────────────────────────────────────

    [Fact]
    public void Four_roads_are_not_enough_for_the_card()
    {
        var state = BuildChainThenOneMore(4, out var result);

        Assert.True(result.Success);
        Assert.Null(state.LongestRoadHolder);
        Assert.False(state.Player(0).HasLongestRoad);
    }

    [Fact]
    public void The_fifth_road_takes_the_card_and_two_points()
    {
        var state = BuildChainThenOneMore(5, out var result);

        Assert.Equal(0, state.LongestRoadHolder);
        Assert.Equal(5, state.Player(0).LongestRoadLength);
        Assert.Equal(2, state.Player(0).VictoryPoints);
        Assert.Contains(result.Events, e => e is LongestRoadChanged { PlayerIndex: 0, Length: 5 });
    }

    /// <summary>در تساوی، دارنده‌ی فعلی کارت را نگه می‌دارد.</summary>
    [Fact]
    public void A_tie_does_not_move_the_card()
    {
        var state = Games.New(players: 2);

        // بازیکن ۱ صاحب کارت با پنج جاده روی حلقه.
        for (var s = 0; s < 5; s++)
        {
            state.PlaceRoad(Ring(s), 1);
        }

        state.Player(1).HasLongestRoad = true;
        state.Player(1).LongestRoadLength = 5;

        // بازیکن ۰ در جای دیگری از برد هم به پنج می‌رسد.
        var far = FarChain(state, 0, 5);
        Games.StartMainPhase(state, 0);
        Games.GiveRoadCost(state, 0);

        var result = GameEngine.Apply(state, new BuildRoad(0, far));

        Assert.True(result.Success);
        Assert.Equal(5, state.Player(0).LongestRoadLength);
        Assert.Equal(1, state.LongestRoadHolder);
    }

    [Fact]
    public void A_longer_road_takes_the_card_away()
    {
        var state = Games.New(players: 2);
        for (var s = 0; s < 5; s++)
        {
            state.PlaceRoad(Ring(s), 1);
        }

        state.Player(1).HasLongestRoad = true;
        state.Player(1).LongestRoadLength = 5;

        var chain = FarChain(state, 0, 6);
        Games.StartMainPhase(state, 0);
        Games.GiveRoadCost(state, 0);

        var result = GameEngine.Apply(state, new BuildRoad(0, chain));

        Assert.Equal(0, state.LongestRoadHolder);
        Assert.False(state.Player(1).HasLongestRoad);
        Assert.Contains(result.Events, e => e is LongestRoadChanged { PlayerIndex: 0 });
    }

    /// <summary>آبادی تازه‌ی حریف می‌تواند دارنده‌ی کارت را از تخت پایین بکشد.</summary>
    [Fact]
    public void Cutting_the_holders_road_takes_the_card_off_the_table()
    {
        var state = Games.New(players: 2);
        for (var s = 0; s < 5; s++)
        {
            state.PlaceRoad(Ring(s), 1);
        }

        state.Player(1).HasLongestRoad = true;
        state.Player(1).LongestRoadLength = 5;

        // بازیکن ۰ جاده‌ای بیرون از حلقه دارد و روی گوشه‌ی وسط جاده‌ی حریف آبادی می‌سازد.
        var cut = Corner(2);
        var outside = cut.TouchingEdges().First(e => state.Board.ContainsEdge(e) && state.RoadAt(e) is null);
        state.PlaceRoad(outside, 0);

        Games.StartMainPhase(state, 0);
        Games.GiveSettlementCost(state, 0);

        var result = GameEngine.Apply(state, new BuildSettlement(0, cut));

        Assert.True(result.Success);
        Assert.Equal(3, state.Player(1).LongestRoadLength);
        Assert.Null(state.LongestRoadHolder);
        Assert.Contains(result.Events, e => e is LongestRoadChanged { PlayerIndex: null });
    }

    /// <summary>زنجیره‌ای با طول ‎length-1‎ می‌سازد و آخرین جاده را از راه موتور می‌گذارد.</summary>
    private static GameState BuildChainThenOneMore(int length, out MoveResult result)
    {
        var state = Games.New(players: 2);
        for (var s = 0; s < length - 1; s++)
        {
            state.PlaceRoad(Ring(s), 0);
        }

        Games.StartMainPhase(state, 0);
        Games.GiveRoadCost(state, 0);
        result = GameEngine.Apply(state, new BuildRoad(0, Ring(length - 1)));
        return state;
    }

    /// <summary>
    /// زنجیره‌ای دور از حلقه‌ی مرکزی برای بازیکن می‌سازد و آخرین ضلعِ نگذاشته را
    /// برمی‌گرداند تا خودِ موتور آن را بسازد.
    /// </summary>
    private static EdgeId FarChain(GameState state, int playerIndex, int length)
    {
        var start = new Axial(0, -2);
        var edges = Enumerable.Range(0, 6).Select(s => EdgeId.Of(start, s)).ToList();

        for (var i = 0; i < length - 1; i++)
        {
            state.PlaceRoad(edges[i], playerIndex);
        }

        return edges[length - 1];
    }
}
