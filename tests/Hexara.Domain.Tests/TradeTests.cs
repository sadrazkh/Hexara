using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Domain.Tests;

/// <summary>
/// معامله — با بانک و با بازیکن‌ها.
///
/// این بخش از موتور کامل پیاده شده بود ولی هیچ تستی نداشت، چون تا مدتی هیچ راهی
/// هم برای رسیدن به آن از رابط نبود و فقط بات استفاده‌اش می‌کرد. با اضافه شدن
/// رابطِ معامله، این مسیر جلوی دست بازیکن‌ها افتاد و بی‌تست ماندنش خطر بود.
/// </summary>
public class TradeTests
{
    private static GameState Ready(int players = 3)
    {
        var state = Games.New(players);
        Games.RunSetup(state);
        Games.StartMainPhase(state, 0);

        return state;
    }

    /// <summary>
    /// بردی که بازیکن ۰ روی یک بندرِ دلخواه آبادی دارد.
    ///
    /// بندرها سرِ ساخت برد روی ساحل می‌نشینند، پس به‌جای ساختن دستیِ برد، آبادیِ
    /// موجود را روی گوشه‌ی بندر می‌گذاریم.
    /// </summary>
    private static GameState WithPortFor(int player, Resource? kind, out int rate)
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Games.New(3, seed);
            var port = state.Board.Ports.FirstOrDefault(p => p.Resource == kind);
            if (port is null) continue;

            var vertex = port.Vertices().First();
            state.PlaceBuilding(vertex, new Building(player, BuildingKind.Settlement));

            Games.StartMainPhase(state, player);
            rate = GameEngine.MaritimeRate(state, player, kind ?? Resource.Wool);

            return state;
        }

        throw new InvalidOperationException($"بردی با بندر {kind} پیدا نشد.");
    }

    // ── معامله با بانک ──────────────────────────────────────────────────

    [Fact]
    public void The_bank_trades_four_for_one_without_a_port()
    {
        var state = Ready();
        Games.Give(state, 0, (Resource.Wool, 4));

        var result = GameEngine.Apply(state, new MaritimeTrade(0, Resource.Wool, Resource.Brick));

        Assert.True(result.Success);
        Assert.Equal(0, state.Player(0)[Resource.Wool]);
        Assert.Equal(1, state.Player(0)[Resource.Brick]);
    }

    [Fact]
    public void A_generic_port_makes_it_three_for_one()
    {
        var state = WithPortFor(0, null, out var rate);
        Assert.Equal(3, rate);

        Games.Give(state, 0, (Resource.Wool, 3));

        Assert.True(GameEngine.Apply(state, new MaritimeTrade(0, Resource.Wool, Resource.Ore)).Success);
        Assert.Equal(0, state.Player(0)[Resource.Wool]);
        Assert.Equal(1, state.Player(0)[Resource.Ore]);
    }

    [Fact]
    public void A_matching_port_makes_it_two_for_one()
    {
        var state = WithPortFor(0, Resource.Wool, out var rate);
        Assert.Equal(2, rate);

        Games.Give(state, 0, (Resource.Wool, 2));

        Assert.True(GameEngine.Apply(state, new MaritimeTrade(0, Resource.Wool, Resource.Ore)).Success);
        Assert.Equal(0, state.Player(0)[Resource.Wool]);
        Assert.Equal(1, state.Player(0)[Resource.Ore]);
    }

    /// <summary>بندرِ اختصاصیِ پشم روی دادنِ گندم تخفیفی نمی‌دهد.</summary>
    [Fact]
    public void A_matching_port_only_discounts_its_own_resource()
    {
        var state = WithPortFor(0, Resource.Wool, out _);

        Assert.Equal(2, GameEngine.MaritimeRate(state, 0, Resource.Wool));
        Assert.Equal(4, GameEngine.MaritimeRate(state, 0, Resource.Grain));
    }

    [Fact]
    public void Trading_a_resource_for_itself_is_refused()
    {
        var state = Ready();
        Games.Give(state, 0, (Resource.Wool, 4));

        var result = GameEngine.Apply(state, new MaritimeTrade(0, Resource.Wool, Resource.Wool));

        Assert.Equal(GameError.CannotTradeTheSameResource, result.Error);
    }

    [Fact]
    public void The_bank_refuses_when_you_are_one_short()
    {
        var state = Ready();
        Games.Give(state, 0, (Resource.Wool, 3));

        var result = GameEngine.Apply(state, new MaritimeTrade(0, Resource.Wool, Resource.Brick));

        Assert.Equal(GameError.NotEnoughResources, result.Error);
        Assert.Equal(3, state.Player(0)[Resource.Wool]);
    }

    [Fact]
    public void The_bank_cannot_pay_what_it_does_not_have()
    {
        var state = Ready();
        Games.Give(state, 0, (Resource.Wool, 4));

        // بانک را از آجر خالی می‌کنیم.
        state.BankTake(Resource.Brick, state.BankOf(Resource.Brick));

        var result = GameEngine.Apply(state, new MaritimeTrade(0, Resource.Wool, Resource.Brick));

        Assert.Equal(GameError.BankCannotPay, result.Error);
    }

    [Fact]
    public void Only_the_player_whose_turn_it_is_can_trade_with_the_bank()
    {
        var state = Ready();
        Games.Give(state, 1, (Resource.Wool, 4));

        var result = GameEngine.Apply(state, new MaritimeTrade(1, Resource.Wool, Resource.Brick));

        Assert.Equal(GameError.NotYourTurn, result.Error);
    }

    // ── معامله با بازیکن‌ها ─────────────────────────────────────────────

    private static GameState WithOffer(out GameState made)
    {
        var state = Ready();
        Games.Give(state, 0, (Resource.Wool, 2));
        Games.Give(state, 1, (Resource.Grain, 1));

        Assert.True(GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Wool] = 2 },
            new Dictionary<Resource, int> { [Resource.Grain] = 1 },
            [])).Success);

        made = state;
        return state;
    }

    [Fact]
    public void An_offer_with_no_recipients_goes_to_everyone_else()
    {
        WithOffer(out var state);
        var offer = state.PendingTrade!;

        Assert.Equal(0, offer.Proposer);
        Assert.Equal([1, 2], offer.Responses.Keys.Order());
        Assert.All(offer.Responses.Values, r => Assert.Equal(TradeResponse.Pending, r));
    }

    [Fact]
    public void A_full_trade_moves_both_bundles()
    {
        WithOffer(out var state);

        Assert.True(GameEngine.Apply(state, new RespondToTrade(1, Accept: true)).Success);
        Assert.True(GameEngine.Apply(state, new ConfirmTrade(0, Partner: 1)).Success);

        Assert.Equal(0, state.Player(0)[Resource.Wool]);
        Assert.Equal(1, state.Player(0)[Resource.Grain]);
        Assert.Equal(2, state.Player(1)[Resource.Wool]);
        Assert.Equal(0, state.Player(1)[Resource.Grain]);

        Assert.Null(state.PendingTrade);
    }

    /// <summary>معامله بین دو بازیکن است، پس مجموع کارت‌های روی میز عوض نمی‌شود.</summary>
    [Fact]
    public void A_trade_between_players_creates_no_cards()
    {
        WithOffer(out var state);
        var before = Games.Hands(state).Values.Sum();

        Assert.True(GameEngine.Apply(state, new RespondToTrade(1, Accept: true)).Success);
        Assert.True(GameEngine.Apply(state, new ConfirmTrade(0, Partner: 1)).Success);

        Assert.Equal(before, Games.Hands(state).Values.Sum());
    }

    [Fact]
    public void You_cannot_settle_with_someone_who_refused()
    {
        WithOffer(out var state);

        Assert.True(GameEngine.Apply(state, new RespondToTrade(1, Accept: false)).Success);

        var result = GameEngine.Apply(state, new ConfirmTrade(0, Partner: 1));

        Assert.Equal(GameError.PartnerDidNotAccept, result.Error);
    }

    [Fact]
    public void You_cannot_settle_with_someone_who_has_not_answered()
    {
        WithOffer(out var state);

        Assert.Equal(GameError.PartnerDidNotAccept, GameEngine.Apply(state, new ConfirmTrade(0, 1)).Error);
    }

    [Fact]
    public void Accepting_without_the_goods_is_refused()
    {
        var state = Ready();
        Games.Give(state, 0, (Resource.Wool, 2));

        Assert.True(GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Wool] = 2 },
            new Dictionary<Resource, int> { [Resource.Grain] = 1 },
            [])).Success);

        // بازیکن ۱ گندمی ندارد.
        var result = GameEngine.Apply(state, new RespondToTrade(1, Accept: true));

        Assert.Equal(GameError.NotEnoughResources, result.Error);
    }

    /// <summary>
    /// دست‌ها بین پذیرش و قطعی‌کردن ممکن است عوض شوند، پس موتور دوباره می‌سنجد.
    /// بی این بررسی، پیشنهاددهنده می‌توانست چیزی بگیرد که طرف دیگر دیگر ندارد.
    /// </summary>
    [Fact]
    public void Settling_rechecks_hands_that_changed_after_the_promise()
    {
        WithOffer(out var state);

        Assert.True(GameEngine.Apply(state, new RespondToTrade(1, Accept: true)).Success);

        state.Player(1).Remove(Resource.Grain, 1);

        Assert.Equal(GameError.NotEnoughResources, GameEngine.Apply(state, new ConfirmTrade(0, 1)).Error);
    }

    [Fact]
    public void Someone_who_was_not_asked_cannot_answer()
    {
        var state = Ready();
        Games.Give(state, 0, (Resource.Wool, 2));

        Assert.True(GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Wool] = 2 },
            new Dictionary<Resource, int> { [Resource.Grain] = 1 },
            [1])).Success);

        Assert.Equal(GameError.NotInvitedToTrade, GameEngine.Apply(state, new RespondToTrade(2, true)).Error);
    }

    [Fact]
    public void Only_one_offer_sits_on_the_table()
    {
        WithOffer(out var state);

        var second = GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Wool] = 1 },
            new Dictionary<Resource, int> { [Resource.Ore] = 1 },
            []));

        Assert.Equal(GameError.TradeAlreadyOnTheTable, second.Error);
    }

    [Fact]
    public void An_offer_needs_something_on_both_sides()
    {
        var state = Ready();
        Games.Give(state, 0, (Resource.Wool, 2));

        var result = GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Wool] = 2 },
            new Dictionary<Resource, int>(),
            []));

        Assert.Equal(GameError.EmptyTrade, result.Error);
    }

    /// <summary>مقدارِ صفر یعنی نبودن، نه یعنی «چیزی بده».</summary>
    [Fact]
    public void Zero_amounts_do_not_count_as_an_offer()
    {
        var state = Ready();
        Games.Give(state, 0, (Resource.Wool, 2));

        var result = GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Wool] = 2 },
            new Dictionary<Resource, int> { [Resource.Grain] = 0 },
            []));

        Assert.Equal(GameError.EmptyTrade, result.Error);
    }

    [Fact]
    public void You_cannot_offer_what_you_do_not_have()
    {
        var state = Ready();

        var result = GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Wool] = 5 },
            new Dictionary<Resource, int> { [Resource.Grain] = 1 },
            []));

        Assert.Equal(GameError.NotEnoughResources, result.Error);
    }

    [Fact]
    public void Only_the_proposer_can_settle_or_withdraw()
    {
        WithOffer(out var state);

        Assert.True(GameEngine.Apply(state, new RespondToTrade(1, Accept: true)).Success);

        Assert.Equal(GameError.NotYourTrade, GameEngine.Apply(state, new ConfirmTrade(1, 1)).Error);
        Assert.NotNull(state.PendingTrade);
    }

    [Fact]
    public void Withdrawing_clears_the_table()
    {
        WithOffer(out var state);

        Assert.True(GameEngine.Apply(state, new CancelTrade(0)).Success);
        Assert.Null(state.PendingTrade);
    }

    [Fact]
    public void Answering_when_nothing_is_on_the_table_is_refused()
    {
        var state = Ready();

        Assert.Equal(GameError.NoTradeOnTheTable, GameEngine.Apply(state, new RespondToTrade(1, true)).Error);
    }

    /// <summary>پیشنهاد فقط سرِ بدنه‌ی نوبت معنا دارد، نه وسط انداختن تاس.</summary>
    [Fact]
    public void You_cannot_offer_before_rolling()
    {
        var state = Games.New();
        Games.RunSetup(state);
        state.CurrentPlayer = 0;
        state.Phase = TurnPhase.Roll;
        Games.Give(state, 0, (Resource.Wool, 2));

        var result = GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Wool] = 2 },
            new Dictionary<Resource, int> { [Resource.Grain] = 1 },
            []));

        Assert.Equal(GameError.WrongPhase, result.Error);
    }

    /// <summary>پیشنهادِ روی میز باید از عکس وضعیت سالم بیرون بیاید.</summary>
    [Fact]
    public void An_offer_survives_a_snapshot()
    {
        WithOffer(out var state);
        Assert.True(GameEngine.Apply(state, new RespondToTrade(1, Accept: true)).Success);

        var back = GameState.Restore(state.ToSnapshot());
        var offer = back.PendingTrade!;

        Assert.Equal(0, offer.Proposer);
        Assert.Equal(2, offer.Give[Resource.Wool]);
        Assert.Equal(1, offer.Take[Resource.Grain]);
        Assert.Equal(TradeResponse.Accepted, offer.Responses[1]);
        Assert.Equal([1], offer.AcceptedBy);
    }
}
