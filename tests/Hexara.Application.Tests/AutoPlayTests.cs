using Hexara.Application.Games;
using Hexara.Domain.Board;
using Hexara.Domain.Game;
using Hexara.Infrastructure.Persistence;

namespace Hexara.Application.Tests;

/// <summary>
/// پوشش خودکار: چه وقت بات جای یک بازیکن را می‌گیرد و چه وقت نه.
///
/// روی مخزن واقعی اجرا می‌شود چون کل تصمیم به ستون ‎UpdatedAt‎ گره خورده و
/// رفت‌وبرگشتش از دیتابیس بخشی از همان چیزی است که باید درست باشد.
/// </summary>
public class AutoPlayTests
{
    private static readonly AutoPlayPolicy Policy = new(TimeSpan.FromSeconds(25), TimeSpan.FromMinutes(3));

    private static async Task<(GameService Games, Guid Id, List<Guid> Users)> NewGameAsync(
        SqliteFixture fixture,
        AppDbContext context,
        int players = 3)
    {
        var users = await fixture.SeedUsersAsync(players);
        var games = new GameService(new GameRepository(context, fixture.Clock), fixture.Clock);
        var id = await games.CreateAsync(new GameOptions { PlayerCount = players, Seed = 9 }, users);

        return (games, id, users);
    }

    private static IReadOnlySet<Guid> Online(params Guid[] users) => users.ToHashSet();

    [Fact]
    public async Task A_present_player_inside_the_deadline_is_left_alone()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var (games, id, users) = await NewGameAsync(fixture, context);

        fixture.Advance(TimeSpan.FromSeconds(40));

        Assert.Null(await games.AutoPlayAsync(id, Online([.. users]), Policy));
    }

    [Fact]
    public async Task An_absent_player_past_the_grace_is_covered()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var (games, id, users) = await NewGameAsync(fixture, context);

        fixture.Advance(TimeSpan.FromSeconds(30));

        // صندلی ۰ نوبتش است و آنلاین نیست.
        var outcome = await games.AutoPlayAsync(id, Online(users[1], users[2]), Policy);

        Assert.NotNull(outcome);
        Assert.Equal(MoveStatus.Applied, outcome!.Status);
        Assert.Contains(outcome.Events, e => e is SetupSettlementPlaced);

        var game = await games.GetAsync(id);
        Assert.Equal(TurnPhase.SetupRoad, game!.State.Phase);
    }

    [Fact]
    public async Task An_absent_player_inside_the_grace_is_left_alone()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var (games, id, users) = await NewGameAsync(fixture, context);

        fixture.Advance(TimeSpan.FromSeconds(10));

        Assert.Null(await games.AutoPlayAsync(id, Online(users[1], users[2]), Policy));
    }

    /// <summary>حاضر بودن معافیت همیشگی نیست — مهلت نوبت هم وجود دارد.</summary>
    [Fact]
    public async Task A_present_player_past_the_turn_deadline_is_covered()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var (games, id, users) = await NewGameAsync(fixture, context);

        fixture.Advance(TimeSpan.FromMinutes(4));

        Assert.NotNull(await games.AutoPlayAsync(id, Online([.. users]), Policy));
    }

    [Fact]
    public async Task Only_the_seat_that_owes_a_move_is_covered()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var (games, id, users) = await NewGameAsync(fixture, context);

        fixture.Advance(TimeSpan.FromSeconds(30));

        // نوبت صندلی ۰ است؛ غیبت صندلی ۲ نباید کاری راه بیندازد.
        Assert.Null(await games.AutoPlayAsync(id, Online(users[0], users[1]), Policy));
    }

    /// <summary>در مرحله‌ی دور ریختن نوبتِ کسی نیست ولی بدهکارها باید پوشش داده شوند.</summary>
    [Fact]
    public async Task A_player_who_owes_a_discard_is_covered_out_of_turn()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var users = await fixture.SeedUsersAsync(3);

        var repository = new GameRepository(context, fixture.Clock);
        var games = new GameService(repository, fixture.Clock);
        var id = await games.CreateAsync(new GameOptions { PlayerCount = 3, Seed = 9 }, users);

        var game = (await games.GetAsync(id))!;
        var snapshot = game.State.ToSnapshot();

        var prepared = GameState.Restore(snapshot with
        {
            Phase = TurnPhase.Discard,
            CurrentPlayer = 0,
            TurnNumber = 4,
            PendingDiscards = new Dictionary<int, int> { [2] = 4 },
            Players =
            [
                snapshot.Players[0],
                snapshot.Players[1],
                snapshot.Players[2] with
                {
                    Resources = new Dictionary<Resource, int>
                    {
                        [Resource.Lumber] = 2,
                        [Resource.Brick] = 2,
                        [Resource.Wool] = 2,
                        [Resource.Grain] = 2,
                        [Resource.Ore] = 1
                    }
                }
            ]
        });

        // فقط برای نشاندن این وضعیت در دیتابیس؛ خودِ حرکت اجرا نمی‌شود و صرفاً یک سطر لاگ است.
        var stored = new StoredGame(id, GameStatus.Active, users, prepared);
        await repository.SaveMoveAsync(stored, new EndTurn(0), []);

        fixture.Advance(TimeSpan.FromSeconds(30));

        // فقط صندلی ۲ بدهکار است و همان هم غایب.
        var outcome = await games.AutoPlayAsync(id, Online(users[0], users[1]), Policy);

        Assert.NotNull(outcome);
        var discarded = Assert.Single(outcome!.Events.OfType<CardsDiscarded>());
        Assert.Equal(2, discarded.PlayerIndex);
        Assert.Equal(4, discarded.Cards.Values.Sum());
    }

    [Fact]
    public async Task A_finished_game_is_never_touched()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var users = await fixture.SeedUsersAsync(2);

        var repository = new GameRepository(context, fixture.Clock);
        var games = new GameService(repository, fixture.Clock);
        var id = await games.CreateAsync(new GameOptions { PlayerCount = 2, Seed = 9 }, users);

        var game = (await games.GetAsync(id))!;
        var finished = new StoredGame(id, GameStatus.Finished, users, game.State);
        await repository.SaveMoveAsync(finished, new EndTurn(0), []);

        fixture.Advance(TimeSpan.FromHours(1));

        Assert.Null(await games.AutoPlayAsync(id, Online(), Policy));
    }

    [Fact]
    public async Task An_unknown_game_is_reported_as_nothing_to_do()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var (games, _, _) = await NewGameAsync(fixture, context);

        Assert.Null(await games.AutoPlayAsync(Guid.NewGuid(), Online(), Policy));
    }

    /// <summary>حرکت بات مثل هر حرکت دیگری در تاریخچه ثبت می‌شود.</summary>
    [Fact]
    public async Task A_bot_move_lands_in_the_history()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var (games, id, users) = await NewGameAsync(fixture, context);

        fixture.Advance(TimeSpan.FromSeconds(30));
        await games.AutoPlayAsync(id, Online(users[1], users[2]), Policy);

        var entry = Assert.Single(await games.HistoryAsync(id));
        Assert.Equal(0, entry.PlayerIndex);
        Assert.IsType<PlaceInitialSettlement>(entry.Action);
    }

    /// <summary>پوشش پشت سر هم باید بازی را جلو ببرد، نه اینکه در جا بزند.</summary>
    [Fact]
    public async Task Repeated_cover_walks_the_game_forward()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var (games, id, _) = await NewGameAsync(fixture, context);

        for (var i = 0; i < 12; i++)
        {
            fixture.Advance(TimeSpan.FromSeconds(30));
            Assert.NotNull(await games.AutoPlayAsync(id, Online(), Policy));
        }

        var game = (await games.GetAsync(id))!;

        // ۳ بازیکن × ۲ دور × (آبادی + جاده) = ۱۲ حرکت، یعنی چیدمان اولیه تمام شده.
        Assert.False(game.State.IsSetup);
        Assert.Equal(12, game.State.Version);
    }

    [Fact]
    public async Task Idle_games_are_the_ones_that_get_listed()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var (games, id, _) = await NewGameAsync(fixture, context);

        Assert.Empty(await games.ListIdleAsync(TimeSpan.FromSeconds(25), 10));

        fixture.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(id, Assert.Single(await games.ListIdleAsync(TimeSpan.FromSeconds(25), 10)));
    }

    /// <summary>
    /// مهم‌ترین ترمز: هر حرکت بات ساعتِ بیکاری را از نو می‌اندازد، پس سرکشی بعدی
    /// بلافاصله دوباره بازی نمی‌کند. بدون این، بات کل بازی را در یک دور می‌بلعید.
    /// </summary>
    [Fact]
    public async Task A_cover_restarts_the_idle_clock()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var (games, id, _) = await NewGameAsync(fixture, context);

        fixture.Advance(TimeSpan.FromSeconds(30));
        Assert.NotNull(await games.AutoPlayAsync(id, Online(), Policy));

        // بدون گذشتِ زمان، سرکشی بعدی باید دست نگه دارد.
        Assert.Null(await games.AutoPlayAsync(id, Online(), Policy));
        Assert.Empty(await games.ListIdleAsync(TimeSpan.FromSeconds(25), 10));

        Assert.Equal(1, (await games.GetAsync(id))!.State.Version);
    }

    /// <summary>
    /// بات هر بار جای معقولی می‌سازد، نه هر جای مجازی: آبادی اولیه باید روی یکی از
    /// پرتولیدترین گوشه‌های برد بنشیند.
    /// </summary>
    [Fact]
    public async Task The_bot_picks_a_high_yield_opening()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();
        var (games, id, _) = await NewGameAsync(fixture, context);

        var before = (await games.GetAsync(id))!.State;
        var best = GameEngine.LegalSettlementVertices(before, 0).Max(v => Pips(before, v));

        fixture.Advance(TimeSpan.FromSeconds(30));
        await games.AutoPlayAsync(id, Online(), Policy);

        var placed = Assert.Single((await games.GetAsync(id))!.State.Buildings);
        Assert.Equal(best, Pips(before, placed.Key));
    }

    private static int Pips(GameState state, VertexId vertex) =>
        vertex.TouchingHexes()
            .Select(state.Board.TileAt)
            .Where(t => t?.Number is not null && t.Resource is not null)
            .Sum(t => 6 - Math.Abs(7 - t!.Number!.Value));
}
