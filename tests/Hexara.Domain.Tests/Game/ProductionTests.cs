using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Domain.Tests.Game;

public class ProductionTests
{
    [Fact]
    public void Roll_pays_exactly_the_matching_buildings()
    {
        var state = Games.SetupWithProductiveRoll(out var roll);
        var expected = Games.ExpectedProduction(state, roll);
        var before = Games.Hands(state);

        var result = GameEngine.Apply(state, new RollDice(0));

        Assert.True(result.Success);
        Assert.Equal(roll, state.LastRoll);
        Assert.Equal(TurnPhase.Main, state.Phase);
        AssertGains(state, before, expected);
    }

    [Fact]
    public void Roll_reports_what_it_paid()
    {
        var state = Games.SetupWithProductiveRoll(out var roll);
        var expected = Games.ExpectedProduction(state, roll);

        var result = GameEngine.Apply(state, new RollDice(0));

        var produced = result.Events.OfType<ResourcesProduced>().Single();
        var reported = produced.Grants
            .GroupBy(g => (g.PlayerIndex, g.Resource))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        Assert.Equal(expected.OrderBy(e => e.Key.ToString()), reported.OrderBy(e => e.Key.ToString()));
    }

    /// <summary>خانه‌ای که دزد روی آن نشسته هیچ چیز تولید نمی‌کند.</summary>
    [Fact]
    public void Robber_blocks_production_on_its_hex()
    {
        var state = Games.SetupWithProductiveRoll(out var roll);

        var blocked = state.Board.TilesWithNumber(roll)
            .First(t => t.Resource is not null && t.Vertices().Any(v => state.BuildingAt(v) is not null));

        var withoutRobber = Games.ExpectedProduction(state, roll).Values.Sum();
        state.Robber = blocked.Position;
        var withRobber = Games.ExpectedProduction(state, roll);

        Assert.True(withRobber.Values.Sum() < withoutRobber);

        var before = Games.Hands(state);
        GameEngine.Apply(state, new RollDice(0));

        AssertGains(state, before, withRobber);
    }

    [Fact]
    public void Production_comes_out_of_the_bank()
    {
        var state = Games.SetupWithProductiveRoll(out var roll);
        var expected = Games.ExpectedProduction(state, roll);
        var bankBefore = TerrainExtensions.AllResources.ToDictionary(r => r, r => state.Bank[r]);

        GameEngine.Apply(state, new RollDice(0));

        foreach (var resource in TerrainExtensions.AllResources)
        {
            var paid = expected.Where(e => e.Key.Resource == resource).Sum(e => e.Value);
            Assert.Equal(bankBefore[resource] - paid, state.Bank[resource]);
        }
    }

    /// <summary>اگر بانک کم بیاورد و بیش از یک بازیکن طلبکار باشد، هیچ‌کس نمی‌گیرد.</summary>
    [Fact]
    public void Bank_shortage_with_several_claimants_pays_nobody()
    {
        var state = Games.FreshWithKnownRoll(out var roll);
        var tile = state.Board.TilesWithNumber(roll).First(t => t.Resource is not null);
        var resource = tile.Resource!.Value;

        // دو گوشه‌ی غیرمجاور از همان خانه تا قاعده‌ی فاصله هم رعایت شود.
        var corners = CleanCorners(state, tile, roll);
        var first = corners[0];
        var second = corners.First(v => v != first && !first.AdjacentVertices().Contains(v));

        state.PlaceBuilding(first, new Building(0, BuildingKind.Settlement));
        state.PlaceBuilding(second, new Building(1, BuildingKind.Settlement));

        // بانک فقط یک کارت دارد ولی دو نفر طلبکارند.
        state.BankTake(resource, state.Bank[resource] - 1);

        var before = Games.Hands(state);
        var result = GameEngine.Apply(state, new RollDice(0));

        Assert.Contains(result.Events, e => e is ProductionSkippedForBank p && p.Resource == resource);
        Assert.Equal(1, state.Bank[resource]);

        foreach (var player in state.Players)
        {
            Assert.Equal(before[(player.Index, resource)], player[resource]);
        }
    }

    /// <summary>اگر فقط یک بازیکن طلبکار باشد، هرچه در بانک مانده را می‌گیرد.</summary>
    [Fact]
    public void Bank_shortage_with_one_claimant_pays_what_is_left()
    {
        var state = Games.FreshWithKnownRoll(out var roll);
        var tile = state.Board.TilesWithNumber(roll).First(t => t.Resource is not null);
        var resource = tile.Resource!.Value;

        // شهر دو کارت می‌خواهد ولی فقط یکی در بانک مانده است.
        state.PlaceBuilding(CleanCorners(state, tile, roll)[0], new Building(0, BuildingKind.City));
        state.BankTake(resource, state.Bank[resource] - 1);

        GameEngine.Apply(state, new RollDice(0));

        Assert.Equal(1, state.Player(0)[resource]);
        Assert.Equal(0, state.Bank[resource]);
    }

    [Fact]
    public void A_city_produces_two_of_its_resource()
    {
        var state = Games.FreshWithKnownRoll(out var roll);
        var tile = state.Board.TilesWithNumber(roll).First(t => t.Resource is not null);

        state.PlaceBuilding(CleanCorners(state, tile, roll)[0], new Building(0, BuildingKind.City));

        GameEngine.Apply(state, new RollDice(0));

        Assert.Equal(2, state.Player(0)[tile.Resource!.Value]);
    }

    /// <summary>
    /// گوشه‌هایی از این خانه که خانه‌ی دیگری با همان عدد به آن‌ها نمی‌رسد؛ اینطور
    /// تولیدِ سنجیده‌شده فقط از همین خانه می‌آید.
    /// </summary>
    private static List<VertexId> CleanCorners(GameState state, HexTile tile, int roll) =>
        tile.Vertices()
            .Where(v => v.TouchingHexes().Count(h => state.Board.TileAt(h)?.Number == roll) == 1)
            .ToList();

    [Fact]
    public void Only_the_current_player_may_roll()
    {
        var state = Games.New(players: 3);
        Games.RunSetup(state);

        var result = GameEngine.Apply(state, new RollDice(1));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotYourTurn, result.Error);
    }

    [Fact]
    public void Rolling_twice_in_a_turn_is_rejected()
    {
        var state = Games.SetupWithProductiveRoll(out _);
        GameEngine.Apply(state, new RollDice(0));

        var result = GameEngine.Apply(state, new RollDice(0));

        Assert.False(result.Success);
        Assert.Equal(GameError.WrongPhase, result.Error);
    }

    private static void AssertGains(
        GameState state,
        Dictionary<(int Player, Resource Resource), int> before,
        Dictionary<(int Player, Resource Resource), int> expected)
    {
        foreach (var player in state.Players)
        {
            foreach (var resource in TerrainExtensions.AllResources)
            {
                var gained = player[resource] - before[(player.Index, resource)];
                Assert.Equal(expected.GetValueOrDefault((player.Index, resource)), gained);
            }
        }
    }
}
