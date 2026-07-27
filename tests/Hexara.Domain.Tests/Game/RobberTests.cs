using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Domain.Tests.Game;

public class RobberTests
{
    [Fact]
    public void Seven_asks_the_rich_players_to_discard()
    {
        var state = Games.SetupWithNextRoll(7);
        Games.Give(state, 1, (Resource.Wool, 9));

        var expected = state.Player(1).TotalCards / 2;
        var result = GameEngine.Apply(state, new RollDice(0));

        Assert.Equal(TurnPhase.Discard, state.Phase);
        Assert.Equal(expected, state.PendingDiscards[1]);
        Assert.DoesNotContain(0, state.PendingDiscards.Keys);
        Assert.Contains(result.Events, e => e is DiscardRequired);
    }

    [Fact]
    public void Seven_with_small_hands_goes_straight_to_the_robber()
    {
        var state = Games.SetupWithNextRoll(7);

        GameEngine.Apply(state, new RollDice(0));

        Assert.Equal(TurnPhase.MoveRobber, state.Phase);
        Assert.Empty(state.PendingDiscards);
    }

    [Fact]
    public void Discarding_the_wrong_amount_is_rejected()
    {
        var state = Games.SetupWithNextRoll(7);
        Games.Give(state, 1, (Resource.Wool, 9));
        GameEngine.Apply(state, new RollDice(0));

        var result = GameEngine.Apply(state, new DiscardCards(1, new Dictionary<Resource, int> { [Resource.Wool] = 1 }));

        Assert.False(result.Success);
        Assert.Equal(GameError.WrongDiscardAmount, result.Error);
    }

    [Fact]
    public void Discarding_cards_you_do_not_have_is_rejected()
    {
        var state = Games.SetupWithNextRoll(7);
        Games.Give(state, 1, (Resource.Wool, 9));
        GameEngine.Apply(state, new RollDice(0));

        var required = state.PendingDiscards[1];
        var result = GameEngine.Apply(state, new DiscardCards(1, new Dictionary<Resource, int> { [Resource.Ore] = required }));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotEnoughCardsToDiscard, result.Error);
    }

    [Fact]
    public void Discarding_returns_the_cards_to_the_bank_and_moves_on()
    {
        var state = Games.SetupWithNextRoll(7);
        Games.Give(state, 1, (Resource.Wool, 9));
        GameEngine.Apply(state, new RollDice(0));

        var required = state.PendingDiscards[1];
        var bankBefore = state.Bank[Resource.Wool];

        var result = GameEngine.Apply(state, new DiscardCards(1, new Dictionary<Resource, int> { [Resource.Wool] = required }));

        Assert.True(result.Success);
        Assert.Equal(bankBefore + required, state.Bank[Resource.Wool]);
        Assert.Empty(state.PendingDiscards);
        Assert.Equal(TurnPhase.MoveRobber, state.Phase);
    }

    [Fact]
    public void A_player_who_owes_nothing_cannot_discard()
    {
        var state = Games.SetupWithNextRoll(7);
        Games.Give(state, 1, (Resource.Wool, 9));
        GameEngine.Apply(state, new RollDice(0));

        var result = GameEngine.Apply(state, new DiscardCards(2, new Dictionary<Resource, int> { [Resource.Wool] = 1 }));

        Assert.False(result.Success);
        Assert.Equal(GameError.NothingToDiscard, result.Error);
    }

    [Fact]
    public void The_robber_has_to_change_hex()
    {
        var state = Games.SetupWithNextRoll(7);
        GameEngine.Apply(state, new RollDice(0));

        var result = GameEngine.Apply(state, new MoveRobber(0, state.Robber, null));

        Assert.False(result.Success);
        Assert.Equal(GameError.RobberMustChangeHex, result.Error);
    }

    [Fact]
    public void The_robber_stays_on_the_board()
    {
        var state = Games.SetupWithNextRoll(7);
        GameEngine.Apply(state, new RollDice(0));

        var result = GameEngine.Apply(state, new MoveRobber(0, new Axial(9, 9), null));

        Assert.False(result.Success);
        Assert.Equal(GameError.HexNotOnBoard, result.Error);
    }

    [Fact]
    public void Moving_the_robber_to_an_empty_hex_ends_the_sequence()
    {
        var state = Games.SetupWithNextRoll(7);
        GameEngine.Apply(state, new RollDice(0));

        var empty = state.Board.Tiles
            .First(t => t.Position != state.Robber && t.Vertices().All(v => state.BuildingAt(v) is null));

        var result = GameEngine.Apply(state, new MoveRobber(0, empty.Position, null));

        Assert.True(result.Success);
        Assert.Equal(empty.Position, state.Robber);
        Assert.Equal(TurnPhase.Main, state.Phase);
        Assert.Contains(result.Events, e => e is RobberMoved);
        Assert.DoesNotContain(result.Events, e => e is ResourceStolen);
    }

    [Fact]
    public void Stealing_moves_exactly_one_card()
    {
        var state = Games.New(players: 3);
        var tile = state.Board.Tiles.First(t => t.Terrain != Terrain.Desert);
        state.PlaceBuilding(tile.Vertices().First(), new Building(1, BuildingKind.Settlement));
        Games.Give(state, 1, (Resource.Ore, 3));

        state.CurrentPlayer = 0;
        state.Phase = TurnPhase.MoveRobber;

        var result = GameEngine.Apply(state, new MoveRobber(0, tile.Position, 1));

        Assert.True(result.Success);
        Assert.Equal(2, state.Player(1).TotalCards);
        Assert.Equal(1, state.Player(0).TotalCards);
        Assert.Equal(Resource.Ore, result.Events.OfType<ResourceStolen>().Single().Resource);
    }

    [Fact]
    public void A_victim_must_be_named_when_there_is_one()
    {
        var state = Games.New(players: 3);
        var tile = state.Board.Tiles.First(t => t.Terrain != Terrain.Desert);
        state.PlaceBuilding(tile.Vertices().First(), new Building(1, BuildingKind.Settlement));
        Games.Give(state, 1, (Resource.Ore, 1));

        state.CurrentPlayer = 0;
        state.Phase = TurnPhase.MoveRobber;

        var result = GameEngine.Apply(state, new MoveRobber(0, tile.Position, null));

        Assert.False(result.Success);
        Assert.Equal(GameError.VictimRequired, result.Error);
    }

    [Fact]
    public void A_player_with_no_building_on_the_hex_cannot_be_robbed()
    {
        var state = Games.New(players: 3);
        var tile = state.Board.Tiles.First(t => t.Terrain != Terrain.Desert);
        state.PlaceBuilding(tile.Vertices().First(), new Building(1, BuildingKind.Settlement));
        Games.Give(state, 1, (Resource.Ore, 1));
        Games.Give(state, 2, (Resource.Ore, 1));

        state.CurrentPlayer = 0;
        state.Phase = TurnPhase.MoveRobber;

        var result = GameEngine.Apply(state, new MoveRobber(0, tile.Position, 2));

        Assert.False(result.Success);
        Assert.Equal(GameError.InvalidVictim, result.Error);
    }

    [Fact]
    public void An_empty_handed_neighbour_is_not_a_victim()
    {
        var state = Games.New(players: 3);
        var tile = state.Board.Tiles.First(t => t.Terrain != Terrain.Desert);
        state.PlaceBuilding(tile.Vertices().First(), new Building(1, BuildingKind.Settlement));

        Assert.Empty(GameEngine.RobberVictims(state, tile.Position, 0));
    }

    /// <summary>وریانت دزد مهربان: بازیکن کم‌امتیاز مصون است.</summary>
    [Fact]
    public void Friendly_robber_protects_a_player_who_is_behind()
    {
        var state = Games.New(players: 3, tweak: o => o with { FriendlyRobber = true });
        var tile = state.Board.Tiles.First(t => t.Terrain != Terrain.Desert);
        state.PlaceBuilding(tile.Vertices().First(), new Building(1, BuildingKind.Settlement));
        state.Player(1).BuildingPoints = 2;
        Games.Give(state, 1, (Resource.Ore, 1));

        Assert.Empty(GameEngine.RobberVictims(state, tile.Position, 0));

        state.Player(1).BuildingPoints = 3;
        Assert.Equal([1], GameEngine.RobberVictims(state, tile.Position, 0));
    }

    [Fact]
    public void Only_the_current_player_moves_the_robber()
    {
        var state = Games.SetupWithNextRoll(7);
        GameEngine.Apply(state, new RollDice(0));

        var other = state.Board.Tiles.First(t => t.Position != state.Robber);
        var result = GameEngine.Apply(state, new MoveRobber(1, other.Position, null));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotYourTurn, result.Error);
    }

    [Fact]
    public void Building_is_not_allowed_while_the_robber_is_pending()
    {
        var state = Games.SetupWithNextRoll(7);
        GameEngine.Apply(state, new RollDice(0));

        var result = GameEngine.Apply(state, new EndTurn(0));

        Assert.False(result.Success);
        Assert.Equal(GameError.WrongPhase, result.Error);
    }
}
