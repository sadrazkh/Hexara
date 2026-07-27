using Hexara.Application.Common.Interfaces;
using Hexara.Application.Games;
using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Application.Tests;

public class GameServiceTests
{
    private static readonly GameOptions Options = new() { PlayerCount = 3, Seed = 12 };

    private static (GameService Service, FakeRepository Repository, List<Guid> Users) NewService()
    {
        var repository = new FakeRepository();
        var users = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        return (new GameService(repository), repository, users);
    }

    private static GameAction NextSetupMove(GameState state) => new PlaceInitialSettlement(
        state.CurrentPlayer,
        GameEngine.LegalSettlementVertices(state, state.CurrentPlayer).First());

    [Fact]
    public async Task Creating_a_game_stores_it()
    {
        var (service, repository, users) = NewService();

        var id = await service.CreateAsync(Options, users);
        var game = await service.GetAsync(id);

        Assert.NotNull(game);
        Assert.Equal(GameStatus.Active, game!.Status);
        Assert.Equal(users, game.PlayerIds);
        Assert.Single(repository.Games);
    }

    [Fact]
    public async Task A_move_from_the_player_on_turn_is_applied()
    {
        var (service, repository, users) = NewService();
        var id = await service.CreateAsync(Options, users);
        var game = await service.GetAsync(id);

        var outcome = await service.PlayAsync(id, users[0], NextSetupMove(game!.State));

        Assert.Equal(MoveStatus.Applied, outcome.Status);
        Assert.True(outcome.Success);
        Assert.Contains(outcome.Events, e => e is SetupSettlementPlaced);
        Assert.Single(repository.Moves);
    }

    [Fact]
    public async Task A_move_on_an_unknown_game_is_reported_as_missing()
    {
        var (service, _, users) = NewService();

        var outcome = await service.PlayAsync(Guid.NewGuid(), users[0], new RollDice(0));

        Assert.Equal(MoveStatus.GameNotFound, outcome.Status);
    }

    [Fact]
    public async Task Someone_who_is_not_a_player_cannot_move()
    {
        var (service, repository, users) = NewService();
        var id = await service.CreateAsync(Options, users);
        var game = await service.GetAsync(id);

        var outcome = await service.PlayAsync(id, Guid.NewGuid(), NextSetupMove(game!.State));

        Assert.Equal(MoveStatus.NotYourSeat, outcome.Status);
        Assert.Empty(repository.Moves);
    }

    /// <summary>
    /// کلاینت دستکاری‌شده نباید بتواند به جای بازیکن دیگری بازی کند، حتی اگر نوبتِ
    /// آن صندلی باشد.
    /// </summary>
    [Fact]
    public async Task A_player_cannot_claim_another_seat()
    {
        var (service, repository, users) = NewService();
        var id = await service.CreateAsync(Options, users);
        var game = await service.GetAsync(id);

        // نوبت صندلی ۰ است و کاربر ۱ می‌خواهد به جای او حرکت کند.
        var outcome = await service.PlayAsync(id, users[1], NextSetupMove(game!.State));

        Assert.Equal(MoveStatus.NotYourSeat, outcome.Status);
        Assert.Empty(repository.Moves);
    }

    [Fact]
    public async Task A_move_out_of_turn_is_rejected_by_the_rules()
    {
        var (service, repository, users) = NewService();
        var id = await service.CreateAsync(Options, users);
        var game = await service.GetAsync(id);

        var vertex = GameEngine.LegalSettlementVertices(game!.State, 1).First();
        var outcome = await service.PlayAsync(id, users[1], new PlaceInitialSettlement(1, vertex));

        Assert.Equal(MoveStatus.Rejected, outcome.Status);
        Assert.Equal(GameError.NotYourTurn, outcome.Error);
        Assert.Empty(repository.Moves);
    }

    /// <summary>حرکت ردشده نباید ذخیره شود و نباید وضعیت ذخیره‌شده را جلو ببرد.</summary>
    [Fact]
    public async Task A_rejected_move_does_not_touch_the_stored_state()
    {
        var (service, repository, users) = NewService();
        var id = await service.CreateAsync(Options, users);

        var before = repository.Games[id].State.Version;
        await service.PlayAsync(id, users[0], new RollDice(0));

        Assert.Equal(before, repository.Games[id].State.Version);
        Assert.Empty(repository.Moves);
    }

    [Fact]
    public async Task A_conflict_is_reported_back()
    {
        var (service, repository, users) = NewService();
        var id = await service.CreateAsync(Options, users);
        var game = await service.GetAsync(id);

        repository.FailNextSave = true;
        var outcome = await service.PlayAsync(id, users[0], NextSetupMove(game!.State));

        Assert.Equal(MoveStatus.Conflict, outcome.Status);
    }

    /// <summary>
    /// وضعیت دلخواه از راه عکس وضعیت ساخته می‌شود — همان API عمومی که ذخیره‌سازی
    /// استفاده می‌کند، بدون نیاز به دسترسی به اجزای internal دامنه.
    /// </summary>
    [Fact]
    public async Task Finishing_the_game_marks_it_finished()
    {
        var (service, repository, users) = NewService();
        var players = new List<Guid> { users[0], users[1] };

        var options = Options with { PlayerCount = 2, VictoryPoints = 3 };
        var snapshot = GameState.Create(options, players).ToSnapshot();
        var road = EdgeId.Of(new Axial(0, 0), 0);

        var state = GameState.Restore(snapshot with
        {
            Phase = TurnPhase.Main,
            CurrentPlayer = 0,
            TurnNumber = 5,
            Roads = [new RoadSnapshot(road.Hex.Q, road.Hex.R, road.Side, 0)],
            Players =
            [
                snapshot.Players[0] with
                {
                    BuildingPoints = 2,
                    Resources = new Dictionary<Resource, int>
                    {
                        [Resource.Lumber] = 1,
                        [Resource.Brick] = 1,
                        [Resource.Wool] = 1,
                        [Resource.Grain] = 1,
                        [Resource.Ore] = 0
                    }
                },
                snapshot.Players[1]
            ]
        });

        var id = Guid.NewGuid();
        repository.Games[id] = new StoredGame(id, GameStatus.Active, players, state);

        var vertex = GameEngine.LegalSettlementVertices(state, 0).First();
        var outcome = await service.PlayAsync(id, users[0], new BuildSettlement(0, vertex));

        Assert.Equal(MoveStatus.Applied, outcome.Status);
        Assert.Contains(outcome.Events, e => e is GameWon);
        Assert.Equal(GameStatus.Finished, repository.Games[id].Status);
        Assert.Equal(users[0], repository.Games[id].WinnerId);
    }

    /// <summary>مخزن در حافظه — تست‌های سرویس نباید به دیتابیس وابسته باشند.</summary>
    private sealed class FakeRepository : IGameRepository
    {
        public Dictionary<Guid, StoredGame> Games { get; } = [];

        public List<GameMoveLogEntry> Moves { get; } = [];

        public bool FailNextSave { get; set; }

        public Task<Guid> CreateAsync(
            GameState state,
            IReadOnlyList<Guid> playerIds,
            GameStatus status,
            CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            Games[id] = new StoredGame(id, status, playerIds, state);
            return Task.FromResult(id);
        }

        public Task<StoredGame?> LoadAsync(Guid gameId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Games.GetValueOrDefault(gameId));

        public Task<bool> SaveMoveAsync(
            StoredGame game,
            GameAction action,
            IReadOnlyList<GameEvent> events,
            CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                return Task.FromResult(false);
            }

            Moves.Add(new GameMoveLogEntry(
                Moves.Count + 1,
                game.State.Version,
                action.PlayerIndex,
                action,
                events,
                DateTimeOffset.UnixEpoch));

            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<GameSummary>> ListForPlayerAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<GameSummary> list =
            [
                .. Games.Values
                    .Where(g => g.PlayerIds.Contains(userId))
                    .Select(g => new GameSummary(
                        g.Id,
                        g.Status,
                        g.PlayerIds.Count,
                        g.State.TurnNumber,
                        g.WinnerId,
                        DateTimeOffset.UnixEpoch,
                        DateTimeOffset.UnixEpoch))
            ];

            return Task.FromResult(list);
        }

        public Task<IReadOnlyList<GameMoveLogEntry>> HistoryAsync(
            Guid gameId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameMoveLogEntry>>([.. Moves]);

        public Task<IReadOnlyList<GameMoveLogEntry>> HistorySinceAsync(
            Guid gameId,
            long sinceVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameMoveLogEntry>>([.. Moves.Where(m => m.Version > sinceVersion)]);
    }
}
