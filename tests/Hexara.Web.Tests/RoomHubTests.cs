using Hexara.Application.Rooms;

namespace Hexara.Web.Tests;

/// <summary>
/// گفت‌وگو و صدا در اتاق انتظار.
///
/// اتاق یک تفاوت مهم با بازی دارد: <c>Join</c> عضویت نمی‌خواهد، پس هر کسی که کد
/// را دارد اتاق را *می‌بیند*. همان قاعده‌ی بازی اینجا هم برقرار است — کسی که
/// صندلی ندارد نه حرف می‌زند و نه حرفِ بقیه را می‌شنود.
/// </summary>
public class RoomHubTests : IClassFixture<HexaraApp>
{
    private readonly HexaraApp _app;

    private static readonly Guid Host = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid Guest = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid Onlooker = Guid.Parse("dddddddd-0000-0000-0000-000000000003");

    public RoomHubTests(HexaraApp app) => _app = app;

    /// <summary>اتاقی با دو صندلی‌نشین: میزبان و مهمان.</summary>
    private static async Task<string> NewRoomAsync(HubHarness harness)
    {
        await harness.EnsureUsersAsync([Host, Guest, Onlooker]);

        var created = await harness.Rooms.CreateAsync(Host, new RoomSettings());
        var code = created.Room!.Code;

        await harness.Rooms.JoinAsync(code, Guest);

        return code;
    }

    // ── گفت‌وگو ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_seated_player_can_talk_and_only_members_hear_it()
    {
        await using var host = HubHarness.For(_app, Host);
        var code = await NewRoomAsync(host);

        await host.Room.SendChat(code, "سلام، شروع کنیم؟");

        var chat = Assert.Single(host.Clients.Messages, m => m.Method == "chat");

        // به کاربرهای صندلی‌دار می‌رود، نه به گروهِ اتاق که تماشاچی هم در آن است.
        Assert.Equal("users", chat.Target);
    }

    /// <summary>
    /// **قاعده‌ی مرکزی:** تماشاچیِ اتاق نه می‌فرستد و نه تاریخچه می‌گیرد.
    /// </summary>
    [Fact]
    public async Task An_onlooker_cannot_talk()
    {
        await using var host = HubHarness.For(_app, Host);
        var code = await NewRoomAsync(host);

        await using var watcher = HubHarness.For(_app, Onlooker, "conn-watch");
        await watcher.Room.Join(code);

        await watcher.Room.SendChat(code, "منم هستم");

        Assert.DoesNotContain(watcher.Clients.Messages, m => m.Method == "chat");
    }

    [Fact]
    public async Task An_onlooker_cannot_read_the_history()
    {
        await using var host = HubHarness.For(_app, Host);
        var code = await NewRoomAsync(host);
        await host.Room.SendChat(code, "یک راز");

        await using var watcher = HubHarness.For(_app, Onlooker, "conn-watch");

        Assert.NotEmpty(await host.Room.ChatHistory(code));
        Assert.Empty(await watcher.Room.ChatHistory(code));
    }

    [Fact]
    public async Task A_seated_player_reads_the_history()
    {
        await using var host = HubHarness.For(_app, Host);
        var code = await NewRoomAsync(host);
        await host.Room.SendChat(code, "سلام");

        await using var guest = HubHarness.For(_app, Guest, "conn-guest");

        var history = await guest.Room.ChatHistory(code);

        Assert.Equal(["سلام"], history.Select(m => m.Text));
    }

    [Fact]
    public async Task An_empty_message_is_never_sent()
    {
        await using var host = HubHarness.For(_app, Host);
        var code = await NewRoomAsync(host);

        await host.Room.SendChat(code, "   ");

        Assert.DoesNotContain(host.Clients.Messages, m => m.Method == "chat");
    }

    [Fact]
    public async Task Talking_in_a_room_that_does_not_exist_does_nothing()
    {
        await using var host = HubHarness.For(_app, Host);

        await host.Room.SendChat("ZZZZZZ", "کسی هست؟");

        Assert.DoesNotContain(host.Clients.Messages, m => m.Method == "chat");
    }

    // ── انتقال گفت‌وگو به بازی ────────────────────────────────────────────

    /// <summary>
    /// تا یک ثانیه پیش داشتند هماهنگ می‌کردند؛ صفحه‌ی بازی نباید با چتِ خالی باز
    /// شود.
    /// </summary>
    [Fact]
    public async Task The_conversation_follows_the_room_into_the_game()
    {
        await using var host = HubHarness.For(_app, Host);
        var code = await NewRoomAsync(host);
        await host.Room.SendChat(code, "بزن بریم");

        var started = await host.Room.Start(code);
        var gameId = started.Room!.GameId;

        Assert.True(started.Success);
        Assert.NotNull(gameId);

        // همان پیام، این بار از هابِ بازی.
        var inGame = await host.Hub.ChatHistory(gameId.Value);
        Assert.Equal(["بزن بریم"], inGame.Select(m => m.Text));

        // و دیگر زیر شناسه‌ی اتاق نمانده.
        Assert.Empty(await host.Room.ChatHistory(code));
    }

    // ── صدا ──────────────────────────────────────────────────────────────

    /// <summary>بی پیکربندی بلیتی نیست — نه برای صندلی‌نشین نه برای تماشاچی.</summary>
    [Fact]
    public async Task No_voice_ticket_when_voice_is_not_configured()
    {
        await using var host = HubHarness.For(_app, Host);
        var code = await NewRoomAsync(host);

        Assert.Null(await host.Room.VoiceTicket(code));
    }

    [Fact]
    public async Task An_onlooker_never_gets_a_voice_ticket()
    {
        await using var host = HubHarness.For(_app, Host);
        var code = await NewRoomAsync(host);

        await using var watcher = HubHarness.For(_app, Onlooker, "conn-watch");

        Assert.Null(await watcher.Room.VoiceTicket(code));
    }

    /// <summary>
    /// اتاق صوتیِ انتظار از اتاق صوتیِ بازی جداست، وگرنه تماشاچیِ اتاق می‌توانست
    /// به صدای خودِ بازی برسد.
    /// </summary>
    [Fact]
    public void The_lobby_voice_room_is_never_the_game_voice_room()
    {
        var id = Guid.NewGuid();

        Assert.NotEqual(
            Hexara.Web.Realtime.LiveKitTokens.LobbyOf(id),
            Hexara.Web.Realtime.LiveKitTokens.RoomOf(id));
    }

    // ── قواعد خانگی ──────────────────────────────────────────────────────

    /// <summary>قواعد باید از دیتابیس سالم برگردند، وگرنه با هر رفرش پاک می‌شوند.</summary>
    [Fact]
    public async Task House_rules_survive_a_round_trip()
    {
        await using var host = HubHarness.For(_app, Host);
        var code = await NewRoomAsync(host);

        var wanted = new HouseRules { DiscardLimit = 9, BankTradeRate = 3, TradeWindowSeconds = 60 };

        var saved = await host.Room.UpdateSettings(
            code,
            new RoomSettingsInput(4, 10, 2, false, false, wanted));

        Assert.True(saved.Success);

        // از نو خوانده می‌شود، نه از پاسخِ همان فراخوانی.
        var again = await host.Room.Join(code);

        Assert.Equal(wanted, again.Room!.Rules);
        Assert.True(again.Room.CustomRules);
    }

    /// <summary>
    /// **کران‌ها سرورند.** فرم فقط راهنماست و یک کلاینتِ دستکاری‌شده هر عددی
    /// می‌تواند بفرستد.
    /// </summary>
    [Fact]
    public async Task Rules_outside_the_range_are_refused()
    {
        await using var host = HubHarness.For(_app, Host);
        var code = await NewRoomAsync(host);

        var absurd = new HouseRules { RoadsPerPlayer = int.MaxValue };

        var result = await host.Room.UpdateSettings(
            code,
            new RoomSettingsInput(4, 10, 2, false, false, absurd));

        Assert.False(result.Success);

        // و اتاق دست‌نخورده مانده.
        var again = await host.Room.Join(code);
        Assert.True(again.Room!.Rules.IsClassic);
    }

    /// <summary>
    /// نفرستادنِ بخشِ قواعد یعنی «دست نزن»، نه «برگرد به کلاسیک» — وگرنه هر بار
    /// که میزبان فقط تعداد بازیکن را عوض می‌کرد، قواعدش بی‌صدا پاک می‌شد.
    /// </summary>
    [Fact]
    public async Task Leaving_the_rules_out_keeps_them()
    {
        await using var host = HubHarness.For(_app, Host);
        var code = await NewRoomAsync(host);

        await host.Room.UpdateSettings(
            code,
            new RoomSettingsInput(4, 10, 2, false, false, new HouseRules { DiscardLimit = 9 }));

        // این بار بدون قواعد، فقط تعداد بازیکن عوض می‌شود.
        await host.Room.UpdateSettings(code, new RoomSettingsInput(5, 10, 2, false, false));

        var again = await host.Room.Join(code);

        Assert.Equal(9, again.Room!.Rules.DiscardLimit);
        Assert.Equal(5, again.Room.MaxPlayers);
    }

    /// <summary>قواعدِ اتاق باید به خودِ بازی برسند، نه اینکه سرِ شروع گم شوند.</summary>
    [Fact]
    public async Task The_rules_reach_the_game_that_starts()
    {
        await using var host = HubHarness.For(_app, Host);
        var code = await NewRoomAsync(host);

        await host.Room.UpdateSettings(
            code,
            new RoomSettingsInput(4, 10, 2, false, false, new HouseRules { DiscardLimit = 9, BankTradeRate = 3 }));

        var started = await host.Room.Start(code);
        Assert.True(started.Success);

        var view = (await host.Hub.Join(started.Room!.GameId!.Value)).View;

        Assert.NotNull(view);
        Assert.Equal(3, view.Players[0].TradeRates.Values.Max());
    }
}
