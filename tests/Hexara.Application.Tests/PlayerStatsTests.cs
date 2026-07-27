using Hexara.Application.Common.Interfaces;
using Hexara.Application.Games;
using Hexara.Application.Rooms;
using Hexara.Domain.Board;
using Hexara.Domain.Game;
using Hexara.Infrastructure.Persistence;

namespace Hexara.Application.Tests;

/// <summary>
/// کارنامه‌ی بازیکن‌ها. ستون‌های ‎GamesPlayed‎ و ‎GamesWon‎ از فاز اول روی کاربر
/// بودند ولی هیچ‌وقت چیزی در آن‌ها نوشته نمی‌شد — این تست‌ها همان را می‌بندند.
/// </summary>
public class PlayerStatsTests
{
    private static GameService NewGames(SqliteFixture fixture, AppDbContext context, out IPlayerStats stats)
    {
        stats = new PlayerStats(context);
        return new GameService(new GameRepository(context, fixture.Clock), fixture.Clock, stats);
    }

    /// <summary>وضعیتی که یک حرکت تا پیروزی فاصله دارد.</summary>
    private static GameState OneMoveFromWinning(
        GameState state,
        int seat,
        int points,
        TeamAssignment? teams = null)
    {
        var snapshot = state.ToSnapshot();
        var road = EdgeId.Of(new Axial(0, 0), 0);

        return GameState.Restore(snapshot with
        {
            Options = snapshot.Options with { VictoryPoints = points + 1, Teams = teams },
            Phase = TurnPhase.Main,
            CurrentPlayer = seat,
            TurnNumber = 5,
            Roads = [new RoadSnapshot(road.Hex.Q, road.Hex.R, road.Side, seat)],
            Players =
            [
                .. snapshot.Players.Select(p => p.Index == seat
                    ? p with
                    {
                        BuildingPoints = points,
                        Resources = new Dictionary<Resource, int>
                        {
                            [Resource.Lumber] = 1,
                            [Resource.Brick] = 1,
                            [Resource.Wool] = 1,
                            [Resource.Grain] = 1,
                            [Resource.Ore] = 0
                        }
                    }
                    : p)
            ]
        });
    }

    private static async Task<Guid> WinAsync(
        SqliteFixture fixture,
        AppDbContext context,
        List<Guid> users,
        TeamAssignment? teams = null)
    {
        var repository = new GameRepository(context, fixture.Clock);
        var games = NewGames(fixture, context, out _);

        var options = new GameOptions { PlayerCount = users.Count, Seed = 3, Teams = teams };
        var id = await games.CreateAsync(options, users);

        var prepared = OneMoveFromWinning((await games.GetAsync(id))!.State, 0, 4, teams);
        await repository.SaveMoveAsync(
            new StoredGame(id, GameStatus.Active, users, prepared),
            new EndTurn(0),
            []);

        var vertex = GameEngine.LegalSettlementVertices(prepared, 0).First();
        var outcome = await games.PlayAsync(id, users[0], new BuildSettlement(0, vertex));

        Assert.Equal(MoveStatus.Applied, outcome.Status);
        Assert.Contains(outcome.Events, e => e is GameWon);

        return id;
    }

    [Fact]
    public async Task Finishing_a_game_counts_for_everyone_and_the_winner()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var users = await fixture.SeedUsersAsync(3);
        var stats = new PlayerStats(context);

        await WinAsync(fixture, context, users);

        var winner = await stats.GetAsync(users[0]);
        var loser = await stats.GetAsync(users[1]);

        Assert.Equal(1, winner!.GamesPlayed);
        Assert.Equal(1, winner.GamesWon);
        Assert.Equal(1, loser!.GamesPlayed);
        Assert.Equal(0, loser.GamesWon);
    }

    /// <summary>در بازی تیمی کل تیم برنده حساب می‌شود، نه فقط کسی که ضربه‌ی آخر را زد.</summary>
    [Fact]
    public async Task A_team_win_counts_for_the_whole_team()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var users = await fixture.SeedUsersAsync(4);
        var stats = new PlayerStats(context);

        await WinAsync(fixture, context, users, new TeamAssignment([0, 1, 0, 1]));

        Assert.Equal(1, (await stats.GetAsync(users[0]))!.GamesWon);
        Assert.Equal(1, (await stats.GetAsync(users[2]))!.GamesWon);
        Assert.Equal(0, (await stats.GetAsync(users[1]))!.GamesWon);
        Assert.Equal(0, (await stats.GetAsync(users[3]))!.GamesWon);

        Assert.All(users, async u => Assert.Equal(1, (await stats.GetAsync(u))!.GamesPlayed));
    }

    [Fact]
    public async Task An_unfinished_game_counts_for_nobody()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var users = await fixture.SeedUsersAsync(3);
        var stats = new PlayerStats(context);
        var games = NewGames(fixture, context, out _);

        var id = await games.CreateAsync(new GameOptions { PlayerCount = 3, Seed = 3 }, users);
        var game = (await games.GetAsync(id))!;
        var vertex = GameEngine.LegalSettlementVertices(game.State, 0).First();

        await games.PlayAsync(id, users[0], new PlaceInitialSettlement(0, vertex));

        Assert.Equal(0, (await stats.GetAsync(users[0]))!.GamesPlayed);
    }

    [Fact]
    public async Task A_bot_finish_also_counts()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var users = await fixture.SeedUsersAsync(2);
        var stats = new PlayerStats(context);
        var repository = new GameRepository(context, fixture.Clock);
        var games = NewGames(fixture, context, out _);

        var id = await games.CreateAsync(new GameOptions { PlayerCount = 2, Seed = 3 }, users);
        var prepared = OneMoveFromWinning((await games.GetAsync(id))!.State, 0, 4);
        await repository.SaveMoveAsync(new StoredGame(id, GameStatus.Active, users, prepared), new EndTurn(0), []);

        fixture.Advance(TimeSpan.FromMinutes(10));
        var outcome = await games.AutoPlayAsync(id, new HashSet<Guid>(), AutoPlayPolicy.Default);

        Assert.NotNull(outcome);
        Assert.Contains(outcome!.Events, e => e is GameWon);
        Assert.Equal(1, (await stats.GetAsync(users[0]))!.GamesWon);
        Assert.Equal(1, (await stats.GetAsync(users[1]))!.GamesPlayed);
    }

    // ── جدول رده‌بندی ────────────────────────────────────────────────────

    [Fact]
    public async Task Someone_who_never_played_is_not_ranked()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        await fixture.SeedUsersAsync(3);
        var stats = new PlayerStats(context);

        Assert.Empty(await stats.LeaderboardAsync(10));
    }

    /// <summary>بردِ بیشتر جلوتر است و در تساوی، کسی که کمتر بازی کرده.</summary>
    [Fact]
    public async Task The_ranking_puts_wins_first_then_efficiency()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var users = await fixture.SeedUsersAsync(3);
        var stats = new PlayerStats(context);

        // بازیکن ۰: ۲ برد از ۵ · بازیکن ۱: ۲ برد از ۳ · بازیکن ۲: ۱ برد از ۱
        await Record(stats, users, [0, 1, 2], [2]);
        await Record(stats, users, [0, 1], [1]);
        await Record(stats, users, [0, 1], [1]);
        await Record(stats, users, [0], [0]);
        await Record(stats, users, [0], [0]);

        var board = await stats.LeaderboardAsync(10);

        Assert.Equal([2, 2, 1], board.Select(p => p.GamesWon));

        // بازیکن ۱ و ۰ هر دو دو برد دارند؛ آن‌که کمتر بازی کرده جلوتر می‌نشیند.
        Assert.Equal(users[1], board[0].UserId);
        Assert.Equal(users[0], board[1].UserId);
        Assert.Equal(users[2], board[2].UserId);
    }

    [Fact]
    public async Task The_win_rate_is_a_percentage_and_null_before_the_first_game()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var users = await fixture.SeedUsersAsync(2);
        var stats = new PlayerStats(context);

        Assert.Null((await stats.GetAsync(users[0]))!.WinRate);

        await Record(stats, users, [0, 1], [0]);
        await Record(stats, users, [0, 1], []);

        Assert.Equal(50, (await stats.GetAsync(users[0]))!.WinRate);
        Assert.Equal(0, (await stats.GetAsync(users[1]))!.WinRate);
    }

    [Fact]
    public async Task The_ranking_respects_its_limit()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var users = await fixture.SeedUsersAsync(5);
        var stats = new PlayerStats(context);

        await Record(stats, users, [0, 1, 2, 3, 4], [0]);

        Assert.Equal(2, (await stats.LeaderboardAsync(2)).Count);
    }

    [Fact]
    public async Task An_unknown_user_has_no_standing()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();

        Assert.Null(await new PlayerStats(context).GetAsync(Guid.NewGuid()));
    }

    private static Task Record(IPlayerStats stats, List<Guid> users, int[] played, int[] won) =>
        stats.RecordFinishAsync([.. played.Select(i => users[i])], [.. won.Select(i => users[i])]);
}
