using Hexara.Application.Games;
using Hexara.Domain.Board;
using Hexara.Domain.Game;
using Microsoft.EntityFrameworkCore;

namespace Hexara.Application.Tests;

public class GameRepositoryTests
{
    private static GameState NewGame(IReadOnlyList<Guid> playerIds, ulong seed = 9) =>
        GameState.Create(new GameOptions { PlayerCount = playerIds.Count, Seed = seed }, playerIds);

    [Fact]
    public async Task A_created_game_can_be_loaded_back()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(3);

        var repository = fixture.NewRepository(out var writeContext);
        await using (writeContext)
        {
            var id = await repository.CreateAsync(NewGame(users), users, GameStatus.Active);

            var reader = fixture.NewRepository(out var readContext);
            await using (readContext)
            {
                var loaded = await reader.LoadAsync(id);

                Assert.NotNull(loaded);
                Assert.Equal(GameStatus.Active, loaded!.Status);
                Assert.Equal(users, loaded.PlayerIds);
                Assert.Equal(TurnPhase.SetupSettlement, loaded.State.Phase);
            }
        }
    }

    [Fact]
    public async Task Seats_keep_their_order()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(4);

        var repository = fixture.NewRepository(out var context);
        await using (context)
        {
            var id = await repository.CreateAsync(NewGame(users), users, GameStatus.Active);
            var loaded = await repository.LoadAsync(id);

            for (var seat = 0; seat < users.Count; seat++)
            {
                Assert.Equal(users[seat], loaded!.PlayerIds[seat]);
                Assert.Equal(seat, loaded.SeatOf(users[seat]));
            }
        }
    }

    [Fact]
    public async Task An_unknown_game_loads_as_null()
    {
        using var fixture = new SqliteFixture();
        var repository = fixture.NewRepository(out var context);

        await using (context)
        {
            Assert.Null(await repository.LoadAsync(Guid.NewGuid()));
        }
    }

    [Fact]
    public async Task A_saved_move_shows_up_in_the_state_and_the_history()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(3);

        Guid id;
        var writer = fixture.NewRepository(out var writeContext);
        await using (writeContext)
        {
            id = await writer.CreateAsync(NewGame(users), users, GameStatus.Active);
        }

        var mover = fixture.NewRepository(out var moveContext);
        await using (moveContext)
        {
            var game = await mover.LoadAsync(id);
            var vertex = GameEngine.LegalSettlementVertices(game!.State, 0).First();
            var action = new PlaceInitialSettlement(0, vertex);
            var result = GameEngine.Apply(game.State, action);

            Assert.True(result.Success);
            Assert.True(await mover.SaveMoveAsync(game, action, result.Events));
        }

        var reader = fixture.NewRepository(out var readContext);
        await using (readContext)
        {
            var reloaded = await reader.LoadAsync(id);
            Assert.Equal(TurnPhase.SetupRoad, reloaded!.State.Phase);
            Assert.Single(reloaded.State.Buildings);

            var history = await reader.HistoryAsync(id);
            var entry = Assert.Single(history);
            Assert.Equal(1, entry.Sequence);
            Assert.Equal(0, entry.PlayerIndex);
            Assert.IsType<PlaceInitialSettlement>(entry.Action);
            Assert.Contains(entry.Events, e => e is SetupSettlementPlaced);
        }
    }

    [Fact]
    public async Task The_history_keeps_the_order_of_moves()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(2);

        var repository = fixture.NewRepository(out var context);
        await using (context)
        {
            var id = await repository.CreateAsync(NewGame(users), users, GameStatus.Active);

            for (var i = 0; i < 4; i++)
            {
                var game = await repository.LoadAsync(id);
                var action = NextSetupAction(game!.State);
                var result = GameEngine.Apply(game.State, action);

                Assert.True(result.Success, $"{action} رد شد: {result.Error}");
                Assert.True(await repository.SaveMoveAsync(game, action, result.Events));
            }

            var history = await repository.HistoryAsync(id);

            Assert.Equal([1, 2, 3, 4], history.Select(h => h.Sequence));
        }
    }

    /// <summary>
    /// دو حرکت هم‌زمان روی یک بازی: دومی باید رد شود، وگرنه یکی از دو حرکت بی‌صدا گم می‌شود.
    /// </summary>
    [Fact]
    public async Task A_concurrent_move_is_refused()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(3);

        Guid id;
        var creator = fixture.NewRepository(out var createContext);
        await using (createContext)
        {
            id = await creator.CreateAsync(NewGame(users), users, GameStatus.Active);
        }

        var first = fixture.NewRepository(out var firstContext);
        var second = fixture.NewRepository(out var secondContext);

        await using (firstContext)
        await using (secondContext)
        {
            // هر دو درخواست همان نسخه را می‌خوانند.
            var a = await first.LoadAsync(id);
            var b = await second.LoadAsync(id);

            var actionA = NextSetupAction(a!.State);
            var resultA = GameEngine.Apply(a.State, actionA);
            Assert.True(await first.SaveMoveAsync(a, actionA, resultA.Events));

            var actionB = NextSetupAction(b!.State);
            var resultB = GameEngine.Apply(b.State, actionB);
            Assert.False(await second.SaveMoveAsync(b, actionB, resultB.Events));
        }

        var reader = fixture.NewRepository(out var readContext);
        await using (readContext)
        {
            // فقط یک حرکت ثبت شده است.
            Assert.Single(await reader.HistoryAsync(id));
        }
    }

    [Fact]
    public async Task Games_are_listed_for_each_of_their_players()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(3);
        var outsider = (await fixture.SeedUsersAsync(1))[0];

        var repository = fixture.NewRepository(out var context);
        await using (context)
        {
            var id = await repository.CreateAsync(NewGame(users), users, GameStatus.Active);

            foreach (var user in users)
            {
                var listed = await repository.ListForPlayerAsync(user);
                Assert.Equal(id, Assert.Single(listed).Id);
                Assert.Equal(3, listed[0].PlayerCount);
            }

            Assert.Empty(await repository.ListForPlayerAsync(outsider));
        }
    }

    [Fact]
    public async Task Finishing_a_game_records_the_winner()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(2);

        var repository = fixture.NewRepository(out var context);
        await using (context)
        {
            var id = await repository.CreateAsync(NewGame(users), users, GameStatus.Active);
            var game = await repository.LoadAsync(id);

            // بازی را دستی به پایان می‌بریم تا ذخیره‌ی برنده بررسی شود.
            var vertex = GameEngine.LegalSettlementVertices(game!.State, 0).First();
            var action = new PlaceInitialSettlement(0, vertex);
            var result = GameEngine.Apply(game.State, action);
            game.Status = GameStatus.Finished;

            Assert.True(await repository.SaveMoveAsync(game, action, result.Events));

            var summary = Assert.Single(await repository.ListForPlayerAsync(users[0]));
            Assert.Equal(GameStatus.Finished, summary.Status);
        }
    }

    [Fact]
    public async Task Deleting_a_game_takes_its_seats_and_moves_with_it()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(2);

        var repository = fixture.NewRepository(out var context);
        await using (context)
        {
            var id = await repository.CreateAsync(NewGame(users), users, GameStatus.Active);
            var game = await repository.LoadAsync(id);
            var action = NextSetupAction(game!.State);
            await repository.SaveMoveAsync(game, action, GameEngine.Apply(game.State, action).Events);

            context.Games.Remove(await context.Games.SingleAsync(g => g.Id == id));
            await context.SaveChangesAsync();

            Assert.Empty(context.GamePlayers);
            Assert.Empty(context.GameMoves);
        }
    }

    private static GameAction NextSetupAction(GameState state) => state.Phase switch
    {
        TurnPhase.SetupSettlement => new PlaceInitialSettlement(
            state.CurrentPlayer,
            GameEngine.LegalSettlementVertices(state, state.CurrentPlayer)
                .OrderBy(v => v.ToString(), StringComparer.Ordinal)
                .First()),

        TurnPhase.SetupRoad => new PlaceInitialRoad(
            state.CurrentPlayer,
            state.LastSetupSettlement!.Value.TouchingEdges()
                .Where(e => state.Board.ContainsEdge(e) && state.RoadAt(e) is null)
                .OrderBy(e => e.ToString(), StringComparer.Ordinal)
                .First()),

        _ => new RollDice(state.CurrentPlayer)
    };
}
