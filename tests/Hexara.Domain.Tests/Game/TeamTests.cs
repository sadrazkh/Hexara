using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Domain.Tests.Game;

public class TeamTests
{
    private static readonly Axial Center = new(0, 0);

    private static GameState TeamGame(int players = 4, params int[] teams) =>
        Games.New(players, tweak: o => o with
        {
            VictoryPoints = 6,
            Teams = new TeamAssignment(teams.Length > 0 ? teams : [0, 1, 0, 1])
        });

    // ── تقسیم تیمی ───────────────────────────────────────────────────────

    [Fact]
    public void Teammates_and_opponents_are_told_apart()
    {
        var teams = new TeamAssignment([0, 1, 0, 1]);

        Assert.True(teams.AreTeammates(0, 2));
        Assert.True(teams.AreTeammates(1, 3));
        Assert.False(teams.AreTeammates(0, 1));

        // خودِ آدم هم‌تیمیِ خودش نیست — وگرنه قواعدِ «نه روی هم‌تیمی» خودش را می‌بندند.
        Assert.False(teams.AreTeammates(0, 0));
    }

    [Fact]
    public void A_team_knows_its_seats()
    {
        var teams = new TeamAssignment([0, 1, 0, 1]);

        Assert.Equal([0, 2], teams.SeatsOf(0));
        Assert.Equal([2], teams.Teammates(0));
        Assert.Equal([0, 1], teams.Teams);
    }

    [Theory]
    [InlineData(new[] { 0, 1, 0 }, 4, false)] // تعداد صندلی نمی‌خواند
    [InlineData(new[] { 0, 0, 0, 0 }, 4, false)] // فقط یک تیم
    [InlineData(new[] { 0, 1, 0, 1 }, 4, true)]
    [InlineData(new[] { 0, 1, 2 }, 3, true)]
    public void Only_a_real_split_is_valid(int[] seats, int players, bool expected) =>
        Assert.Equal(expected, new TeamAssignment(seats).IsValidFor(players));

    [Fact]
    public void An_invalid_split_is_refused_at_creation()
    {
        var options = new GameOptions
        {
            PlayerCount = 4,
            Teams = new TeamAssignment([0, 0, 0, 0])
        };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void The_alternating_split_puts_neighbours_on_opposite_teams()
    {
        var teams = TeamAssignment.Alternating(4);

        Assert.Equal([0, 1, 0, 1], teams.BySeat);
        Assert.True(teams.IsValidFor(4));
    }

    /// <summary>
    /// برابری ساختاری لازم است، وگرنه رفت‌وبرگشت عکس وضعیت بی‌صدا نابرابر می‌شود.
    /// </summary>
    [Fact]
    public void Two_identical_splits_are_equal()
    {
        Assert.Equal(new TeamAssignment([0, 1, 0, 1]), new TeamAssignment([0, 1, 0, 1]));
        Assert.NotEqual(new TeamAssignment([0, 1, 0, 1]), new TeamAssignment([0, 1, 1, 0]));

        var a = new GameOptions { PlayerCount = 4, Teams = new TeamAssignment([0, 1, 0, 1]) };
        var b = new GameOptions { PlayerCount = 4, Teams = new TeamAssignment([0, 1, 0, 1]) };
        Assert.Equal(a, b);
    }

    [Fact]
    public void A_team_game_survives_the_snapshot_round_trip()
    {
        var state = TeamGame();
        var restored = GameState.Restore(state.ToSnapshot());

        Assert.Equal(state.Options, restored.Options);
        Assert.Equal([0, 1, 0, 1], restored.Options.Teams!.BySeat);
    }

    // ── امتیاز مشترک ─────────────────────────────────────────────────────

    [Fact]
    public void A_teams_points_are_added_up()
    {
        var state = TeamGame();
        state.Player(0).BuildingPoints = 2;
        state.Player(2).BuildingPoints = 3;
        state.Player(1).BuildingPoints = 4;

        Assert.Equal(5, state.ScoreOf(0));
        Assert.Equal(5, state.ScoreOf(2));
        Assert.Equal(4, state.ScoreOf(1));
    }

    [Fact]
    public void Without_teams_the_score_is_just_your_own()
    {
        var state = Games.New(players: 3);
        state.Player(0).BuildingPoints = 2;
        state.Player(1).BuildingPoints = 3;

        Assert.Equal(2, state.ScoreOf(0));
    }

    /// <summary>در تیم، کارت پیروزی پنهانِ هم‌تیمی هم به حساب می‌آید.</summary>
    [Fact]
    public void A_teammates_hidden_card_counts_towards_the_win()
    {
        var state = TeamGame();
        state.PlaceRoad(EdgeId.Of(Center, 0), 0);
        state.Player(0).BuildingPoints = 4;
        state.Player(2).VictoryPointCards = 1;

        Games.StartMainPhase(state, 0);
        Games.GiveSettlementCost(state, 0);

        var result = GameEngine.Apply(state, new BuildSettlement(0, VertexId.Of(Center, 0)));

        Assert.True(result.Success);
        Assert.Equal(6, result.Events.OfType<GameWon>().Single().VictoryPoints);
        Assert.Equal(TurnPhase.GameOver, state.Phase);
    }

    [Fact]
    public void The_whole_team_wins_together()
    {
        var state = TeamGame();
        state.PlaceRoad(EdgeId.Of(Center, 0), 0);
        state.Player(0).BuildingPoints = 3;
        state.Player(2).BuildingPoints = 2;

        Games.StartMainPhase(state, 0);
        Games.GiveSettlementCost(state, 0);
        GameEngine.Apply(state, new BuildSettlement(0, VertexId.Of(Center, 0)));

        Assert.Equal(0, state.Winner);
        Assert.Equal([0, 2], state.WinningSeats());
    }

    [Fact]
    public void In_a_solo_game_only_one_seat_wins()
    {
        var state = Games.New(players: 3, tweak: o => o with { VictoryPoints = 3 });
        state.PlaceRoad(EdgeId.Of(Center, 0), 0);
        state.Player(0).BuildingPoints = 2;

        Games.StartMainPhase(state, 0);
        Games.GiveSettlementCost(state, 0);
        GameEngine.Apply(state, new BuildSettlement(0, VertexId.Of(Center, 0)));

        Assert.Equal([0], state.WinningSeats());
    }

    [Fact]
    public void Nobody_has_won_before_the_end()
    {
        Assert.Empty(TeamGame().WinningSeats());
    }

    // ── دزد ──────────────────────────────────────────────────────────────

    /// <summary>از هم‌تیمی نمی‌شود دزدید.</summary>
    [Fact]
    public void A_teammate_is_never_a_robber_target()
    {
        var state = TeamGame();
        var tile = state.Board.Tiles.First(t => t.Terrain != Terrain.Desert);
        var corners = tile.Vertices().ToList();

        // صندلی ۲ هم‌تیمیِ صندلی ۰ است و صندلی ۱ حریف.
        state.PlaceBuilding(corners[0], new Building(2, BuildingKind.Settlement));
        state.PlaceBuilding(corners[2], new Building(1, BuildingKind.Settlement));
        Games.Give(state, 1, (Resource.Ore, 2));
        Games.Give(state, 2, (Resource.Ore, 2));

        Assert.Equal([1], GameEngine.RobberVictims(state, tile.Position, 0));
    }

    [Fact]
    public void Without_teams_everyone_is_fair_game()
    {
        var state = Games.New(players: 4);
        var tile = state.Board.Tiles.First(t => t.Terrain != Terrain.Desert);
        var corners = tile.Vertices().ToList();

        state.PlaceBuilding(corners[0], new Building(2, BuildingKind.Settlement));
        state.PlaceBuilding(corners[2], new Building(1, BuildingKind.Settlement));
        Games.Give(state, 1, (Resource.Ore, 2));
        Games.Give(state, 2, (Resource.Ore, 2));

        Assert.Equal([1, 2], GameEngine.RobberVictims(state, tile.Position, 0).Order());
    }

    [Fact]
    public void Naming_a_teammate_as_the_victim_is_rejected()
    {
        var state = TeamGame();
        var tile = state.Board.Tiles.First(t => t.Terrain != Terrain.Desert);
        state.PlaceBuilding(tile.Vertices().First(), new Building(2, BuildingKind.Settlement));
        Games.Give(state, 2, (Resource.Ore, 2));

        state.CurrentPlayer = 0;
        state.Phase = TurnPhase.MoveRobber;

        var result = GameEngine.Apply(state, new MoveRobber(0, tile.Position, 2));

        Assert.False(result.Success);
        Assert.Equal(GameError.InvalidVictim, result.Error);
    }
}
