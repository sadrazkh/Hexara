using Hexara.Domain.Game;

namespace Hexara.Domain.Tests.Game;

public class LargestArmyTests
{
    /// <summary>یک شوالیه بازی می‌کند و دزد را به خانه‌ای بی‌ساختمان می‌برد.</summary>
    private static MoveResult PlayOneKnight(GameState state, int player)
    {
        Games.GiveDevelopmentCard(state, player, DevelopmentCard.Knight);
        Games.StartMainPhase(state, player);
        state.Player(player).PlayedDevelopmentCardThisTurn = false;

        return GameEngine.Apply(state, new PlayKnight(player, Games.EmptyHex(state), null));
    }

    [Fact]
    public void Two_knights_are_not_enough()
    {
        var state = Games.New(players: 3);

        PlayOneKnight(state, 0);
        PlayOneKnight(state, 0);

        Assert.Equal(2, state.Player(0).KnightsPlayed);
        Assert.Null(state.LargestArmyHolder);
    }

    [Fact]
    public void The_third_knight_takes_the_card_and_two_points()
    {
        var state = Games.New(players: 3);

        PlayOneKnight(state, 0);
        PlayOneKnight(state, 0);
        var result = PlayOneKnight(state, 0);

        Assert.Equal(0, state.LargestArmyHolder);
        Assert.True(state.Player(0).HasLargestArmy);
        Assert.Equal(2, state.Player(0).VictoryPoints);
        Assert.Contains(result.Events, e => e is LargestArmyChanged { PlayerIndex: 0, Knights: 3 });
    }

    /// <summary>برای گرفتن کارت باید از دارنده جلو زد، نه اینکه با او مساوی شد.</summary>
    [Fact]
    public void A_tie_does_not_move_the_card()
    {
        var state = Games.New(players: 3);

        for (var i = 0; i < 3; i++)
        {
            PlayOneKnight(state, 0);
        }

        for (var i = 0; i < 3; i++)
        {
            PlayOneKnight(state, 1);
        }

        Assert.Equal(3, state.Player(1).KnightsPlayed);
        Assert.Equal(0, state.LargestArmyHolder);
    }

    [Fact]
    public void A_bigger_army_takes_the_card()
    {
        var state = Games.New(players: 3);

        for (var i = 0; i < 3; i++)
        {
            PlayOneKnight(state, 0);
        }

        for (var i = 0; i < 4; i++)
        {
            PlayOneKnight(state, 1);
        }

        Assert.Equal(1, state.LargestArmyHolder);
        Assert.False(state.Player(0).HasLargestArmy);
        Assert.Equal(0, state.Player(0).VictoryPoints);
        Assert.Equal(2, state.Player(1).VictoryPoints);
    }

    [Fact]
    public void A_knight_moves_the_robber()
    {
        var state = Games.New(players: 3);
        var before = state.Robber;

        var result = PlayOneKnight(state, 0);

        Assert.True(result.Success);
        Assert.NotEqual(before, state.Robber);
        Assert.Contains(result.Events, e => e is RobberMoved);
    }

    [Fact]
    public void A_knight_still_has_to_move_the_robber_somewhere_new()
    {
        var state = Games.New(players: 3);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.Knight);
        Games.StartMainPhase(state, 0);

        var result = GameEngine.Apply(state, new PlayKnight(0, state.Robber, null));

        Assert.False(result.Success);
        Assert.Equal(GameError.RobberMustChangeHex, result.Error);
        Assert.Equal(1, state.Player(0)[DevelopmentCard.Knight]);
        Assert.Equal(0, state.Player(0).KnightsPlayed);
    }

    /// <summary>ارتش بزرگ می‌تواند خودش بازی را تمام کند.</summary>
    [Fact]
    public void Largest_army_can_win_the_game()
    {
        var state = Games.New(players: 3, tweak: o => o with { VictoryPoints = 4 });
        state.Player(0).BuildingPoints = 2;

        PlayOneKnight(state, 0);
        PlayOneKnight(state, 0);
        var result = PlayOneKnight(state, 0);

        Assert.Equal(4, state.Player(0).VictoryPoints);
        Assert.Equal(TurnPhase.GameOver, state.Phase);
        Assert.Contains(result.Events, e => e is GameWon { PlayerIndex: 0 });
    }
}
