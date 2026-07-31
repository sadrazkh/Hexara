using Hexara.Application.Rooms;

namespace Hexara.Application.Tests;

/// <summary>
/// قواعد خانگی.
///
/// خطرِ اینجا سکوت است: یک عددِ بیرون از کران نه خطا می‌دهد نه دیده می‌شود — فقط
/// بازی‌ای می‌سازد که یا نمی‌شود بردش یا همان لحظه‌ی ساخت می‌ترکد. پس کران‌ها
/// سرورند و آزمون دارند.
/// </summary>
public class HouseRulesTests
{
    [Fact]
    public void The_defaults_are_the_classic_game()
    {
        var rules = new HouseRules();

        Assert.True(rules.IsClassic);
        Assert.Equal(7, rules.DiscardLimit);
        Assert.Equal(4, rules.BankTradeRate);
        Assert.Equal(5, rules.LongestRoadMinimum);
        Assert.Equal(3, rules.LargestArmyMinimum);
    }

    /// <summary>
    /// پیش‌فرض‌های اینجا باید **مو‌به‌مو** همان‌های موتور باشند، وگرنه اتاقی که
    /// دست به قواعد نزده بازیِ دیگری می‌گیرد.
    /// </summary>
    [Fact]
    public void The_defaults_match_the_engine_exactly()
    {
        var engine = new Domain.Game.GameOptions { PlayerCount = 4 };
        var fromRoom = new RoomSettings().ToGameOptions(4, 1);

        Assert.Equal(engine.DiscardLimit, fromRoom.DiscardLimit);
        Assert.Equal(engine.BankPerResource, fromRoom.BankPerResource);
        Assert.Equal(engine.FriendlyRobberThreshold, fromRoom.FriendlyRobberThreshold);
        Assert.Equal(engine.LongestRoadMinimum, fromRoom.LongestRoadMinimum);
        Assert.Equal(engine.LargestArmyMinimum, fromRoom.LargestArmyMinimum);
        Assert.Equal(engine.BankTradeRate, fromRoom.BankTradeRate);
        Assert.Equal(engine.TradeWindowSeconds, fromRoom.TradeWindowSeconds);
        Assert.Equal(engine.SettlementsPerPlayer, fromRoom.SettlementsPerPlayer);
        Assert.Equal(engine.CitiesPerPlayer, fromRoom.CitiesPerPlayer);
        Assert.Equal(engine.RoadsPerPlayer, fromRoom.RoadsPerPlayer);
    }

    [Fact]
    public void Every_rule_reaches_the_engine()
    {
        var rules = new HouseRules
        {
            DiscardLimit = 9,
            BankPerResource = 25,
            FriendlyRobberThreshold = 3,
            LongestRoadMinimum = 4,
            LargestArmyMinimum = 2,
            BankTradeRate = 3,
            TradeWindowSeconds = 45,
            SettlementsPerPlayer = 6,
            CitiesPerPlayer = 5,
            RoadsPerPlayer = 18
        };

        var options = new RoomSettings { Rules = rules }.ToGameOptions(4, 1);

        Assert.Equal(9, options.DiscardLimit);
        Assert.Equal(25, options.BankPerResource);
        Assert.Equal(3, options.FriendlyRobberThreshold);
        Assert.Equal(4, options.LongestRoadMinimum);
        Assert.Equal(2, options.LargestArmyMinimum);
        Assert.Equal(3, options.BankTradeRate);
        Assert.Equal(45, options.TradeWindowSeconds);
        Assert.Equal(6, options.SettlementsPerPlayer);
        Assert.Equal(5, options.CitiesPerPlayer);
        Assert.Equal(18, options.RoadsPerPlayer);
    }

    [Fact]
    public void Changing_anything_stops_being_classic()
    {
        Assert.False(new HouseRules { DiscardLimit = 8 }.IsClassic);
        Assert.True(new HouseRules { DiscardLimit = 7 }.IsClassic);
    }

    // ── کران‌ها ──────────────────────────────────────────────────────────

    [Fact]
    public void The_classic_rules_are_valid()
    {
        Assert.True(HouseRules.Classic.IsValid);
        Assert.True(new RoomSettings().IsValid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(-5)]
    [InlineData(int.MaxValue)]
    public void A_discard_limit_outside_the_range_is_refused(int limit) =>
        Assert.False(new HouseRules { DiscardLimit = limit }.IsValid);

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void A_bank_rate_outside_the_range_is_refused(int rate) =>
        Assert.False(new HouseRules { BankTradeRate = rate }.IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(400)]
    public void A_trade_window_outside_the_range_is_refused(int seconds) =>
        Assert.False(new HouseRules { TradeWindowSeconds = seconds }.IsValid);

    /// <summary>
    /// یک عددِ نجومی همان چیزی است که بازی را سرِ ساخت می‌ترکاند؛ باید پیش از
    /// رسیدن به موتور رد شود.
    /// </summary>
    [Fact]
    public void An_absurd_piece_count_is_refused()
    {
        Assert.False(new HouseRules { RoadsPerPlayer = int.MaxValue }.IsValid);
        Assert.False(new HouseRules { SettlementsPerPlayer = 1_000_000 }.IsValid);
    }

    /// <summary>
    /// با جاده‌ی کمتر از حدِ نشان، «طولانی‌ترین جاده» هرگز گرفتنی نیست — بازی سرِ
    /// پا می‌ماند ولی یک قانونش مرده است، و این بدتر از یک خطاست.
    /// </summary>
    [Fact]
    public void Roads_below_the_longest_road_minimum_are_refused()
    {
        Assert.False(new HouseRules { RoadsPerPlayer = 4, LongestRoadMinimum = 5 }.IsValid);
        Assert.True(new HouseRules { RoadsPerPlayer = 5, LongestRoadMinimum = 5 }.IsValid);
    }

    /// <summary>قواعدِ نامعتبر باید کلِ تنظیماتِ اتاق را نامعتبر کند.</summary>
    [Fact]
    public void Bad_rules_make_the_whole_room_invalid()
    {
        var settings = new RoomSettings { Rules = new HouseRules { BankTradeRate = 99 } };

        Assert.False(settings.IsValid);
    }
}
