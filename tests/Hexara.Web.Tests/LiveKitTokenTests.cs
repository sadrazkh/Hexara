using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexara.Application.Common.Interfaces;
using Hexara.Web.Realtime;

namespace Hexara.Web.Tests;

/// <summary>
/// بلیت ورود به اتاق صوتی یک ‎JWT‎ دست‌ساز است، پس شکلش را چیزی جز آزمون نگه
/// نمی‌دارد: یک حرفِ غلط در نامِ ادعا و LiveKit بی‌صدا ردش می‌کند، یا بدتر،
/// اجازه‌ای می‌دهد که نباید.
/// </summary>
public class LiveKitTokenTests
{
    private const string Secret = "یک-رمزِ-آزمایشی-که-هیچ-جا-واقعی-نیست";

    private static readonly Guid Game = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid User = Guid.Parse("bbbbbbbb-5555-6666-7777-888888888888");
    private static readonly DateTimeOffset Noon = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private sealed class FrozenClock : IClock
    {
        public DateTimeOffset UtcNow => Noon;
    }

    private static LiveKitTokens New(Action<LiveKitOptions>? tweak = null)
    {
        var options = new LiveKitOptions
        {
            Enabled = true,
            Url = "wss://live.example.com",
            ApiKey = "APIkey123",
            ApiSecret = Secret
        };

        tweak?.Invoke(options);

        return new LiveKitTokens(options, new FrozenClock());
    }

    /// <summary>بدنه‌ی بلیت را باز می‌کند، بدون اینکه امضا را باور کند.</summary>
    private static JsonElement Payload(string token)
    {
        var body = token.Split('.')[1];
        return JsonSerializer.Deserialize<JsonElement>(Base64Url.DecodeFromChars(body));
    }

    // ── پیکربندی ─────────────────────────────────────────────────────────

    /// <summary>
    /// نبودِ کلید و رمز باید *خودش* یعنی خاموش. اگر «روشن» به تنهایی کافی بود، یک
    /// استقرارِ نیمه‌کاره بلیتِ بی‌امضا می‌داد.
    /// </summary>
    [Theory]
    [InlineData(false, "wss://x", "k", "s")]
    [InlineData(true, "", "k", "s")]
    [InlineData(true, "wss://x", "", "s")]
    [InlineData(true, "wss://x", "k", "")]
    [InlineData(true, "wss://x", "k", "   ")]
    public void Half_a_configuration_counts_as_off(bool enabled, string url, string key, string secret)
    {
        var tokens = New(o =>
        {
            o.Enabled = enabled;
            o.Url = url;
            o.ApiKey = key;
            o.ApiSecret = secret;
        });

        Assert.False(tokens.IsConfigured);
        Assert.Null(tokens.Issue(Game, User, "کسی"));
    }

    [Fact]
    public void A_full_configuration_issues_a_ticket()
    {
        var ticket = New().Issue(Game, User, "ملوان");

        Assert.NotNull(ticket);
        Assert.Equal("wss://live.example.com", ticket.Url);
        Assert.Equal(LiveKitTokens.RoomOf(Game), ticket.Room);
    }

    // ── اتاق ─────────────────────────────────────────────────────────────

    /// <summary>
    /// نامِ اتاق فقط از شناسه‌ی بازی می‌آید؛ دو بازی هرگز به یک اتاق نمی‌رسند.
    /// </summary>
    [Fact]
    public void Every_game_gets_its_own_room()
    {
        var mine = LiveKitTokens.RoomOf(Game);
        var theirs = LiveKitTokens.RoomOf(Guid.NewGuid());

        Assert.NotEqual(mine, theirs);
        Assert.Equal(mine, LiveKitTokens.RoomOf(Game));
    }

    [Fact]
    public void The_ticket_is_scoped_to_that_one_room()
    {
        var ticket = New().Issue(Game, User, "ملوان")!;

        var video = Payload(ticket.Token).GetProperty("video");

        Assert.Equal(LiveKitTokens.RoomOf(Game), video.GetProperty("room").GetString());
        Assert.True(video.GetProperty("roomJoin").GetBoolean());
    }

    /// <summary>
    /// داده از راه LiveKit رد و بدل نمی‌شود. اگر می‌شد، یک کلاینتِ دستکاری‌شده
    /// می‌توانست کانالی بسازد که سرورِ بازی هرگز نمی‌بیندش.
    /// </summary>
    [Fact]
    public void Data_channels_are_shut()
    {
        var video = Payload(New().Issue(Game, User, "ملوان")!.Token).GetProperty("video");

        Assert.False(video.GetProperty("canPublishData").GetBoolean());
        Assert.True(video.GetProperty("canPublish").GetBoolean());
        Assert.True(video.GetProperty("canSubscribe").GetBoolean());
    }

    // ── هویت ─────────────────────────────────────────────────────────────

    [Fact]
    public void The_identity_is_the_user_id_not_the_name()
    {
        var payload = Payload(New().Issue(Game, User, "ملوان")!.Token);

        Assert.Equal(User.ToString(), payload.GetProperty("sub").GetString());
        Assert.Equal("ملوان", payload.GetProperty("name").GetString());
        Assert.Equal("APIkey123", payload.GetProperty("iss").GetString());
    }

    // ── عمر ──────────────────────────────────────────────────────────────

    [Fact]
    public void The_ticket_expires_after_the_configured_minutes()
    {
        var payload = Payload(New(o => o.TokenMinutes = 15).Issue(Game, User, "ملوان")!.Token);

        Assert.Equal(Noon.AddMinutes(15).ToUnixTimeSeconds(), payload.GetProperty("exp").GetInt64());
    }

    /// <summary>ساعتِ کمی جلوترِ کلاینت نباید بلیتِ تازه را باطل کند.</summary>
    [Fact]
    public void The_ticket_is_already_valid_a_minute_ago()
    {
        var payload = Payload(New().Issue(Game, User, "ملوان")!.Token);

        Assert.Equal(Noon.AddMinutes(-1).ToUnixTimeSeconds(), payload.GetProperty("nbf").GetInt64());
    }

    [Fact]
    public void A_zero_lifetime_still_gives_at_least_a_minute()
    {
        var payload = Payload(New(o => o.TokenMinutes = 0).Issue(Game, User, "ملوان")!.Token);

        Assert.True(payload.GetProperty("exp").GetInt64() > Noon.ToUnixTimeSeconds());
    }

    // ── امضا ─────────────────────────────────────────────────────────────

    /// <summary>
    /// امضا با همان رمز و همان الگوریتم بازساخته می‌شود؛ اگر رمز یا ترتیبِ
    /// بخش‌ها فرق کند، LiveKit بی‌صدا رد می‌کند و هیچ‌کس نمی‌فهمد چرا.
    /// </summary>
    [Fact]
    public void The_signature_checks_out_against_the_secret()
    {
        var token = New().Issue(Game, User, "ملوان")!.Token;
        var parts = token.Split('.');

        Assert.Equal(3, parts.Length);

        var expected = Base64Url.EncodeToString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Secret),
            Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}")));

        Assert.Equal(expected, parts[2]);
    }

    [Fact]
    public void A_different_secret_gives_a_different_signature()
    {
        var mine = New().Issue(Game, User, "ملوان")!.Token.Split('.')[2];
        var theirs = New(o => o.ApiSecret = "رمزِ دیگری").Issue(Game, User, "ملوان")!.Token.Split('.')[2];

        Assert.NotEqual(mine, theirs);
    }

    [Fact]
    public void The_header_says_hs256()
    {
        var token = New().Issue(Game, User, "ملوان")!.Token;
        var header = JsonSerializer.Deserialize<JsonElement>(
            Base64Url.DecodeFromChars(token.Split('.')[0]));

        Assert.Equal("HS256", header.GetProperty("alg").GetString());
        Assert.Equal("JWT", header.GetProperty("typ").GetString());
    }

    /// <summary>‎base64url‎ است نه ‎base64‎: بلیت در URL و هدر می‌نشیند.</summary>
    [Fact]
    public void The_token_carries_no_padding_or_url_unsafe_characters()
    {
        var token = New().Issue(Game, User, "ملوان")!.Token;

        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }
}
