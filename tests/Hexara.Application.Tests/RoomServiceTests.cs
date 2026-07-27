using Hexara.Application.Games;
using Hexara.Application.Rooms;
using Hexara.Infrastructure.Persistence;

namespace Hexara.Application.Tests;

/// <summary>
/// تست‌های اتاق روی مخزن واقعی (SQLite) اجرا می‌شوند، نه یک بدل در حافظه: بیشتر
/// قواعد اینجا درباره‌ی یکتایی صندلی و کد است و آن‌ها را فقط دیتابیس واقعی نشان می‌دهد.
/// </summary>
public class RoomServiceTests
{
    private static RoomService NewService(SqliteFixture fixture, out AppDbContext context)
    {
        context = fixture.NewContext();
        var rooms = new RoomRepository(context, fixture.Clock);
        var games = new GameService(new GameRepository(context, fixture.Clock), fixture.Clock);
        return new RoomService(rooms, games);
    }

    [Fact]
    public async Task Creating_a_room_seats_the_host_first()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(1);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var result = await service.CreateAsync(users[0], new RoomSettings());

            Assert.True(result.Success);
            var room = result.Room!;
            Assert.Equal(RoomStatus.Open, room.Status);
            Assert.Equal(users[0], room.HostId);
            Assert.Equal(0, Assert.Single(room.Members).Seat);
            Assert.True(RoomCode.IsWellFormed(room.Code));
        }
    }

    [Fact]
    public async Task Invalid_settings_are_refused()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(1);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var result = await service.CreateAsync(users[0], new RoomSettings { MaxPlayers = 9 });

            Assert.Equal(RoomError.InvalidSettings, result.Error);
        }
    }

    [Fact]
    public async Task Joining_takes_the_next_free_seat()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(3);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings())).Room!;

            await service.JoinAsync(room.Code, users[1]);
            var result = await service.JoinAsync(room.Code, users[2]);

            Assert.True(result.Success);
            Assert.Equal([0, 1, 2], result.Room!.Members.Select(m => m.Seat));
            Assert.Equal([users[0], users[1], users[2]], result.Room.Members.Select(m => m.UserId));
        }
    }

    [Fact]
    public async Task The_room_code_is_not_case_sensitive()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(2);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings())).Room!;

            var result = await service.JoinAsync(room.Code.ToLowerInvariant(), users[1]);

            Assert.True(result.Success);
            Assert.Equal(2, result.Room!.Members.Count);
        }
    }

    /// <summary>پیوستن دوباره باید بی‌سروصدا همان اتاق را برگرداند، نه خطا.</summary>
    [Fact]
    public async Task Joining_twice_is_harmless()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(2);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings())).Room!;
            await service.JoinAsync(room.Code, users[1]);

            var again = await service.JoinAsync(room.Code, users[1]);

            Assert.True(again.Success);
            Assert.Equal(2, again.Room!.Members.Count);
        }
    }

    [Fact]
    public async Task A_full_room_refuses_new_players()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(3);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings { MaxPlayers = 2 })).Room!;
            await service.JoinAsync(room.Code, users[1]);

            var result = await service.JoinAsync(room.Code, users[2]);

            Assert.Equal(RoomError.RoomFull, result.Error);
        }
    }

    [Fact]
    public async Task An_unknown_code_is_reported()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(1);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            Assert.Equal(RoomError.RoomNotFound, (await service.JoinAsync("ZZZZZZ", users[0])).Error);
        }
    }

    [Fact]
    public async Task Leaving_frees_the_seat_for_someone_else()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(3);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings { MaxPlayers = 2 })).Room!;
            await service.JoinAsync(room.Code, users[1]);
            await service.LeaveAsync(room.Id, users[1]);

            var result = await service.JoinAsync(room.Code, users[2]);

            Assert.True(result.Success);
            Assert.Equal([0, 1], result.Room!.Members.Select(m => m.Seat));
        }
    }

    /// <summary>اگر میزبان برود، میزبانی باید به کسی که مانده برسد.</summary>
    [Fact]
    public async Task The_host_role_moves_on_when_the_host_leaves()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(2);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings())).Room!;
            await service.JoinAsync(room.Code, users[1]);

            var result = await service.LeaveAsync(room.Id, users[0]);

            Assert.True(result.Success);
            Assert.Equal(users[1], result.Room!.HostId);
            Assert.Equal(RoomStatus.Open, result.Room.Status);
        }
    }

    [Fact]
    public async Task The_last_one_out_closes_the_room()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(1);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings())).Room!;

            var result = await service.LeaveAsync(room.Id, users[0]);

            Assert.Equal(RoomStatus.Closed, result.Room!.Status);
            Assert.Empty(await service.ListOpenAsync());
        }
    }

    [Fact]
    public async Task Only_the_host_changes_the_settings()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(2);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings())).Room!;
            await service.JoinAsync(room.Code, users[1]);

            var byGuest = await service.UpdateSettingsAsync(room.Id, users[1], new RoomSettings { VictoryPoints = 5 });
            Assert.Equal(RoomError.NotHost, byGuest.Error);

            var byHost = await service.UpdateSettingsAsync(room.Id, users[0], new RoomSettings { VictoryPoints = 5 });
            Assert.True(byHost.Success);
            Assert.Equal(5, byHost.Room!.Settings.VictoryPoints);
        }
    }

    /// <summary>سقف صندلی را نمی‌توان زیر تعداد کسانی که نشسته‌اند آورد.</summary>
    [Fact]
    public async Task Max_players_cannot_drop_below_the_people_already_seated()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(3);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings { MaxPlayers = 4 })).Room!;
            await service.JoinAsync(room.Code, users[1]);
            await service.JoinAsync(room.Code, users[2]);

            var result = await service.UpdateSettingsAsync(room.Id, users[0], new RoomSettings { MaxPlayers = 2 });

            Assert.Equal(RoomError.InvalidSettings, result.Error);
        }
    }

    // ── شروع بازی ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_lone_host_cannot_start()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(1);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings())).Room!;

            Assert.Equal(RoomError.NotEnoughPlayers, (await service.StartAsync(room.Id, users[0])).Error);
        }
    }

    [Fact]
    public async Task Only_the_host_starts_the_game()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(2);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings())).Room!;
            await service.JoinAsync(room.Code, users[1]);

            Assert.Equal(RoomError.NotHost, (await service.StartAsync(room.Id, users[1])).Error);
        }
    }

    /// <summary>ترتیب صندلی‌های اتاق باید دقیقاً ترتیب نوبت بازی شود.</summary>
    [Fact]
    public async Task Starting_creates_a_game_with_the_seats_in_order()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(3);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings { VictoryPoints = 8 })).Room!;
            await service.JoinAsync(room.Code, users[1]);
            await service.JoinAsync(room.Code, users[2]);

            var result = await service.StartAsync(room.Id, users[0]);

            Assert.True(result.Success);
            Assert.NotNull(result.GameId);
            Assert.Equal(RoomStatus.Started, result.Room!.Status);
            Assert.Equal(result.GameId, result.Room.GameId);

            var games = new GameService(new GameRepository(context, fixture.Clock), fixture.Clock);
            var game = await games.GetAsync(result.GameId!.Value);

            Assert.NotNull(game);
            Assert.Equal(users, game!.PlayerIds);
            Assert.Equal(3, game.State.Players.Count);
            Assert.Equal(8, game.State.Options.VictoryPoints);
        }
    }

    [Fact]
    public async Task A_started_room_cannot_be_started_again_or_joined()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(3);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings())).Room!;
            await service.JoinAsync(room.Code, users[1]);
            await service.StartAsync(room.Id, users[0]);

            Assert.Equal(RoomError.RoomAlreadyStarted, (await service.StartAsync(room.Id, users[0])).Error);
            Assert.Equal(RoomError.RoomAlreadyStarted, (await service.JoinAsync(room.Code, users[2])).Error);
            Assert.Equal(RoomError.RoomAlreadyStarted, (await service.LeaveAsync(room.Id, users[1])).Error);
        }
    }

    [Fact]
    public async Task A_started_room_leaves_the_open_list_but_stays_reachable_by_code()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(2);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings())).Room!;
            await service.JoinAsync(room.Code, users[1]);
            await service.StartAsync(room.Id, users[0]);

            Assert.Empty(await service.ListOpenAsync());
            Assert.NotNull(await service.FindAsync(room.Code));
            Assert.Single(await service.ListForUserAsync(users[1]));
        }
    }

    // ── بازی تیمی ────────────────────────────────────────────────────────

    /// <summary>تقسیم یک‌درمیان است تا هم‌تیمی‌ها پشت سر هم نوبت نگیرند.</summary>
    [Fact]
    public async Task A_team_room_alternates_the_seats()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(4);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings { Teams = true })).Room!;
            foreach (var user in users.Skip(1))
            {
                await service.JoinAsync(room.Code, user);
            }

            var result = await service.StartAsync(room.Id, users[0]);

            var games = new GameService(new GameRepository(context, fixture.Clock), fixture.Clock);
            var game = await games.GetAsync(result.GameId!.Value);

            Assert.Equal([0, 1, 0, 1], game!.State.Options.Teams!.BySeat);
        }
    }

    /// <summary>با دو یا سه نفر تیم‌بندی معنا ندارد و نادیده گرفته می‌شود.</summary>
    [Fact]
    public async Task Teams_are_ignored_below_four_players()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(3);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings { Teams = true })).Room!;
            await service.JoinAsync(room.Code, users[1]);
            await service.JoinAsync(room.Code, users[2]);

            var result = await service.StartAsync(room.Id, users[0]);

            var games = new GameService(new GameRepository(context, fixture.Clock), fixture.Clock);
            var game = await games.GetAsync(result.GameId!.Value);

            Assert.Null(game!.State.Options.Teams);
        }
    }

    [Fact]
    public async Task The_team_setting_survives_a_reload()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(1);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var room = (await service.CreateAsync(users[0], new RoomSettings { Teams = true })).Room!;

            Assert.True((await service.FindAsync(room.Code))!.Settings.Teams);
        }
    }

    // ── برد سفارشی ───────────────────────────────────────────────────────

    /// <summary>برد سفارشی باید عیناً همانی باشد که بازی با آن شروع می‌شود.</summary>
    [Fact]
    public async Task A_custom_board_reaches_the_game()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(2);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            // یک برد دست‌ساز که با هیچ seedی تصادفاً درنمی‌آید.
            var draft = BoardEditor.Random(2, 8);
            var edited = draft with
            {
                Tiles = [.. draft.Tiles.Select(t =>
                    t.Terrain == Domain.Board.Terrain.Desert
                        ? t
                        : t with { Terrain = Domain.Board.Terrain.Mountains, Number = 11 })]
            };

            Assert.True(BoardEditor.TryWrite(edited, out var code, out _));

            var room = (await service.CreateAsync(users[0], new RoomSettings { BoardCode = code })).Room!;
            await service.JoinAsync(room.Code, users[1]);

            var result = await service.StartAsync(room.Id, users[0]);
            Assert.True(result.Success);

            var games = new GameService(new GameRepository(context, fixture.Clock), fixture.Clock);
            var game = await games.GetAsync(result.GameId!.Value);

            var land = game!.State.Board.Tiles.Where(t => t.Terrain != Domain.Board.Terrain.Desert).ToList();
            Assert.Equal(18, land.Count);
            Assert.All(land, t => Assert.Equal(11, t.Number));
        }
    }

    [Fact]
    public async Task A_custom_board_sets_the_radius_from_the_code()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(2);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            Assert.True(BoardEditor.TryWrite(BoardEditor.Random(3, 5), out var code, out _));

            // شعاع اتاق عمداً با کد نمی‌خواند؛ کد باید برنده شود.
            var settings = new RoomSettings { BoardRadius = 2, BoardCode = code };
            var room = (await service.CreateAsync(users[0], settings)).Room!;
            await service.JoinAsync(room.Code, users[1]);

            var result = await service.StartAsync(room.Id, users[0]);

            var games = new GameService(new GameRepository(context, fixture.Clock), fixture.Clock);
            var game = await games.GetAsync(result.GameId!.Value);

            Assert.Equal(37, game!.State.Board.Tiles.Count);
            Assert.Equal(3, game.State.Options.BoardRadius);
        }
    }

    [Fact]
    public async Task A_broken_board_code_is_refused_at_creation()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(1);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            var result = await service.CreateAsync(users[0], new RoomSettings { BoardCode = "nope" });

            Assert.Equal(RoomError.InvalidSettings, result.Error);
        }
    }

    [Fact]
    public async Task The_board_code_survives_a_reload()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(1);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            Assert.True(BoardEditor.TryWrite(BoardEditor.Random(2, 77), out var code, out _));

            var room = (await service.CreateAsync(users[0], new RoomSettings { BoardCode = code })).Room!;
            var reloaded = await service.FindAsync(room.Code);

            Assert.True(reloaded!.Settings.HasCustomBoard);
            Assert.Equal(code, reloaded.Settings.BoardCode);
        }
    }

    [Fact]
    public async Task Clearing_the_board_code_returns_to_a_random_board()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(1);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            Assert.True(BoardEditor.TryWrite(BoardEditor.Random(2, 3), out var code, out _));

            var room = (await service.CreateAsync(users[0], new RoomSettings { BoardCode = code })).Room!;
            var cleared = await service.UpdateSettingsAsync(
                room.Id,
                users[0],
                room.Settings with { BoardCode = null });

            Assert.True(cleared.Success);
            Assert.False(cleared.Room!.Settings.HasCustomBoard);
        }
    }

    /// <summary>seed دلخواه باید عیناً به بازی برسد تا برد قابل بازتولید باشد.</summary>
    [Fact]
    public async Task A_chosen_seed_reaches_the_game()
    {
        using var fixture = new SqliteFixture();
        var users = await fixture.SeedUsersAsync(2);
        var service = NewService(fixture, out var context);

        await using (context)
        {
            const ulong seed = 0xDEAD_BEEF_1234_5678;
            var room = (await service.CreateAsync(users[0], new RoomSettings { Seed = seed })).Room!;
            await service.JoinAsync(room.Code, users[1]);

            var result = await service.StartAsync(room.Id, users[0]);

            var games = new GameService(new GameRepository(context, fixture.Clock), fixture.Clock);
            var game = await games.GetAsync(result.GameId!.Value);

            Assert.Equal(seed, game!.State.Options.Seed);
        }
    }
}
