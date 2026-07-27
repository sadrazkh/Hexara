using Hexara.Domain.Board;
using Hexara.Domain.Common;
using Hexara.Domain.Game;

namespace Hexara.Domain.Tests.Game;

/// <summary>
/// بازی‌های کامل که کاملاً با <see cref="BotPlayer"/> جلو می‌روند.
///
/// همان باتی که در تولید جای بازیکن غایب را می‌گیرد اینجا کل بازی را می‌برد، پس
/// هر بن‌بست یا حرکت غیرقانونی‌ای که بات بسازد همین‌جا لو می‌رود. تست‌های نقطه‌ای
/// این را نمی‌گیرند: مسئله ترکیبِ حالت‌هاست، نه یک حالت مشخص.
/// </summary>
public class FullGameSmokeTests
{
    private const int MaxActions = 40_000;

    [Theory]
    [InlineData(2, 11UL)]
    [InlineData(3, 22UL)]
    [InlineData(4, 33UL)]
    [InlineData(6, 44UL)]
    public void A_game_played_entirely_by_bots_reaches_a_winner(int players, ulong seed)
    {
        var state = Games.New(players, seed);
        var rng = new Rng(seed * 31);
        var totalCards = state.Options.BankPerResource * TerrainExtensions.AllResources.Length;

        var actions = 0;
        while (state.Phase != TurnPhase.GameOver && actions < MaxActions)
        {
            var seat = BotPlayer.SeatsToAct(state).First();
            var action = BotPlayer.NextAction(state, seat, rng);

            Assert.NotNull(action);

            var result = GameEngine.Apply(state, action!);
            Assert.True(result.Success, $"بات حرکت غیرقانونی داد: {action} → {result.Error}");

            actions++;
            AssertInvariants(state, totalCards);
        }

        Assert.Equal(TurnPhase.GameOver, state.Phase);
        Assert.NotNull(state.Winner);
        Assert.True(state.Player(state.Winner!.Value).VictoryPoints >= state.Options.VictoryPoints);
    }

    /// <summary>
    /// بات باید برای هر صندلی که منتظرش هستند حرکتی بدهد و برای بقیه هیچ — وگرنه
    /// یا بازی گیر می‌کند یا بات به جای دیگری بازی می‌کند.
    /// </summary>
    [Theory]
    [InlineData(3, 5UL)]
    [InlineData(4, 8UL)]
    public void Only_the_seats_that_owe_a_decision_get_a_move(int players, ulong seed)
    {
        var state = Games.New(players, seed);
        var rng = new Rng(seed);

        for (var step = 0; step < 600 && state.Phase != TurnPhase.GameOver; step++)
        {
            var owing = BotPlayer.SeatsToAct(state).ToHashSet();
            Assert.NotEmpty(owing);

            for (var seat = 0; seat < players; seat++)
            {
                var action = BotPlayer.NextAction(state, seat, rng);

                if (owing.Contains(seat))
                {
                    Assert.NotNull(action);
                    Assert.Equal(seat, action!.PlayerIndex);
                }
                else
                {
                    Assert.Null(action);
                }
            }

            var next = owing.First();
            Assert.True(GameEngine.Apply(state, BotPlayer.NextAction(state, next, rng)!).Success);
        }
    }

    /// <summary>بعد از پایان بازی، بات دیگر حرکتی پیشنهاد نمی‌دهد.</summary>
    [Fact]
    public void A_finished_game_asks_for_nothing()
    {
        var state = Games.New(players: 2, seed: 11);
        var rng = new Rng(1);

        while (state.Phase != TurnPhase.GameOver)
        {
            var seat = BotPlayer.SeatsToAct(state).First();
            GameEngine.Apply(state, BotPlayer.NextAction(state, seat, rng)!);
        }

        Assert.Empty(BotPlayer.SeatsToAct(state));
        Assert.Null(BotPlayer.NextAction(state, 0, rng));
        Assert.Null(BotPlayer.NextAction(state, 1, rng));
    }

    /// <summary>با seed یکسان، بات دقیقاً همان بازی را تکرار می‌کند.</summary>
    [Fact]
    public void The_bot_is_reproducible()
    {
        Assert.Equal(Playout(3, 77), Playout(3, 77));
        Assert.NotEqual(Playout(3, 77), Playout(3, 78));
    }

    /// <summary>یک صندلی خارج از بازی هرگز حرکتی نمی‌گیرد.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void An_impossible_seat_gets_nothing(int seat)
    {
        var state = Games.New(players: 3);

        Assert.Null(BotPlayer.NextAction(state, seat, new Rng(1)));
    }

    private static string Playout(int players, ulong seed)
    {
        var state = Games.New(players, seed);
        var rng = new Rng(seed * 7);
        var log = new List<string>();

        for (var step = 0; step < 400 && state.Phase != TurnPhase.GameOver; step++)
        {
            var seat = BotPlayer.SeatsToAct(state).First();
            var action = BotPlayer.NextAction(state, seat, rng)!;
            log.Add(action.ToString()!);
            GameEngine.Apply(state, action);
        }

        return string.Join("\n", log);
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
}
