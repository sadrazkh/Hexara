using Hexara.Domain.Board;
using Hexara.Domain.Game;
using Hexara.Web.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Hexara.Web.Tests;

/// <summary>
/// هر چیزی که رابط با نامِ یک عضوِ enum ترجمه می‌کند باید ترجمه داشته باشد.
///
/// این تست از دو اشکالِ واقعیِ یک‌شکل درآمده: مرحله‌های بازی و بیست‌وسه خطای
/// موتور ترجمه نداشتند و بازیکن به‌جای متن، کلیدِ خام می‌دید («‎game.error.
/// CannotTradeTheSameResource‎»). هیچ کامپایلری این را نمی‌گیرد، چون کلید در
/// زمان اجرا از نام عضو ساخته می‌شود: ‎t(`game.error.${outcome.error}`)‎.
///
/// پس اضافه‌کردن هر عضو تازه به این ‎enum‎ها از همین‌جا صدا درمی‌آورد.
/// </summary>
public class TranslationCoverageTests : IClassFixture<HexaraApp>
{
    private readonly UiTranslator _t;

    public TranslationCoverageTests(HexaraApp app)
    {
        // مترجم را از خودِ برنامه می‌گیریم تا همان فایل‌هایی خوانده شود که سرو می‌شود.
        _t = app.Services.CreateScope().ServiceProvider.GetRequiredService<UiTranslator>();
    }

    /// <summary>ترجمه‌ی نبوده، خودِ کلید برمی‌گردد — پس همین برابری یعنی نبودن.</summary>
    private void AssertTranslated(string key)
    {
        foreach (var culture in UiTranslator.SupportedCultures)
        {
            var value = _t.Catalog(culture).GetValueOrDefault(key);

            Assert.False(
                string.IsNullOrWhiteSpace(value),
                $"کلید «{key}» در زبان «{culture}» ترجمه ندارد.");
        }
    }

    public static TheoryData<string> Errors()
    {
        var data = new TheoryData<string>();
        foreach (var error in Enum.GetValues<GameError>())
        {
            if (error != GameError.None) data.Add(error.ToString());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public void Every_engine_error_has_a_message(string error) =>
        AssertTranslated($"game.error.{error}");

    /// <summary>وضعیت‌هایی که خودِ لایه‌ی وب برمی‌گرداند، نه موتور.</summary>
    [Theory]
    [InlineData("Conflict")]
    [InlineData("GameNotFound")]
    [InlineData("NotYourSeat")]
    public void Every_move_status_has_a_message(string status) =>
        AssertTranslated($"game.error.{status}");

    public static TheoryData<string> Phases()
    {
        var data = new TheoryData<string>();
        foreach (var phase in Enum.GetValues<TurnPhase>()) data.Add(phase.ToString());

        return data;
    }

    [Theory]
    [MemberData(nameof(Phases))]
    public void Every_phase_has_a_label(string phase) => AssertTranslated($"game.phase.{phase}");

    public static TheoryData<string> Events()
    {
        var data = new TheoryData<string>();

        foreach (var type in typeof(GameEvent).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(GameEvent).IsAssignableFrom(t)))
        {
            data.Add(type.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void Every_event_has_a_label(string kind) => AssertTranslated($"game.event.{kind}");

    public static TheoryData<string> Resources()
    {
        var data = new TheoryData<string>();
        foreach (var resource in Enum.GetValues<Resource>()) data.Add(resource.ToString());

        return data;
    }

    [Theory]
    [MemberData(nameof(Resources))]
    public void Every_resource_has_a_name(string resource) =>
        AssertTranslated($"game.resource.{resource}");

    public static TheoryData<string> Terrains()
    {
        var data = new TheoryData<string>();
        foreach (var terrain in Enum.GetValues<Terrain>()) data.Add(terrain.ToString());

        return data;
    }

    [Theory]
    [MemberData(nameof(Terrains))]
    public void Every_terrain_has_a_name(string terrain) =>
        AssertTranslated($"board.terrain.{terrain}");
}
