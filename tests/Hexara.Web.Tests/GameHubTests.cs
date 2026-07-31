using Hexara.Web.Realtime;

namespace Hexara.Web.Tests;

/// <summary>
/// آنچه فقط در *رفتار* هاب دیده می‌شود.
///
/// این‌ها تا حالا آزمون نداشتند و من دو بار در گزارش‌هایم نوشتم که «با بازخوانی
/// کد مطمئن شدم». اینجا همان ادعاها سنجیده می‌شوند — چون هر دو امنیتی‌اند و
/// شکستنشان هیچ صدایی نمی‌دهد: بازی درست کار می‌کند و فقط کسی چیزی می‌بیند که
/// نباید.
/// </summary>
public class GameHubTests : IClassFixture<HexaraApp>
{
    private readonly HexaraApp _app;

    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Stranger = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    public GameHubTests(HexaraApp app) => _app = app;

    private static string Players(Guid gameId) => $"game:{gameId}";

    // ── پیوستن ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_player_joins_the_players_group()
    {
        await using var seat = HubHarness.For(_app, Alice);
        var gameId = await seat.NewGameAsync([Alice, Bob]);

        var result = await seat.Hub.Join(gameId);

        Assert.True(result.Success);
        Assert.Contains(Players(gameId), seat.Groups.GroupsOf("conn-1"));
        Assert.DoesNotContain(GameBroadcaster.WatchersOf(gameId), seat.Groups.GroupsOf("conn-1"));
    }

    /// <summary>
    /// **این آزمونِ مرکزیِ این فایل است.**
    ///
    /// چت و «حضور» هر دو به گروه بازیکن‌ها می‌روند. اگر تماشاچی آن‌جا بود، حرفِ
    /// خصوصیِ سرِ میز («سه گندم دارم») را می‌شنید و می‌توانست به یکی از
    /// بازیکن‌ها برساند — راهِ تقلبی که هیچ‌جای دیگر بسته نمی‌شود.
    /// </summary>
    [Fact]
    public async Task A_spectator_never_lands_in_the_players_group()
    {
        await using var watcher = HubHarness.For(_app, Stranger, "conn-watch");
        var gameId = await watcher.NewGameAsync([Alice, Bob]);

        var result = await watcher.Hub.Join(gameId);

        Assert.True(result.Success);

        var groups = watcher.Groups.GroupsOf("conn-watch");
        Assert.Contains(GameBroadcaster.WatchersOf(gameId), groups);
        Assert.DoesNotContain(Players(gameId), groups);
    }

    [Fact]
    public async Task A_spectator_gets_a_view_without_a_seat_or_a_hand()
    {
        await using var watcher = HubHarness.For(_app, Stranger, "conn-watch");
        var gameId = await watcher.NewGameAsync([Alice, Bob]);

        var view = (await watcher.Hub.Join(gameId)).View;

        Assert.NotNull(view);
        Assert.Null(view.Seat);
        Assert.Null(view.Hand);
    }

    /// <summary>حضور درباره‌ی بازیکن‌هاست؛ تماشاچی نباید کسی را آنلاین کند.</summary>
    [Fact]
    public async Task A_spectator_announces_no_presence()
    {
        await using var watcher = HubHarness.For(_app, Stranger, "conn-watch");
        var gameId = await watcher.NewGameAsync([Alice, Bob]);

        await watcher.Hub.Join(gameId);

        Assert.DoesNotContain(watcher.Clients.Messages, m => m.Method == "presence");
    }

    [Fact]
    public async Task A_player_joining_announces_presence_to_the_others()
    {
        await using var seat = HubHarness.For(_app, Alice);
        var gameId = await seat.NewGameAsync([Alice, Bob]);

        await seat.Hub.Join(gameId);

        var presence = Assert.Single(seat.Clients.Messages, m => m.Method == "presence");
        Assert.Equal($"othersInGroup:{Players(gameId)}", presence.Target);
        Assert.Equal(Alice, presence.Args[0]);
        Assert.Equal(true, presence.Args[1]);
    }

    [Fact]
    public async Task Joining_a_game_that_does_not_exist_is_refused()
    {
        await using var seat = HubHarness.For(_app, Alice);

        var result = await seat.Hub.Join(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("gameNotFound", result.Error);
        Assert.Empty(seat.Groups.GroupsOf("conn-1"));
    }

    // ── چت ───────────────────────────────────────────────────────────────

    /// <summary>
    /// چت به گروهِ بازیکن‌ها می‌رود و **نه** به گروه تماشاچی‌ها. اگر روزی کسی
    /// مقصد را عوض کند، همین‌جا لو می‌رود.
    /// </summary>
    [Fact]
    public async Task Chat_goes_to_the_players_group_only()
    {
        await using var seat = HubHarness.For(_app, Alice);
        var gameId = await seat.NewGameAsync([Alice, Bob]);
        await seat.Hub.Join(gameId);

        await seat.Hub.SendChat(gameId, "سلام");

        var chat = Assert.Single(seat.Clients.Messages, m => m.Method == "chat");
        Assert.Equal($"group:{Players(gameId)}", chat.Target);
        Assert.Empty(seat.Clients.ToGroup(GameBroadcaster.WatchersOf(gameId)));
    }

    /// <summary>تماشاچی صندلی ندارد، پس پیامش اصلاً فرستاده نمی‌شود.</summary>
    [Fact]
    public async Task A_spectator_cannot_send_chat()
    {
        await using var watcher = HubHarness.For(_app, Stranger, "conn-watch");
        var gameId = await watcher.NewGameAsync([Alice, Bob]);
        await watcher.Hub.Join(gameId);

        await watcher.Hub.SendChat(gameId, "من اینجام");

        Assert.DoesNotContain(watcher.Clients.Messages, m => m.Method == "chat");
    }

    [Fact]
    public async Task A_spectator_cannot_read_the_chat_history()
    {
        await using var seat = HubHarness.For(_app, Alice);
        var gameId = await seat.NewGameAsync([Alice, Bob]);
        await seat.Hub.Join(gameId);
        await seat.Hub.SendChat(gameId, "یک راز");

        await using var watcher = HubHarness.For(_app, Stranger, "conn-watch");

        Assert.NotEmpty(await seat.Hub.ChatHistory(gameId));
        Assert.Empty(await watcher.Hub.ChatHistory(gameId));
    }

    [Fact]
    public async Task An_empty_message_is_never_broadcast()
    {
        await using var seat = HubHarness.For(_app, Alice);
        var gameId = await seat.NewGameAsync([Alice, Bob]);
        await seat.Hub.Join(gameId);

        await seat.Hub.SendChat(gameId, "   ");

        Assert.DoesNotContain(seat.Clients.Messages, m => m.Method == "chat");
    }

    // ── صدا ──────────────────────────────────────────────────────────────

    /// <summary>
    /// بی پیکربندی، بلیتی در کار نیست — نه برای بازیکن نه برای تماشاچی. با
    /// پیکربندی هم تماشاچی نباید بلیت بگیرد، و همان شرطِ صندلی این را می‌بندد.
    /// </summary>
    [Fact]
    public async Task No_voice_ticket_without_a_seat()
    {
        await using var watcher = HubHarness.For(_app, Stranger, "conn-watch");
        var gameId = await watcher.NewGameAsync([Alice, Bob]);

        Assert.Null(await watcher.Hub.VoiceTicket(gameId));
    }

    // ── رفتن ─────────────────────────────────────────────────────────────

    /// <summary>
    /// رفتن باید هر دو گروه را پاک کند. تماشاچی و بازیکن از یک متد رد می‌شوند و
    /// اگر فقط یکی برداشته می‌شد، اتصالِ رفته در گروهِ دیگری می‌ماند.
    /// </summary>
    [Fact]
    public async Task Leaving_clears_both_groups()
    {
        await using var seat = HubHarness.For(_app, Alice);
        var gameId = await seat.NewGameAsync([Alice, Bob]);
        await seat.Hub.Join(gameId);

        await seat.Hub.Leave(gameId);

        Assert.True(seat.Groups.Removed("conn-1", Players(gameId)));
        Assert.True(seat.Groups.Removed("conn-1", GameBroadcaster.WatchersOf(gameId)));
    }

    // ── جداییِ اتاق‌ها ────────────────────────────────────────────────────

    [Fact]
    public async Task Two_games_never_share_a_group()
    {
        await using var seat = HubHarness.For(_app, Alice);
        var first = await seat.NewGameAsync([Alice, Bob]);
        var second = await seat.NewGameAsync([Alice, Bob]);

        Assert.NotEqual(Players(first), Players(second));
        Assert.NotEqual(GameBroadcaster.WatchersOf(first), GameBroadcaster.WatchersOf(second));
        Assert.NotEqual(Players(first), GameBroadcaster.WatchersOf(first));
    }
}
