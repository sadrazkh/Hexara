using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Domain.Tests.Game;

public class SnapshotTests
{
    [Fact]
    public void A_fresh_game_survives_the_round_trip()
    {
        var state = Games.New(players: 4, seed: 777);

        AssertEquivalent(state, GameState.Restore(state.ToSnapshot()));
    }

    [Fact]
    public void A_game_in_progress_survives_the_round_trip()
    {
        var state = RichState();

        AssertEquivalent(state, GameState.Restore(state.ToSnapshot()));
    }

    /// <summary>
    /// مهم‌ترین خاصیت: بازیِ بازیابی‌شده باید دقیقاً همان تاس‌های بازی اصلی را
    /// بیندازد، وگرنه ادامه‌ی بازی بعد از ری‌استارت سرور قابل بازپخش نیست.
    /// </summary>
    [Fact]
    public void The_restored_game_rolls_the_same_dice()
    {
        var original = Games.SetupWithProductiveRoll(out _);
        var restored = GameState.Restore(original.ToSnapshot());

        for (var i = 0; i < 20; i++)
        {
            var a = GameEngine.Apply(original, new RollDice(original.CurrentPlayer));
            var b = GameEngine.Apply(restored, new RollDice(restored.CurrentPlayer));

            Assert.Equal(a.Success, b.Success);
            Assert.Equal(original.LastRoll, restored.LastRoll);

            // هر دو بازی را به مرحله‌ی بعد می‌بریم تا تاس بعدی انداخته شود.
            Advance(original);
            Advance(restored);
        }
    }

    [Fact]
    public void The_board_comes_back_exactly_as_it_was()
    {
        var state = Games.New(players: 3, seed: 4242);
        var restored = GameState.Restore(state.ToSnapshot());

        Assert.Equal(
            state.Board.Tiles.OrderBy(t => t.Position.Q).ThenBy(t => t.Position.R).Select(t => $"{t.Position}{t.Terrain}{t.Number}"),
            restored.Board.Tiles.OrderBy(t => t.Position.Q).ThenBy(t => t.Position.R).Select(t => $"{t.Position}{t.Terrain}{t.Number}"));

        Assert.Equal(
            state.Board.Ports.Select(p => $"{p.Edge}{p.Resource}").Order(),
            restored.Board.Ports.Select(p => $"{p.Edge}{p.Resource}").Order());
    }

    [Fact]
    public void A_pending_trade_comes_back()
    {
        var state = Games.New(players: 3);
        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (Resource.Lumber, 2));
        Games.Give(state, 1, (Resource.Ore, 1));

        GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Lumber] = 2 },
            new Dictionary<Resource, int> { [Resource.Ore] = 1 },
            []));
        // پیشنهاد هنوز بی‌جواب است. عمداً: پذیرش خودش معامله را می‌بندد، پس
        // پیشنهادِ پذیرفته‌شده اصلاً روی میز نمی‌ماند که ذخیره شود.
        var restored = GameState.Restore(state.ToSnapshot());

        Assert.NotNull(restored.PendingTrade);
        Assert.Equal(0, restored.PendingTrade!.Proposer);
        Assert.Equal(TradeResponse.Pending, restored.PendingTrade.Responses[1]);
        Assert.Equal(TradeResponse.Pending, restored.PendingTrade.Responses[2]);

        // و معامله روی وضعیت بازیابی‌شده هم انجام می‌شود.
        Assert.True(GameEngine.Apply(restored, new RespondToTrade(1, true)).Success);
        Assert.Equal(1, restored.Player(0)[Resource.Ore]);
        Assert.Null(restored.PendingTrade);
    }

    [Fact]
    public void Pending_discards_come_back()
    {
        var state = Games.SetupWithNextRoll(7);
        Games.Give(state, 1, (Resource.Wool, 9));
        GameEngine.Apply(state, new RollDice(0));

        var restored = GameState.Restore(state.ToSnapshot());

        Assert.Equal(TurnPhase.Discard, restored.Phase);
        Assert.Equal(state.PendingDiscards[1], restored.PendingDiscards[1]);
    }

    [Fact]
    public void The_development_deck_keeps_its_order()
    {
        var state = Games.New(players: 2);
        var restored = GameState.Restore(state.ToSnapshot());

        Assert.Equal(state.DevelopmentDeckCount, restored.DevelopmentDeckCount);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(state.DrawDevelopmentCard(), restored.DrawDevelopmentCard());
        }
    }

    [Fact]
    public void An_unknown_schema_version_is_refused()
    {
        var snapshot = Games.New(players: 2).ToSnapshot() with { SchemaVersion = 99 };

        Assert.Throws<NotSupportedException>(() => GameState.Restore(snapshot));
    }

    /// <summary>وضعیتی که تقریباً همه‌ی گوشه‌های وضعیت را پر کرده باشد.</summary>
    private static GameState RichState()
    {
        var state = Games.New(players: 3, seed: 31337);
        Games.RunSetup(state);
        Games.StartMainPhase(state, 1);

        Games.Give(state, 0, (Resource.Ore, 3), (Resource.Grain, 2));
        Games.Give(state, 1, (Resource.Lumber, 4));
        Games.GiveDevelopmentCard(state, 1, DevelopmentCard.Knight, 2);
        Games.GiveDevelopmentCard(state, 2, DevelopmentCard.Monopoly);
        state.Player(2).AddNewDevelopmentCard(DevelopmentCard.YearOfPlenty);

        state.Player(1).KnightsPlayed = 3;
        state.Player(1).HasLargestArmy = true;
        state.Player(0).HasLongestRoad = true;
        state.Player(0).LongestRoadLength = 6;
        state.Player(2).VictoryPointCards = 1;
        state.Player(1).PlayedDevelopmentCardThisTurn = true;
        state.Robber = state.Board.Tiles.First(t => t.Terrain != Terrain.Desert).Position;
        state.Die1 = 3;
        state.Die2 = 5;

        return state;
    }

    private static void Advance(GameState state)
    {
        // بعد از تاس ممکن است سر مرحله‌ی دور ریختن یا دزد باشیم؛ همه را رد می‌کنیم.
        while (state.Phase is TurnPhase.Discard or TurnPhase.MoveRobber)
        {
            if (state.Phase == TurnPhase.Discard)
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

                GameEngine.Apply(state, new DiscardCards(playerIndex, cards));
                continue;
            }

            var target = state.Board.Tiles.First(t => t.Position != state.Robber).Position;
            var victims = GameEngine.RobberVictims(state, target, state.CurrentPlayer).ToList();
            int? victim = victims.Count > 0 ? victims[0] : null;
            GameEngine.Apply(state, new MoveRobber(state.CurrentPlayer, target, victim));
        }

        GameEngine.Apply(state, new EndTurn(state.CurrentPlayer));
    }

    private static void AssertEquivalent(GameState expected, GameState actual)
    {
        Assert.Equal(expected.Phase, actual.Phase);
        Assert.Equal(expected.CurrentPlayer, actual.CurrentPlayer);
        Assert.Equal(expected.TurnNumber, actual.TurnNumber);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.Robber, actual.Robber);
        Assert.Equal(expected.Die1, actual.Die1);
        Assert.Equal(expected.Die2, actual.Die2);
        Assert.Equal(expected.Winner, actual.Winner);
        Assert.Equal(expected.SetupStep, actual.SetupStep);
        Assert.Equal(expected.LastSetupSettlement, actual.LastSetupSettlement);
        Assert.Equal(expected.DevelopmentDeckCount, actual.DevelopmentDeckCount);
        Assert.Equal(expected.Options, actual.Options);

        Assert.Equal(
            expected.Buildings.Select(b => $"{b.Key}{b.Value.PlayerIndex}{b.Value.Kind}").Order(),
            actual.Buildings.Select(b => $"{b.Key}{b.Value.PlayerIndex}{b.Value.Kind}").Order());

        Assert.Equal(
            expected.Roads.Select(r => $"{r.Key}{r.Value}").Order(),
            actual.Roads.Select(r => $"{r.Key}{r.Value}").Order());

        foreach (var resource in TerrainExtensions.AllResources)
        {
            Assert.Equal(expected.Bank[resource], actual.Bank[resource]);
        }

        Assert.Equal(expected.Players.Count, actual.Players.Count);
        for (var i = 0; i < expected.Players.Count; i++)
        {
            var a = expected.Player(i);
            var b = actual.Player(i);

            Assert.Equal(a.Id, b.Id);
            Assert.Equal(a.SettlementsLeft, b.SettlementsLeft);
            Assert.Equal(a.CitiesLeft, b.CitiesLeft);
            Assert.Equal(a.RoadsLeft, b.RoadsLeft);
            Assert.Equal(a.BuildingPoints, b.BuildingPoints);
            Assert.Equal(a.VictoryPointCards, b.VictoryPointCards);
            Assert.Equal(a.VictoryPoints, b.VictoryPoints);
            Assert.Equal(a.HasLongestRoad, b.HasLongestRoad);
            Assert.Equal(a.HasLargestArmy, b.HasLargestArmy);
            Assert.Equal(a.LongestRoadLength, b.LongestRoadLength);
            Assert.Equal(a.KnightsPlayed, b.KnightsPlayed);
            Assert.Equal(a.PlayedDevelopmentCardThisTurn, b.PlayedDevelopmentCardThisTurn);
            Assert.Equal(a.TotalCards, b.TotalCards);
            Assert.Equal(a.TotalDevelopmentCards, b.TotalDevelopmentCards);

            foreach (var resource in TerrainExtensions.AllResources)
            {
                Assert.Equal(a[resource], b[resource]);
            }

            foreach (var card in Enum.GetValues<DevelopmentCard>())
            {
                Assert.Equal(a[card], b[card]);
            }
        }
    }
}
