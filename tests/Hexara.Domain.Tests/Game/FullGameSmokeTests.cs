using Hexara.Domain.Board;
using Hexara.Domain.Common;
using Hexara.Domain.Game;

namespace Hexara.Domain.Tests.Game;

/// <summary>
/// بازی‌های کامل با بازیکن تصادفی. هدف پیدا کردن ترکیب‌هایی است که تست‌های نقطه‌ای
/// نمی‌بینند: بن‌بست در مرحله‌ها، شکستن ثابت‌های وضعیت، یا استثنای غیرمنتظره.
/// </summary>
public class FullGameSmokeTests
{
    private const int MaxActions = 40_000;

    [Theory]
    [InlineData(2, 11UL)]
    [InlineData(3, 22UL)]
    [InlineData(4, 33UL)]
    [InlineData(6, 44UL)]
    public void A_random_game_reaches_a_winner_without_breaking_anything(int players, ulong seed)
    {
        var state = Games.New(players, seed);
        var rng = new Rng(seed * 31);
        var totalCards = state.Options.BankPerResource * TerrainExtensions.AllResources.Length;

        var actions = 0;
        while (state.Phase != TurnPhase.GameOver && actions < MaxActions)
        {
            var action = NextAction(state, rng);
            var result = GameEngine.Apply(state, action);

            Assert.True(result.Success, $"حرکت {action} رد شد: {result.Error}");
            actions++;

            AssertInvariants(state, totalCards);
        }

        Assert.Equal(TurnPhase.GameOver, state.Phase);
        Assert.NotNull(state.Winner);
        Assert.True(state.Player(state.Winner!.Value).VictoryPoints >= state.Options.VictoryPoints);
    }

    /// <summary>هیچ کارتی نباید از هیچ‌جا ساخته یا گم شود.</summary>
    private static void AssertInvariants(GameState state, int totalCards)
    {
        var inHands = state.Players.Sum(p => p.TotalCards);
        var inBank = state.Bank.Values.Sum();
        Assert.Equal(totalCards, inHands + inBank);

        foreach (var player in state.Players)
        {
            Assert.All(player.Resources.Values, amount => Assert.True(amount >= 0));
            Assert.True(player.RoadsLeft >= 0);
            Assert.True(player.SettlementsLeft >= 0);
            Assert.True(player.CitiesLeft >= 0);
            Assert.Equal(
                state.BuildingsOf(player.Index).Sum(b => b.Value.Points),
                player.BuildingPoints);
        }

        Assert.True(state.Players.Count(p => p.HasLongestRoad) <= 1);
        Assert.True(state.Players.Count(p => p.HasLargestArmy) <= 1);
    }

    private static GameAction NextAction(GameState state, Rng rng) => state.Phase switch
    {
        TurnPhase.SetupSettlement => new PlaceInitialSettlement(
            state.CurrentPlayer,
            Pick(GameEngine.LegalSettlementVertices(state, state.CurrentPlayer).ToList(), rng)),

        TurnPhase.SetupRoad => new PlaceInitialRoad(
            state.CurrentPlayer,
            Pick(SetupRoadOptions(state), rng)),

        TurnPhase.Roll => new RollDice(state.CurrentPlayer),

        TurnPhase.Discard => Discard(state),

        TurnPhase.MoveRobber => RobberMove(state, rng),

        _ => MainPhaseAction(state, rng)
    };

    private static List<EdgeId> SetupRoadOptions(GameState state) =>
        state.LastSetupSettlement!.Value.TouchingEdges()
            .Where(e => state.Board.ContainsEdge(e) && state.RoadAt(e) is null)
            .ToList();

    private static GameAction Discard(GameState state)
    {
        var (playerIndex, required) = state.PendingDiscards.First();
        var player = state.Player(playerIndex);

        var cards = new Dictionary<Resource, int>();
        var left = required;
        foreach (var resource in TerrainExtensions.AllResources)
        {
            var amount = Math.Min(left, player[resource]);
            if (amount > 0)
            {
                cards[resource] = amount;
                left -= amount;
            }
        }

        return new DiscardCards(playerIndex, cards);
    }

    private static GameAction RobberMove(GameState state, Rng rng)
    {
        var target = Pick(state.Board.Tiles.Where(t => t.Position != state.Robber).ToList(), rng).Position;
        var victims = GameEngine.RobberVictims(state, target, state.CurrentPlayer).ToList();

        return new MoveRobber(state.CurrentPlayer, target, victims.Count > 0 ? Pick(victims, rng) : null);
    }

    /// <summary>
    /// سیاست ساده: هرچه می‌تواند بسازد، وگرنه نوبت را تمام می‌کند. عمداً هوشمند نیست —
    /// فقط باید بازی را جلو ببرد تا به پیروزی برسد.
    /// </summary>
    private static GameAction MainPhaseAction(GameState state, Rng rng)
    {
        var index = state.CurrentPlayer;
        var player = state.Player(index);

        if (player.CanAfford(BuildCosts.City) && player.CitiesLeft > 0)
        {
            var settlements = state.BuildingsOf(index)
                .Where(b => b.Value.Kind == BuildingKind.Settlement)
                .Select(b => b.Key)
                .ToList();

            if (settlements.Count > 0)
            {
                return new BuildCity(index, Pick(settlements, rng));
            }
        }

        if (player.CanAfford(BuildCosts.Settlement) && player.SettlementsLeft > 0)
        {
            var spots = GameEngine.LegalSettlementVertices(state, index).ToList();
            if (spots.Count > 0)
            {
                return new BuildSettlement(index, Pick(spots, rng));
            }
        }

        if (player.CanAfford(BuildCosts.Road) && player.RoadsLeft > 0)
        {
            var edges = GameEngine.LegalRoadEdges(state, index).ToList();
            if (edges.Count > 0)
            {
                return new BuildRoad(index, Pick(edges, rng));
            }
        }

        if (player.CanAfford(BuildCosts.DevelopmentCard) && state.DevelopmentDeckCount > 0)
        {
            return new BuyDevelopmentCard(index);
        }

        if (PlayableCard(state, index) is { } card)
        {
            return card;
        }

        if (TradeUp(state, index) is { } trade)
        {
            return trade;
        }

        return new EndTurn(index);
    }

    private static GameAction? PlayableCard(GameState state, int index)
    {
        var player = state.Player(index);
        if (player.PlayedDevelopmentCardThisTurn)
        {
            return null;
        }

        if (player[DevelopmentCard.Knight] > 0)
        {
            var target = state.Board.Tiles.First(t => t.Position != state.Robber).Position;
            var victims = GameEngine.RobberVictims(state, target, index).ToList();
            return new PlayKnight(index, target, victims.Count > 0 ? victims[0] : null);
        }

        if (player[DevelopmentCard.YearOfPlenty] > 0)
        {
            // دو منبع متفاوت انتخاب می‌شود تا موجودی بانک برای هرکدام کافی باشد.
            var affordable = TerrainExtensions.AllResources.Where(r => state.Bank[r] > 0).ToList();
            if (affordable.Count >= 2)
            {
                return new PlayYearOfPlenty(index, affordable[0], affordable[1]);
            }
        }

        if (player[DevelopmentCard.Monopoly] > 0)
        {
            return new PlayMonopoly(index, Resource.Wool);
        }

        if (player[DevelopmentCard.RoadBuilding] > 0 && player.RoadsLeft > 0)
        {
            var edges = GameEngine.LegalRoadEdges(state, index).ToList();
            if (edges.Count > 0)
            {
                return new PlayRoadBuilding(index, edges[0], null);
            }
        }

        return null;
    }

    /// <summary>کارت اضافی را با بانک به چیزی که کم دارد تبدیل می‌کند تا بازی قفل نشود.</summary>
    private static GameAction? TradeUp(GameState state, int index)
    {
        var player = state.Player(index);

        foreach (var give in TerrainExtensions.AllResources)
        {
            var rate = GameEngine.MaritimeRate(state, index, give);
            if (player[give] < rate)
            {
                continue;
            }

            var take = TerrainExtensions.AllResources
                .Where(r => r != give && player[r] == 0 && state.Bank[r] > 0)
                .Cast<Resource?>()
                .FirstOrDefault();

            if (take is { } wanted)
            {
                return new MaritimeTrade(index, give, wanted);
            }
        }

        return null;
    }

    private static T Pick<T>(IReadOnlyList<T> items, Rng rng) => items[rng.Next(items.Count)];
}
