using Hexara.Web.Realtime;

namespace Hexara.Web.Tests;

/// <summary>
/// چت هیچ قانونی از بازی را دست نمی‌زند، ولی سه چیز است که اگر درست نباشند از
/// همان راهِ بی‌خطر آسیب می‌زنند: پیامِ بی‌انتها، سیلِ پیام، و تاریخچه‌ای که
/// بی‌مرز رشد کند.
/// </summary>
public class GameChatTests
{
    private static readonly Guid Game = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Noon = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private static GameChat New(Action<ChatOptions>? tweak = null)
    {
        var options = new ChatOptions();
        tweak?.Invoke(options);

        return new GameChat(options);
    }

    [Fact]
    public void A_message_comes_back_with_the_seat_that_sent_it()
    {
        var chat = New();

        var message = chat.Post(Game, 2, "سلام", Noon);

        Assert.NotNull(message);
        Assert.Equal(2, message.Seat);
        Assert.Equal("سلام", message.Text);
        Assert.Equal(Noon, message.SentAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n\t")]
    public void Nothing_is_posted_for_an_empty_message(string? text) =>
        Assert.Null(New().Post(Game, 0, text, Noon));

    /// <summary>
    /// خط تازه به فاصله تبدیل می‌شود، وگرنه یک پیامِ صد خطِ خالی کلِ پنل را از
    /// دستِ بقیه درمی‌آورد.
    /// </summary>
    [Fact]
    public void Line_breaks_and_runs_of_spaces_collapse()
    {
        var message = New().Post(Game, 0, "  سلام\n\n\n   دنیا  ", Noon);

        Assert.Equal("سلام دنیا", message!.Text);
    }

    [Fact]
    public void Control_characters_are_stripped()
    {
        // ‎U+202E‎ جهتِ متن را برمی‌گرداند و می‌شود با آن پیام را جعل کرد.
        var message = New().Post(Game, 0, "سلام‮دنیا", Noon);

        Assert.Equal("سلام دنیا", message!.Text);
    }

    [Fact]
    public void A_long_message_is_cut_not_rejected()
    {
        var chat = New(o => o.MaxLength = 10);

        var message = chat.Post(Game, 0, new string('د', 50), Noon);

        Assert.Equal(10, message!.Text.Length);
    }

    /// <summary>
    /// متن **بدون** فرار دادنِ HTML ذخیره می‌شود: کلاینت آن را متن می‌گذارد نه
    /// HTML، و فرار دادنِ اینجا فقط باعث می‌شد کاربر ‎&amp;lt;‎ ببیند.
    /// </summary>
    [Fact]
    public void Markup_is_kept_as_plain_text()
    {
        var message = New().Post(Game, 0, "<b>سلام</b>", Noon);

        Assert.Equal("<b>سلام</b>", message!.Text);
    }

    [Fact]
    public void History_comes_back_oldest_first()
    {
        var chat = New();
        chat.Post(Game, 0, "یک", Noon);
        chat.Post(Game, 1, "دو", Noon.AddSeconds(1));

        Assert.Equal(["یک", "دو"], chat.History(Game).Select(m => m.Text));
    }

    [Fact]
    public void History_never_grows_past_its_limit()
    {
        var chat = New(o =>
        {
            o.HistorySize = 3;
            o.BurstLimit = 100;
        });

        for (var i = 0; i < 10; i++)
        {
            chat.Post(Game, 0, $"پیام {i}", Noon.AddSeconds(i));
        }

        Assert.Equal(3, chat.History(Game).Count);
        Assert.Equal(["پیام 7", "پیام 8", "پیام 9"], chat.History(Game).Select(m => m.Text));
    }

    [Fact]
    public void Two_games_never_see_each_other()
    {
        var chat = New();
        chat.Post(Game, 0, "مالِ بازی اول", Noon);

        Assert.Empty(chat.History(Other));
    }

    // ── سرعت ─────────────────────────────────────────────────────────────

    [Fact]
    public void A_flood_is_cut_off_after_the_burst_limit()
    {
        var chat = New(o =>
        {
            o.BurstLimit = 3;
            o.BurstSeconds = 10;
        });

        var accepted = Enumerable.Range(0, 6)
            .Count(i => chat.Post(Game, 0, $"پیام {i}", Noon) is not null);

        Assert.Equal(3, accepted);
    }

    [Fact]
    public void The_limit_opens_again_once_the_window_slides_past()
    {
        var chat = New(o =>
        {
            o.BurstLimit = 2;
            o.BurstSeconds = 10;
        });

        chat.Post(Game, 0, "یک", Noon);
        chat.Post(Game, 0, "دو", Noon);

        Assert.Null(chat.Post(Game, 0, "سه", Noon.AddSeconds(9)));
        Assert.NotNull(chat.Post(Game, 0, "سه", Noon.AddSeconds(11)));
    }

    /// <summary>سیلِ یک بازیکن نباید دهانِ بقیه را ببندد.</summary>
    [Fact]
    public void One_players_flood_does_not_silence_the_others()
    {
        var chat = New(o => o.BurstLimit = 2);

        chat.Post(Game, 0, "یک", Noon);
        chat.Post(Game, 0, "دو", Noon);

        Assert.Null(chat.Post(Game, 0, "سه", Noon));
        Assert.NotNull(chat.Post(Game, 1, "سلام", Noon));
    }

    // ── کلید خاموش ───────────────────────────────────────────────────────

    /// <summary>
    /// خاموش که باشد باید *واقعاً* خاموش باشد؛ نه اینکه پیام بگیرد و پنهان کند.
    /// </summary>
    [Fact]
    public void Nothing_works_when_chat_is_off()
    {
        var chat = New(o => o.Enabled = false);

        Assert.False(chat.Enabled);
        Assert.Null(chat.Post(Game, 0, "سلام", Noon));
        Assert.Empty(chat.History(Game));
    }

    [Fact]
    public void Forgetting_a_game_clears_its_history()
    {
        var chat = New();
        chat.Post(Game, 0, "سلام", Noon);

        chat.Forget(Game);

        Assert.Empty(chat.History(Game));
    }
}
