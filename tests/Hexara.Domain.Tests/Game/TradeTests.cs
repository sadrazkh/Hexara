using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Domain.Tests.Game;

public class TradeTests
{
    // ── معامله با بانک و بندر ────────────────────────────────────────────

    [Fact]
    public void Without_a_port_the_rate_is_four_to_one()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (Resource.Lumber, 4));

        var result = GameEngine.Apply(state, new MaritimeTrade(0, Resource.Lumber, Resource.Ore));

        Assert.True(result.Success);
        Assert.Equal(0, state.Player(0)[Resource.Lumber]);
        Assert.Equal(1, state.Player(0)[Resource.Ore]);
        Assert.Equal(4, result.Events.OfType<MaritimeTraded>().Single().Rate);
    }

    [Fact]
    public void Three_cards_are_not_enough_without_a_port()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (Resource.Lumber, 3));

        var result = GameEngine.Apply(state, new MaritimeTrade(0, Resource.Lumber, Resource.Ore));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotEnoughResources, result.Error);
    }

    [Fact]
    public void A_generic_port_gives_three_to_one()
    {
        var state = Games.New(players: 2);
        var port = state.Board.Ports.First(p => p.IsGeneric);
        state.PlaceBuilding(port.Vertices().First(), new Building(0, BuildingKind.Settlement));

        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (Resource.Lumber, 3));

        var result = GameEngine.Apply(state, new MaritimeTrade(0, Resource.Lumber, Resource.Ore));

        Assert.True(result.Success);
        Assert.Equal(3, GameEngine.MaritimeRate(state, 0, Resource.Lumber));
        Assert.Equal(0, state.Player(0)[Resource.Lumber]);
    }

    [Fact]
    public void A_matching_port_gives_two_to_one()
    {
        var state = Games.New(players: 2);
        var port = state.Board.Ports.First(p => !p.IsGeneric);
        var traded = port.Resource!.Value;
        state.PlaceBuilding(port.Vertices().First(), new Building(0, BuildingKind.Settlement));

        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (traded, 2));

        var take = TerrainExtensions.AllResources.First(r => r != traded);
        var result = GameEngine.Apply(state, new MaritimeTrade(0, traded, take));

        Assert.True(result.Success);
        Assert.Equal(2, GameEngine.MaritimeRate(state, 0, traded));
        Assert.Equal(0, state.Player(0)[traded]);
        Assert.Equal(1, state.Player(0)[take]);
    }

    /// <summary>بندر اختصاصی فقط برای منبع خودش تخفیف می‌دهد.</summary>
    [Fact]
    public void A_matching_port_does_not_help_other_resources()
    {
        var state = Games.New(players: 2);
        var port = state.Board.Ports.First(p => !p.IsGeneric);
        state.PlaceBuilding(port.Vertices().First(), new Building(0, BuildingKind.Settlement));

        var other = TerrainExtensions.AllResources.First(r => r != port.Resource);

        Assert.Equal(4, GameEngine.MaritimeRate(state, 0, other));
    }

    [Fact]
    public void A_port_only_counts_for_its_owner()
    {
        var state = Games.New(players: 2);
        var port = state.Board.Ports.First(p => p.IsGeneric);
        state.PlaceBuilding(port.Vertices().First(), new Building(0, BuildingKind.Settlement));

        Assert.Equal(3, GameEngine.MaritimeRate(state, 0, Resource.Lumber));
        Assert.Equal(4, GameEngine.MaritimeRate(state, 1, Resource.Lumber));
    }

    [Fact]
    public void Trading_a_resource_for_itself_is_rejected()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (Resource.Lumber, 4));

        var result = GameEngine.Apply(state, new MaritimeTrade(0, Resource.Lumber, Resource.Lumber));

        Assert.False(result.Success);
        Assert.Equal(GameError.CannotTradeTheSameResource, result.Error);
    }

    [Fact]
    public void The_bank_must_have_the_card_you_want()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (Resource.Lumber, 4));
        state.BankTake(Resource.Ore, state.Bank[Resource.Ore]);

        var result = GameEngine.Apply(state, new MaritimeTrade(0, Resource.Lumber, Resource.Ore));

        Assert.False(result.Success);
        Assert.Equal(GameError.BankCannotPay, result.Error);
    }

    // ── معامله بین بازیکنان ──────────────────────────────────────────────

    private static GameState WithOffer(int players = 3)
    {
        var state = Games.New(players);
        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (Resource.Lumber, 2));
        Games.Give(state, 1, (Resource.Ore, 1));

        var offer = GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Lumber] = 2 },
            new Dictionary<Resource, int> { [Resource.Ore] = 1 },
            []));

        Assert.True(offer.Success);
        return state;
    }

    [Fact]
    public void An_offer_goes_to_everyone_by_default()
    {
        var state = WithOffer();

        Assert.NotNull(state.PendingTrade);
        Assert.Equal([1, 2], state.PendingTrade!.Responses.Keys.Order());
    }

    [Fact]
    public void You_cannot_offer_what_you_do_not_have()
    {
        var state = Games.New(players: 3);
        Games.StartMainPhase(state, 0);

        var result = GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Lumber] = 2 },
            new Dictionary<Resource, int> { [Resource.Ore] = 1 },
            []));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotEnoughResources, result.Error);
    }

    [Fact]
    public void An_empty_offer_is_rejected()
    {
        var state = Games.New(players: 3);
        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (Resource.Lumber, 2));

        var result = GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Lumber] = 2 },
            new Dictionary<Resource, int>(),
            []));

        Assert.False(result.Success);
        Assert.Equal(GameError.EmptyTrade, result.Error);
    }

    [Fact]
    public void Only_the_player_on_turn_may_offer()
    {
        var state = Games.New(players: 3);
        Games.StartMainPhase(state, 0);
        Games.Give(state, 1, (Resource.Lumber, 2));

        var result = GameEngine.Apply(state, new ProposeTrade(
            1,
            new Dictionary<Resource, int> { [Resource.Lumber] = 2 },
            new Dictionary<Resource, int> { [Resource.Ore] = 1 },
            []));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotYourTurn, result.Error);
    }

    [Fact]
    public void Two_offers_cannot_be_on_the_table_at_once()
    {
        var state = WithOffer();

        var result = GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Lumber] = 1 },
            new Dictionary<Resource, int> { [Resource.Ore] = 1 },
            []));

        Assert.False(result.Success);
        Assert.Equal(GameError.TradeAlreadyOnTheTable, result.Error);
    }

    [Fact]
    public void Accepting_without_the_goods_is_rejected()
    {
        var state = WithOffer();

        var result = GameEngine.Apply(state, new RespondToTrade(2, true));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotEnoughResources, result.Error);
    }

    [Fact]
    public void A_player_outside_the_offer_cannot_answer()
    {
        var state = Games.New(players: 3);
        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (Resource.Lumber, 2));
        Games.Give(state, 1, (Resource.Ore, 1));

        GameEngine.Apply(state, new ProposeTrade(
            0,
            new Dictionary<Resource, int> { [Resource.Lumber] = 2 },
            new Dictionary<Resource, int> { [Resource.Ore] = 1 },
            [1]));

        var result = GameEngine.Apply(state, new RespondToTrade(2, true));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotInvitedToTrade, result.Error);
    }

    [Fact]
    public void Confirming_before_anyone_accepts_is_rejected()
    {
        var state = WithOffer();

        var result = GameEngine.Apply(state, new ConfirmTrade(0, 1));

        Assert.False(result.Success);
        Assert.Equal(GameError.PartnerDidNotAccept, result.Error);
    }

    [Fact]
    public void An_accepted_offer_swaps_the_cards()
    {
        var state = WithOffer();

        // پذیرش خودش معامله را می‌بندد؛ دیگر تأییدِ جداگانه‌ای لازم نیست.
        var result = GameEngine.Apply(state, new RespondToTrade(1, true));

        Assert.True(result.Success);
        Assert.Equal(0, state.Player(0)[Resource.Lumber]);
        Assert.Equal(1, state.Player(0)[Resource.Ore]);
        Assert.Equal(2, state.Player(1)[Resource.Lumber]);
        Assert.Equal(0, state.Player(1)[Resource.Ore]);
        Assert.Null(state.PendingTrade);
        Assert.Contains(result.Events, e => e is TradeCompleted);
    }

    [Fact]
    public void A_rejected_offer_cannot_be_confirmed()
    {
        var state = WithOffer();

        GameEngine.Apply(state, new RespondToTrade(1, false));
        var result = GameEngine.Apply(state, new ConfirmTrade(0, 1));

        Assert.False(result.Success);
        Assert.Equal(GameError.PartnerDidNotAccept, result.Error);
    }

    [Fact]
    public void Only_the_proposer_may_cancel()
    {
        var state = WithOffer();

        Assert.Equal(GameError.NotYourTrade, GameEngine.Apply(state, new CancelTrade(1)).Error);
        Assert.NotNull(state.PendingTrade);
    }

    [Fact]
    public void The_proposer_can_take_the_offer_back()
    {
        var state = WithOffer();

        var result = GameEngine.Apply(state, new CancelTrade(0));

        Assert.True(result.Success);
        Assert.Null(state.PendingTrade);
    }

    [Fact]
    public void Answering_when_there_is_no_offer_is_rejected()
    {
        var state = Games.New(players: 3);
        Games.StartMainPhase(state, 0);

        var result = GameEngine.Apply(state, new RespondToTrade(1, true));

        Assert.False(result.Success);
        Assert.Equal(GameError.NoTradeOnTheTable, result.Error);
    }

    [Fact]
    public void The_offer_leaves_the_table_when_the_turn_ends()
    {
        var state = WithOffer();

        GameEngine.Apply(state, new EndTurn(0));

        Assert.Null(state.PendingTrade);
    }

    /// <summary>
    /// دستِ پیشنهاددهنده هم بین پیشنهاد و پذیرش عوض می‌شود؛ لحظه‌ی پذیرش هر دو
    /// طرف دوباره سنجیده می‌شوند، وگرنه پذیرنده چیزی می‌گرفت که وجود ندارد.
    /// </summary>
    [Fact]
    public void A_proposer_who_lost_the_goods_cannot_be_accepted()
    {
        var state = WithOffer();

        state.Player(0).Remove(Resource.Lumber, 1);

        var result = GameEngine.Apply(state, new RespondToTrade(1, true));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotEnoughResources, result.Error);
        Assert.NotNull(state.PendingTrade);
    }
    // ── مهلت و پیشنهاد متقابل ───────────────────────────────────────────

    private static readonly DateTimeOffset Noon =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>پیشنهادی با مهلت، ساخته‌شده در ساعتِ ثابتِ بالا.</summary>
    private static GameState WithTimedOffer()
    {
        var state = Games.New(3);
        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (Resource.Lumber, 2));
        Games.Give(state, 1, (Resource.Ore, 1));

        Assert.True(GameEngine.Apply(
            state,
            new ProposeTrade(
                0,
                new Dictionary<Resource, int> { [Resource.Lumber] = 2 },
                new Dictionary<Resource, int> { [Resource.Ore] = 1 },
                []),
            Noon).Success);

        return state;
    }

    [Fact]
    public void An_offer_gets_a_deadline_from_the_game_options()
    {
        var state = WithTimedOffer();

        Assert.Equal(Noon.AddSeconds(state.Options.TradeWindowSeconds), state.PendingTrade!.ExpiresAt);
    }

    /// <summary>بی‌زمان یعنی بی‌مهلت — بازی‌های قدیمی نباید یک‌شبه منقضی شوند.</summary>
    [Fact]
    public void An_offer_made_without_a_clock_never_expires()
    {
        var state = WithOffer();

        Assert.Null(state.PendingTrade!.ExpiresAt);
        Assert.False(state.PendingTrade.HasExpired(Noon.AddYears(5)));
    }

    [Fact]
    public void Accepting_after_the_deadline_is_refused()
    {
        var state = WithTimedOffer();
        var late = Noon.AddSeconds(state.Options.TradeWindowSeconds + 1);

        var result = GameEngine.Apply(state, new RespondToTrade(1, true), late);

        Assert.Equal(GameError.TradeExpired, result.Error);
        Assert.Equal(2, state.Player(0)[Resource.Lumber]);
    }

    [Fact]
    public void Accepting_inside_the_window_still_works()
    {
        var state = WithTimedOffer();

        var result = GameEngine.Apply(state, new RespondToTrade(1, true), Noon.AddSeconds(29));

        Assert.True(result.Success);
        Assert.Null(state.PendingTrade);
    }

    /// <summary>مهلت باید از عکس وضعیت سالم بیرون بیاید، وگرنه با ری‌استارت گم می‌شود.</summary>
    [Fact]
    public void The_deadline_survives_a_snapshot()
    {
        var state = WithTimedOffer();

        var back = GameState.Restore(state.ToSnapshot());

        Assert.Equal(state.PendingTrade!.ExpiresAt, back.PendingTrade!.ExpiresAt);
    }

    /// <summary>
    /// دو نفر می‌پذیرند؛ اولی معامله را می‌بندد و دومی چیزی برای پذیرفتن
    /// نمی‌یابد. این همان قاعده‌ی «اولین پذیرشِ معتبر» است.
    /// </summary>
    [Fact]
    public void The_first_valid_accept_takes_the_trade()
    {
        var state = Games.New(3);
        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (Resource.Lumber, 2));
        Games.Give(state, 1, (Resource.Ore, 1));
        Games.Give(state, 2, (Resource.Ore, 1));

        Assert.True(GameEngine.Apply(
            state,
            new ProposeTrade(
                0,
                new Dictionary<Resource, int> { [Resource.Lumber] = 2 },
                new Dictionary<Resource, int> { [Resource.Ore] = 1 },
                []),
            Noon).Success);

        Assert.True(GameEngine.Apply(state, new RespondToTrade(1, true), Noon).Success);

        var second = GameEngine.Apply(state, new RespondToTrade(2, true), Noon);

        Assert.Equal(GameError.NoTradeOnTheTable, second.Error);
        Assert.Equal(2, state.Player(1)[Resource.Lumber]);
        Assert.Equal(1, state.Player(2)[Resource.Ore]);
    }

    [Fact]
    public void A_counter_replaces_the_offer_and_turns_it_around()
    {
        var state = WithTimedOffer();

        var result = GameEngine.Apply(
            state,
            new CounterTrade(
                1,
                new Dictionary<Resource, int> { [Resource.Ore] = 1 },
                new Dictionary<Resource, int> { [Resource.Lumber] = 1 }),
            Noon);

        Assert.True(result.Success);
        Assert.Contains(result.Events, e => e is TradeCountered);

        var offer = state.PendingTrade!;
        Assert.Equal(1, offer.Proposer);
        Assert.Equal([0], offer.Responses.Keys);
        Assert.Equal(1, offer.Give[Resource.Ore]);
        Assert.Equal(Noon.AddSeconds(state.Options.TradeWindowSeconds), offer.ExpiresAt);
    }

    [Fact]
    public void The_original_proposer_can_accept_a_counter()
    {
        var state = WithTimedOffer();

        Assert.True(GameEngine.Apply(
            state,
            new CounterTrade(
                1,
                new Dictionary<Resource, int> { [Resource.Ore] = 1 },
                new Dictionary<Resource, int> { [Resource.Lumber] = 1 }),
            Noon).Success);

        Assert.True(GameEngine.Apply(state, new RespondToTrade(0, true), Noon).Success);

        Assert.Equal(1, state.Player(0)[Resource.Ore]);
        Assert.Equal(1, state.Player(1)[Resource.Lumber]);
        Assert.Null(state.PendingTrade);
    }

    [Fact]
    public void Only_someone_the_offer_was_sent_to_can_counter()
    {
        var state = WithTimedOffer();

        var result = GameEngine.Apply(
            state,
            new CounterTrade(
                0,
                new Dictionary<Resource, int> { [Resource.Lumber] = 1 },
                new Dictionary<Resource, int> { [Resource.Ore] = 1 }),
            Noon);

        Assert.Equal(GameError.NotInvitedToTrade, result.Error);
    }

    [Fact]
    public void You_cannot_counter_with_cards_you_do_not_hold()
    {
        var state = WithTimedOffer();

        var result = GameEngine.Apply(
            state,
            new CounterTrade(
                1,
                new Dictionary<Resource, int> { [Resource.Ore] = 9 },
                new Dictionary<Resource, int> { [Resource.Lumber] = 1 }),
            Noon);

        Assert.Equal(GameError.NotEnoughResources, result.Error);
    }

    [Fact]
    public void Countering_after_the_deadline_is_refused()
    {
        var state = WithTimedOffer();

        var result = GameEngine.Apply(
            state,
            new CounterTrade(
                1,
                new Dictionary<Resource, int> { [Resource.Ore] = 1 },
                new Dictionary<Resource, int> { [Resource.Lumber] = 1 }),
            Noon.AddSeconds(31));

        Assert.Equal(GameError.TradeExpired, result.Error);
    }
}
