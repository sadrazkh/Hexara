using Hexara.Application.Games;
using Hexara.Domain.Board;
using Hexara.Domain.Game;
using Hexara.Infrastructure.Persistence;

namespace Hexara.Application.Tests;

/// <summary>
/// بازگشت بعد از قطعی: بازیکن می‌گوید آخرین نسخه‌ای که دیده چه بوده و فقط همان
/// چیزهایی را می‌گیرد که از دست داده — سانسورشده برای صندلی خودش.
/// </summary>
public class CatchUpTests
{
    private static GameAction NextSetupAction(GameState state) => state.Phase switch
    {
        TurnPhase.SetupSettlement => new PlaceInitialSettlement(
            state.CurrentPlayer,
            GameEngine.LegalSettlementVertices(state, state.CurrentPlayer)
                .OrderBy(v => v.ToString(), StringComparer.Ordinal)
                .First()),

        _ => new PlaceInitialRoad(
            state.CurrentPlayer,
            state.LastSetupSettlement!.Value.TouchingEdges()
                .Where(e => state.Board.ContainsEdge(e) && state.RoadAt(e) is null)
                .OrderBy(e => e.ToString(), StringComparer.Ordinal)
                .First())
    };

    private static async Task<(GameService Service, Guid GameId, List<Guid> Users)> StartAsync(
        SqliteFixture fixture,
        AppDbContext context,
        int moves)
    {
        var users = await fixture.SeedUsersAsync(3);
        var service = new GameService(new GameRepository(context, fixture.Clock), fixture.Clock);

        var id = await service.CreateAsync(new GameOptions { PlayerCount = 3, Seed = 4 }, users);

        for (var i = 0; i < moves; i++)
        {
            var game = (await service.GetAsync(id))!;
            var action = NextSetupAction(game.State);
            var outcome = await service.PlayAsync(id, users[action.PlayerIndex], action);

            Assert.Equal(MoveStatus.Applied, outcome.Status);
        }

        return (service, id, users);
    }

    [Fact]
    public async Task Catching_up_from_the_start_replays_everything()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();

        var (service, id, _) = await StartAsync(fixture, context, moves: 4);

        var events = await service.EventsSinceAsync(id, sinceVersion: 0, viewerSeat: 0);

        Assert.Equal(2, events.OfType<SetupSettlementPlaced>().Count());
        Assert.Equal(2, events.OfType<SetupRoadPlaced>().Count());
    }

    [Fact]
    public async Task Catching_up_from_the_middle_skips_what_was_already_seen()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();

        var (service, id, _) = await StartAsync(fixture, context, moves: 4);

        var all = await service.EventsSinceAsync(id, 0, 0);
        var tail = await service.EventsSinceAsync(id, 2, 0);

        Assert.True(tail.Count < all.Count);
        Assert.Equal(all.TakeLast(tail.Count).Select(e => e.GetType()), tail.Select(e => e.GetType()));
    }

    [Fact]
    public async Task A_player_who_is_up_to_date_gets_nothing()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();

        var (service, id, _) = await StartAsync(fixture, context, moves: 3);
        var game = (await service.GetAsync(id))!;

        Assert.Empty(await service.EventsSinceAsync(id, game.State.Version, 0));
    }

    /// <summary>عقب‌ماندگی هم مثل پخش زنده سانسور می‌شود.</summary>
    [Fact]
    public async Task Catching_up_hides_other_peoples_secrets()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();

        var users = await fixture.SeedUsersAsync(3);
        var service = new GameService(new GameRepository(context, fixture.Clock), fixture.Clock);
        var id = await service.CreateAsync(new GameOptions { PlayerCount = 3, Seed = 4 }, users);

        // بازی را دستی به حالتی می‌بریم که بازیکن ۰ بتواند بدزدد.
        var game = (await service.GetAsync(id))!;
        var snapshot = game.State.ToSnapshot();
        var tile = snapshot.Tiles.First(t => t.Terrain != Terrain.Desert);
        var corner = VertexId.Of(new Axial(tile.Q, tile.R), 0);

        var prepared = GameState.Restore(snapshot with
        {
            Phase = TurnPhase.MoveRobber,
            CurrentPlayer = 0,
            TurnNumber = 2,
            Buildings = [new BuildingSnapshot(corner.Hex.Q, corner.Hex.R, corner.Corner, 1, BuildingKind.Settlement)],
            Players =
            [
                snapshot.Players[0],
                snapshot.Players[1] with
                {
                    Resources = new Dictionary<Resource, int>
                    {
                        [Resource.Lumber] = 0,
                        [Resource.Brick] = 0,
                        [Resource.Wool] = 0,
                        [Resource.Grain] = 0,
                        [Resource.Ore] = 2
                    }
                },
                snapshot.Players[2]
            ]
        });

        var repository = new GameRepository(context, fixture.Clock);
        var stored = new StoredGame(game.Id, GameStatus.Active, users, prepared);
        var action = new MoveRobber(0, new Axial(tile.Q, tile.R), 1);
        var result = GameEngine.Apply(prepared, action);

        Assert.True(result.Success);
        Assert.True(await repository.SaveMoveAsync(stored, action, result.Events));

        var thiefSees = await service.EventsSinceAsync(id, 0, viewerSeat: 0);
        var victimSees = await service.EventsSinceAsync(id, 0, viewerSeat: 1);
        var outsiderSees = await service.EventsSinceAsync(id, 0, viewerSeat: 2);

        Assert.Contains(thiefSees, e => e is ResourceStolen);
        Assert.Contains(victimSees, e => e is ResourceStolen);
        Assert.DoesNotContain(outsiderSees, e => e is ResourceStolen);
        Assert.Contains(outsiderSees, e => e is ResourceStolenSecretly);
    }

    /// <summary>شماره‌ی نسخه در لاگ باید با نسخه‌ی وضعیت بعد از همان حرکت یکی باشد.</summary>
    [Fact]
    public async Task Every_move_row_records_the_version_it_produced()
    {
        using var fixture = new SqliteFixture();
        await using var context = fixture.NewContext();

        var (service, id, _) = await StartAsync(fixture, context, moves: 4);

        var history = await service.HistoryAsync(id);
        var game = (await service.GetAsync(id))!;

        Assert.Equal([1, 2, 3, 4], history.Select(h => h.Version));
        Assert.Equal(game.State.Version, history[^1].Version);
    }
}
