using Hexara.Application.Common.Interfaces;
using System.Globalization;
using System.Text.Json;
using Hexara.Application.Games;
using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Application.Tests;

/// <summary>
/// همه‌چیز زیر فرهنگ فارسی، چون زبان پیش‌فرضِ برنامه همین است.
///
/// این تست‌ها از یک اشکال واقعی درآمده‌اند: علامت منفی در فرهنگ فارسی ‎ASCII‎ نیست
/// (‎U+200E U+2212‎ به‌جای ‎U+002D‎). هر جا عددِ ماشینی با فرهنگ جاری نوشته یا خوانده
/// می‌شد، مختصات محوریِ منفی خراب می‌شد: نوشتن رشته‌ی ناخوانا در دیتابیس، و خواندن
/// با <see cref="FormatException"/>. نتیجه‌اش در بازی این بود که ساختن روی نیمه‌ی
/// منفیِ برد اصلاً کار نمی‌کرد و هیچ پیامی هم به بازیکن نمی‌رسید.
///
/// تستِ گردِ فرهنگ‌محور تنها نگهبانِ قابل اعتماد این‌جاست: آنالایزرها رشته‌های
/// درون‌یابی‌شده را نمی‌گیرند، پس ‎$"{q},{r}"‎ بی‌صدا از کنارشان رد می‌شود.
/// </summary>
public class PersianCultureTests : IDisposable
{
    private readonly CultureInfo _culture;
    private readonly CultureInfo _uiCulture;

    public PersianCultureTests()
    {
        _culture = CultureInfo.CurrentCulture;
        _uiCulture = CultureInfo.CurrentUICulture;

        var fa = CultureInfo.GetCultureInfo("fa-IR");
        CultureInfo.CurrentCulture = fa;
        CultureInfo.CurrentUICulture = fa;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
        GC.SuppressFinalize(this);
    }

    /// <summary>اگر این نگذرد، بقیه‌ی تست‌های این کلاس چیزی را نمی‌سنجند.</summary>
    [Fact]
    public void The_culture_really_is_one_with_a_non_ascii_minus()
    {
        Assert.NotEqual("-", CultureInfo.CurrentCulture.NumberFormat.NegativeSign);
        Assert.Equal("‎−1", (-1).ToString(CultureInfo.CurrentCulture));
    }

    /// <summary>
    /// علامت منفیِ فارسی نباید در متن باشد — نه خودش و نه شکلِ ‎escape‎شده‌اش.
    ///
    /// بررسی هر دو شکل لازم است: ‎System.Text.Json‎ نویسه‌های غیر-ASCII را
    /// ‎escape‎ می‌کند، پس جست‌وجوی نویسه‌ی خام در ‎JSON‎ هیچ‌وقت چیزی پیدا نمی‌کند و
    /// تست بی‌آنکه بفهمی از کار می‌افتد.
    /// </summary>
    private static void AssertAsciiMinusOnly(string text)
    {
        Assert.DoesNotContain('−', text);
        Assert.DoesNotContain('‎', text);
        Assert.DoesNotContain("\\u2212", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\u200E", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>در فرهنگ دیگری می‌خوانیم؛ فرهنگِ درخواست همیشه یکی نیست.</summary>
    private static T ReadAs<T>(string json, string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return GameJson.Deserialize<T>(json);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>
    /// همان اتفاقی که در عمل افتاد: حرکت زیر فرهنگ فارسی ذخیره شد و بعد در
    /// درخواستی با فرهنگ دیگر خوانده شد و ‎CatchUp‎ با <see cref="FormatException"/>
    /// پایین آمد. پس نوشتن و خواندن عمداً در دو فرهنگ انجام می‌شود.
    /// </summary>
    [Fact]
    public void A_negative_vertex_survives_being_written_in_persian_and_read_in_english()
    {
        var vertex = VertexId.Of(new Axial(-2, -1), 3);

        var json = GameJson.Serialize<GameAction>(new BuildSettlement(0, vertex));

        AssertAsciiMinusOnly(json);

        var back = Assert.IsType<BuildSettlement>(ReadAs<GameAction>(json, "en-US"));
        Assert.Equal(vertex, back.Vertex);
    }

    [Fact]
    public void A_negative_edge_survives_being_written_in_persian_and_read_in_english()
    {
        var edge = EdgeId.Of(new Axial(-1, -2), 4);

        var json = GameJson.Serialize<GameAction>(new BuildRoad(1, edge));

        AssertAsciiMinusOnly(json);

        var back = Assert.IsType<BuildRoad>(ReadAs<GameAction>(json, "en-US"));
        Assert.Equal(edge, back.Edge);
    }

    /// <summary>
    /// قرارداد سیمِ ‎SignalR‎ عمداً با قالب ذخیره‌سازی فرق دارد (camelCase و
    /// بی‌اعتنا به بزرگی حرف)، پس این‌جا هم همان‌طور ساخته می‌شود تا دقیقاً همان
    /// مسیری سنجیده شود که کلیکِ بازیکن از آن می‌گذرد.
    /// </summary>
    private static JsonSerializerOptions WireOptions()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = GameJson.Options.TypeInfoResolver,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        foreach (var converter in GameJson.Options.Converters)
        {
            options.Converters.Add(converter);
        }

        return options;
    }

    /// <summary>
    /// همان رشته‌ای که مرورگر می‌فرستد — با خطِ تیره‌ی ‎ASCII‎، چون جاوااسکریپت
    /// همیشه همین را می‌سازد. سرور باید بخواندش، فرهنگش هر چه باشد.
    /// </summary>
    [Fact]
    public void The_id_the_browser_sends_is_readable_under_persian()
    {
        const string json = """{"$kind":"BuildSettlement","playerIndex":0,"vertex":"-2,-1,3"}""";

        var action = Assert.IsType<BuildSettlement>(
            JsonSerializer.Deserialize<GameAction>(json, WireOptions()));

        Assert.Equal(VertexId.Of(new Axial(-2, -1), 3), action.Vertex);
    }

    /// <summary>
    /// لاگ حرکت‌ها همان جایی است که واقعاً آسیب دید.
    ///
    /// عکس وضعیت از این اشکال جان سالم برده بود چون ‎HexSnapshot‎ و ‎VertexSnapshot‎
    /// رکوردهایی با ‎int‎ ساده‌اند و عدد ‎JSON‎ می‌شوند، نه رشته. ولی ستون
    /// ‎GameMoves.Action‎ خودِ <see cref="VertexId"/> و <see cref="EdgeId"/> را
    /// نگه می‌دارد که از مبدل رشته‌ای می‌گذرند — و ‎CatchUp‎ همین‌ها را می‌خواند.
    /// </summary>
    [Fact]
    public void A_move_log_written_in_persian_replays_in_english()
    {
        GameAction[] moves =
        [
            new PlaceInitialSettlement(0, VertexId.Of(new Axial(-2, -1), 3)),
            new PlaceInitialRoad(0, EdgeId.Of(new Axial(-1, -2), 4)),
            new BuildCity(1, VertexId.Of(new Axial(-1, 2), 1)),
            new MoveRobber(1, new Axial(-2, 0), null)
        ];

        var stored = moves.Select(GameJson.Serialize).ToArray();

        Assert.All(stored, AssertAsciiMinusOnly);

        var replayed = stored.Select(json => ReadAs<GameAction>(json, "en-US")).ToArray();

        Assert.Equal(moves, replayed);
    }

    /// <summary>کد برد دست‌به‌دست می‌چرخد، پس نباید به زبانِ سازنده‌اش وابسته باشد.</summary>
    [Fact]
    public void A_board_code_made_under_persian_is_ascii_and_reads_back()
    {
        var board = BoardGenerator.Generate(2, 99);

        var code = BoardCode.Encode(board);

        AssertAsciiMinusOnly(code);
        Assert.All(code, ch => Assert.True(ch < 128, $"نویسه‌ی غیر-ASCII در کد برد: U+{(int)ch:X4}"));

        Assert.True(BoardCode.TryDecode(code, out var back, out var error));
        Assert.Equal(BoardCodeError.None, error);
        Assert.Equal(code, BoardCode.Encode(back!));
    }

    /// <summary>
    /// کدی که در رابط انگلیسی ساخته شده باید در رابط فارسی خوانده شود. این همان
    /// چیزی است که با فرهنگ جاری می‌شکست، چون هر طرف علامت منفیِ خودش را می‌نوشت.
    /// </summary>
    [Fact]
    public void A_board_code_made_under_english_reads_under_persian()
    {
        string code;

        var fa = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            code = BoardCode.Encode(BoardGenerator.Generate(2, 7));
        }
        finally
        {
            CultureInfo.CurrentCulture = fa;
        }

        Assert.True(BoardCode.TryDecode(code, out _, out var error));
        Assert.Equal(BoardCodeError.None, error);
    }

    /// <summary>
    /// این رشته‌ها به لاگ و پیام خطا و کلیدِ دیکشنری می‌روند، پس باید در هر زبانی
    /// یک شکل باشند. مقدارِ کانونی عمداً سخت‌کد نشده — موضوعِ سنجش فرهنگ است نه
    /// قاعده‌ی کانونی‌سازی.
    /// </summary>
    [Fact]
    public void Geometry_ids_print_with_an_ascii_minus()
    {
        string[] printed =
        [
            new Axial(-2, -1).ToString(),
            VertexId.Of(new Axial(-2, -1), 3).ToString(),
            EdgeId.Of(new Axial(-1, -2), 4).ToString()
        ];

        foreach (var text in printed)
        {
            Assert.All(text, ch => Assert.True(ch < 128, $"نویسه‌ی غیر-ASCII در «{text}»"));
            Assert.Contains('-', text);
            Assert.DoesNotContain('−', text);
        }
    }

    // ── کلیدهایی که بعداً اضافه شدند ──────────────────────────────────────

    /// <summary>
    /// کلیدهای «جاده‌ی دومِ کارت جاده‌سازی» با دستِ خودمان از مختصات ساخته
    /// می‌شوند، و مختصاتِ منفی روی این برد فراوان است.
    ///
    /// اگر با فرهنگ جاری ساخته می‌شدند، زیر فارسی کلید «‎−1,0,2‎» می‌شد (با منهای
    /// ‎U+2212‎) در حالی که کلاینت «‎-1,0,2‎» می‌فرستد — و هیچ جاده‌ی دومی هرگز
    /// پیدا نمی‌شد، بی‌آنکه خطایی بدهد.
    /// </summary>
    [Fact]
    public async Task Follow_up_road_keys_stay_ascii_under_persian()
    {
        var game = RoadBuildingGame();

        var view = await new GameViewBuilder(new Directory()).BuildAsync(game, 0);

        Assert.NotEmpty(view.Legal.FollowUpRoads);

        foreach (var key in view.Legal.FollowUpRoads.Keys)
        {
            Assert.DoesNotContain('−', key);
            Assert.DoesNotContain('‎', key);
            Assert.Matches("^-?[0-9]+,-?[0-9]+,[0-9]+$", key);
        }

        // و کلیدها واقعاً با همان یال‌هایی که فرستاده شده‌اند جور درمی‌آیند.
        //
        // خودِ این مقایسه هم باید با فرهنگ ناوابسته ساخته شود: نسخه‌ی اولِ همین
        // تست با ‎$"{r.Q},..."‎ نوشته شده بود و زیر فارسی «‎−1‎» تولید کرد، پس
        // شکست — نه چون کد خراب بود، که چون تله دقیقاً همین است و حتی در تست
        // هم گیر می‌اندازد.
        var free = view.Legal.FreeRoads
            .Select(r => string.Create(CultureInfo.InvariantCulture, $"{r.Q},{r.R},{r.Side}"))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(free, view.Legal.FollowUpRoads.Keys.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>دست‌کم یک کلیدِ منفی باید در کار باشد وگرنه تست چیزی را نمی‌سنجد.</summary>
    [Fact]
    public async Task At_least_one_of_those_keys_is_negative()
    {
        var view = await new GameViewBuilder(new Directory()).BuildAsync(RoadBuildingGame(), 0);

        Assert.Contains(view.Legal.FollowUpRoads.Keys, k => k.Contains('-', StringComparison.Ordinal));
    }

    /// <summary>بازی‌ای با کارت جاده‌سازی در دست و یک شبکه‌ی جاده‌ی واقعی.</summary>
    private static StoredGame RoadBuildingGame()
    {
        var ids = Enumerable.Range(0, 3)
            .Select(i => Guid.Parse($"00000000-0000-0000-0000-00000000000{i}"))
            .ToList();

        var state = GameState.Create(new GameOptions { PlayerCount = 3, Seed = 5 }, ids);

        while (state.IsSetup)
        {
            var player = state.CurrentPlayer;
            var vertex = GameEngine.LegalSettlementVertices(state, player)
                .OrderBy(v => v.ToString(), StringComparer.Ordinal).First();
            GameEngine.Apply(state, new PlaceInitialSettlement(player, vertex));

            var edge = vertex.TouchingEdges()
                .Where(e => state.Board.ContainsEdge(e) && state.RoadAt(e) is null)
                .OrderBy(e => e.ToString(), StringComparer.Ordinal).First();
            GameEngine.Apply(state, new PlaceInitialRoad(player, edge));
        }

        var snapshot = state.ToSnapshot() with { Phase = TurnPhase.Main, CurrentPlayer = 0, TurnNumber = 4 };
        var players = snapshot.Players.ToList();
        players[0] = players[0] with
        {
            DevelopmentCards = new Dictionary<DevelopmentCard, int> { [DevelopmentCard.RoadBuilding] = 1 }
        };

        return new StoredGame(
            Guid.NewGuid(),
            GameStatus.Active,
            ids,
            GameState.Restore(snapshot with { Players = players }));
    }

    private sealed class Directory : IPlayerDirectory
    {
        public Task<IReadOnlyList<PlayerProfile>> GetAsync(
            IReadOnlyList<Guid> userIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlayerProfile>>(
                [.. userIds.Select(id => new PlayerProfile(id, "کسی", "#000000", false))]);
    }
}
