using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Domain.Tests.Game;

public class DevelopmentCardTests
{
    private static readonly Axial Center = new(0, 0);

    [Fact]
    public void Classic_deck_has_twenty_five_cards()
    {
        var state = Games.New(players: 3);
        Assert.Equal(25, state.DevelopmentDeckCount);
    }

    [Fact]
    public void Buying_costs_ore_wool_and_grain()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.Give(state, 0, (Resource.Ore, 1), (Resource.Wool, 1), (Resource.Grain, 1));

        var result = GameEngine.Apply(state, new BuyDevelopmentCard(0));

        Assert.True(result.Success);
        Assert.Equal(0, state.Player(0).TotalCards);
        Assert.Equal(24, state.DevelopmentDeckCount);
        Assert.Equal(1, state.Player(0).TotalDevelopmentCards);
        Assert.Contains(result.Events, e => e is DevelopmentCardBought);
    }

    [Fact]
    public void Buying_without_the_resources_is_rejected()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);

        var result = GameEngine.Apply(state, new BuyDevelopmentCard(0));

        Assert.False(result.Success);
        Assert.Equal(GameError.NotEnoughResources, result.Error);
    }

    /// <summary>کارتی که همین نوبت خریده شده تا نوبت بعد قابل بازی نیست.</summary>
    [Fact]
    public void A_card_bought_this_turn_cannot_be_played()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        state.Player(0).AddNewDevelopmentCard(DevelopmentCard.Monopoly);

        var result = GameEngine.Apply(state, new PlayMonopoly(0, Resource.Ore));

        Assert.False(result.Success);
        Assert.Equal(GameError.CardBoughtThisTurn, result.Error);
    }

    [Fact]
    public void Cards_become_playable_after_the_turn_ends()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        state.Player(0).AddNewDevelopmentCard(DevelopmentCard.Monopoly);

        GameEngine.Apply(state, new EndTurn(0));

        Assert.Equal(1, state.Player(0)[DevelopmentCard.Monopoly]);
        Assert.Empty(state.Player(0).NewDevelopmentCards);
    }

    [Fact]
    public void Playing_a_card_you_do_not_own_is_rejected()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);

        var result = GameEngine.Apply(state, new PlayMonopoly(0, Resource.Ore));

        Assert.False(result.Success);
        Assert.Equal(GameError.NoSuchDevelopmentCard, result.Error);
    }

    [Fact]
    public void Only_one_development_card_per_turn()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.Monopoly, 2);

        Assert.True(GameEngine.Apply(state, new PlayMonopoly(0, Resource.Ore)).Success);
        var second = GameEngine.Apply(state, new PlayMonopoly(0, Resource.Wool));

        Assert.False(second.Success);
        Assert.Equal(GameError.AlreadyPlayedADevelopmentCard, second.Error);
    }

    [Fact]
    public void The_limit_resets_next_turn()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.Monopoly, 2);

        GameEngine.Apply(state, new PlayMonopoly(0, Resource.Ore));
        GameEngine.Apply(state, new EndTurn(0));

        Assert.False(state.Player(0).PlayedDevelopmentCardThisTurn);
    }

    /// <summary>کارت توسعه را می‌توان قبل از تاس هم بازی کرد.</summary>
    [Fact]
    public void A_knight_may_be_played_before_the_roll()
    {
        var state = Games.New(players: 3);
        Games.RunSetup(state);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.Knight);

        var result = GameEngine.Apply(state, new PlayKnight(0, Games.EmptyHex(state), null));

        Assert.True(result.Success);
        Assert.Equal(TurnPhase.Roll, state.Phase);
        Assert.Equal(1, state.Player(0).KnightsPlayed);
    }

    [Fact]
    public void No_development_card_while_the_robber_is_pending()
    {
        var state = Games.SetupWithNextRoll(7);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.Monopoly);
        GameEngine.Apply(state, new RollDice(0));

        var result = GameEngine.Apply(state, new PlayMonopoly(0, Resource.Ore));

        Assert.False(result.Success);
        Assert.Equal(GameError.WrongPhase, result.Error);
    }

    [Fact]
    public void A_victory_point_card_is_never_played()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.VictoryPoint);

        var result = GameEngine.Apply(state, new PlayKnight(0, Games.EmptyHex(state), null));

        Assert.False(result.Success);
        Assert.Equal(GameError.NoSuchDevelopmentCard, result.Error);
    }

    /// <summary>کارت امتیاز از همان لحظه‌ی خرید حساب می‌شود و می‌تواند بازی را تمام کند.</summary>
    [Fact]
    public void A_bought_victory_point_card_can_win_the_game()
    {
        var state = Games.New(players: 2, tweak: o => o with { VictoryPoints = 3 });
        Games.StartMainPhase(state, 0);
        state.Player(0).BuildingPoints = 2;

        // دسته را تا اولین کارت امتیاز جلو می‌بریم تا خرید بعدی حتماً کارت امتیاز باشد.
        while (state.PeekDevelopmentCard() != DevelopmentCard.VictoryPoint)
        {
            state.DrawDevelopmentCard();
        }

        Games.Give(state, 0, (Resource.Ore, 1), (Resource.Wool, 1), (Resource.Grain, 1));
        var result = GameEngine.Apply(state, new BuyDevelopmentCard(0));

        Assert.True(result.Success);
        Assert.Equal(1, state.Player(0).VictoryPointCards);
        Assert.Equal(3, state.Player(0).VictoryPoints);
        Assert.Equal(2, state.Player(0).PublicVictoryPoints);
        Assert.Contains(result.Events, e => e is GameWon);
    }

    [Fact]
    public void Buying_from_an_empty_deck_is_rejected()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        while (state.DevelopmentDeckCount > 0)
        {
            state.DrawDevelopmentCard();
        }

        Games.Give(state, 0, (Resource.Ore, 1), (Resource.Wool, 1), (Resource.Grain, 1));
        var result = GameEngine.Apply(state, new BuyDevelopmentCard(0));

        Assert.False(result.Success);
        Assert.Equal(GameError.DevelopmentDeckEmpty, result.Error);
    }

    // ── سال فراوانی ──────────────────────────────────────────────────────

    [Fact]
    public void Year_of_plenty_takes_two_cards_from_the_bank()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.YearOfPlenty);

        var bankOre = state.Bank[Resource.Ore];
        var result = GameEngine.Apply(state, new PlayYearOfPlenty(0, Resource.Ore, Resource.Brick));

        Assert.True(result.Success);
        Assert.Equal(1, state.Player(0)[Resource.Ore]);
        Assert.Equal(1, state.Player(0)[Resource.Brick]);
        Assert.Equal(bankOre - 1, state.Bank[Resource.Ore]);
    }

    [Fact]
    public void Year_of_plenty_can_take_two_of_the_same_resource()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.YearOfPlenty);

        GameEngine.Apply(state, new PlayYearOfPlenty(0, Resource.Ore, Resource.Ore));

        Assert.Equal(2, state.Player(0)[Resource.Ore]);
    }

    [Fact]
    public void Year_of_plenty_needs_the_bank_to_have_both()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.YearOfPlenty);
        state.BankTake(Resource.Ore, state.Bank[Resource.Ore] - 1);

        var result = GameEngine.Apply(state, new PlayYearOfPlenty(0, Resource.Ore, Resource.Ore));

        Assert.False(result.Success);
        Assert.Equal(GameError.BankCannotPay, result.Error);
        Assert.Equal(1, state.Player(0)[DevelopmentCard.YearOfPlenty]);
    }

    // ── انحصار ───────────────────────────────────────────────────────────

    [Fact]
    public void Monopoly_collects_the_resource_from_everyone()
    {
        var state = Games.New(players: 3);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.Monopoly);
        Games.Give(state, 1, (Resource.Wool, 3), (Resource.Ore, 2));
        Games.Give(state, 2, (Resource.Wool, 1));

        var result = GameEngine.Apply(state, new PlayMonopoly(0, Resource.Wool));

        Assert.True(result.Success);
        Assert.Equal(4, state.Player(0)[Resource.Wool]);
        Assert.Equal(0, state.Player(1)[Resource.Wool]);
        Assert.Equal(2, state.Player(1)[Resource.Ore]);
        Assert.Equal(4, result.Events.OfType<MonopolyPlayed>().Single().Collected);
    }

    [Fact]
    public void Monopoly_on_a_resource_nobody_has_collects_nothing()
    {
        var state = Games.New(players: 3);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.Monopoly);

        var result = GameEngine.Apply(state, new PlayMonopoly(0, Resource.Wool));

        Assert.True(result.Success);
        Assert.Equal(0, result.Events.OfType<MonopolyPlayed>().Single().Collected);
    }

    // ── جاده‌سازی ────────────────────────────────────────────────────────

    [Fact]
    public void Road_building_places_two_free_roads()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(EdgeId.Of(Center, 0), 0);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.RoadBuilding);

        var first = EdgeId.Of(Center, 1);
        var second = EdgeId.Of(Center, 2);
        var result = GameEngine.Apply(state, new PlayRoadBuilding(0, first, second));

        Assert.True(result.Success);
        Assert.Equal(0, state.RoadAt(first));
        Assert.Equal(0, state.RoadAt(second));
        Assert.Equal(0, state.Player(0).TotalCards);
        Assert.Equal(13, state.Player(0).RoadsLeft);
    }

    /// <summary>جاده‌ی دوم می‌تواند فقط به لطف جاده‌ی اول قانونی باشد.</summary>
    [Fact]
    public void The_second_road_may_lean_on_the_first()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(EdgeId.Of(Center, 0), 0);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.RoadBuilding);

        // ضلع سوم فقط وقتی وصل است که ضلع دوم گذاشته شده باشد.
        var result = GameEngine.Apply(state, new PlayRoadBuilding(0, EdgeId.Of(Center, 1), EdgeId.Of(Center, 2)));

        Assert.True(result.Success);
    }

    [Fact]
    public void An_illegal_second_road_rolls_the_whole_move_back()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(EdgeId.Of(Center, 0), 0);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.RoadBuilding);

        var far = state.Board.Edges.First(e =>
            state.RoadAt(e) is null && !e.Endpoints().Any(v => v.TouchingEdges().Any(x => state.RoadAt(x) == 0)));

        var result = GameEngine.Apply(state, new PlayRoadBuilding(0, EdgeId.Of(Center, 1), far));

        Assert.False(result.Success);
        Assert.Equal(GameError.RoadNotConnected, result.Error);
        Assert.Null(state.RoadAt(EdgeId.Of(Center, 1)));
        Assert.Equal(15, state.Player(0).RoadsLeft);
        Assert.Equal(1, state.Player(0)[DevelopmentCard.RoadBuilding]);
    }

    [Fact]
    public void Road_building_with_a_single_road_is_allowed()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(EdgeId.Of(Center, 0), 0);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.RoadBuilding);

        var result = GameEngine.Apply(state, new PlayRoadBuilding(0, EdgeId.Of(Center, 1), null));

        Assert.True(result.Success);
        Assert.Equal(14, state.Player(0).RoadsLeft);
    }

    // ── فهرستی که رابط از آن می‌خواند ────────────────────────────────────

    /// <summary>
    /// رابط از همین فهرست تصمیم می‌گیرد کدام کارت کلیک‌شدنی باشد، پس اگر با
    /// خودِ اعتبارسنجی یکی نباشد کاربر کارتی می‌بیند که سرور ردش می‌کند.
    /// </summary>
    [Fact]
    public void Playable_cards_match_what_the_engine_accepts()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.Monopoly);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.YearOfPlenty);

        var playable = GameEngine.PlayableDevelopmentCards(state, 0);

        Assert.Equal(
            [DevelopmentCard.Monopoly, DevelopmentCard.YearOfPlenty],
            playable.OrderBy(c => c.ToString(), StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void A_victory_point_card_is_never_playable()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.VictoryPoint);

        Assert.Empty(GameEngine.PlayableDevelopmentCards(state, 0));
    }

    [Fact]
    public void A_card_bought_this_turn_is_not_in_the_playable_list()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        state.Player(0).AddNewDevelopmentCard(DevelopmentCard.Knight);

        Assert.Empty(GameEngine.PlayableDevelopmentCards(state, 0));
    }

    /// <summary>شوالیه پیش از تاس هم بازی می‌شود — قاعده‌ی آشنای بازی.</summary>
    [Fact]
    public void Cards_are_playable_before_rolling()
    {
        var state = Games.New(players: 2);
        state.Phase = TurnPhase.Roll;
        state.CurrentPlayer = 0;
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.Knight);

        Assert.Contains(DevelopmentCard.Knight, GameEngine.PlayableDevelopmentCards(state, 0));
    }

    [Fact]
    public void Nothing_is_playable_after_one_card_this_turn()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.Monopoly);
        Games.GiveDevelopmentCard(state, 0, DevelopmentCard.YearOfPlenty);

        Assert.True(GameEngine.Apply(state, new PlayMonopoly(0, Resource.Ore)).Success);

        Assert.Empty(GameEngine.PlayableDevelopmentCards(state, 0));
    }

    [Fact]
    public void Another_players_cards_are_not_playable_on_your_turn()
    {
        var state = Games.New(players: 2);
        Games.StartMainPhase(state, 0);
        Games.GiveDevelopmentCard(state, 1, DevelopmentCard.Monopoly);

        Assert.Empty(GameEngine.PlayableDevelopmentCards(state, 1));
    }

    // ── جاده‌ی دومِ کارت جاده‌سازی ────────────────────────────────────────

    /// <summary>
    /// جاده‌ی دوم روی وضعیتِ بعد از اولی سنجیده می‌شود. اگر رابط فقط فهرستِ
    /// «الان قانونی» را روشن کند، زنجیره‌ساختن با این کارت ممکن نیست.
    /// </summary>
    [Fact]
    public void Placing_a_road_opens_spots_the_current_list_does_not_have()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(EdgeId.Of(Center, 0), 0);

        var first = EdgeId.Of(Center, 1);
        var before = GameEngine.LegalRoadEdges(state, 0).ToHashSet();
        var opened = GameEngine.RoadsOpenedBy(state, 0, first);

        Assert.Contains(first, before);
        Assert.Contains(opened, e => !before.Contains(e));
    }

    /// <summary>
    /// این تست نگهبانِ یک میان‌بر است.
    ///
    /// ‎RoadsOpenedBy‎ فقط یال‌های همسایه‌ی جاده‌ی گذاشته‌شده را می‌پرسد نه کلِ برد،
    /// چون قانونی‌بودنِ یک یال فقط به دو سرِ خودش نگاه می‌کند. اینجا همان ادعا برای
    /// *هر* انتخابِ ممکن با پیمایشِ کاملِ برد سنجیده می‌شود؛ اگر روزی قاعده‌ی اتصال
    /// جاده عوض شود و دیگر محلی نباشد، همین‌جا لو می‌رود.
    /// </summary>
    [Fact]
    public void The_neighbour_only_shortcut_agrees_with_scanning_the_whole_board()
    {
        var state = Games.New(players: 3);
        Games.RunSetup(state);

        var candidates = GameEngine.LegalRoadEdges(state, 0).ToList();
        Assert.NotEmpty(candidates);

        foreach (var first in candidates)
        {
            var before = GameEngine.LegalRoadEdges(state, 0).ToHashSet();

            // مرجع: کلِ برد را با همان قاعده می‌پیماییم.
            state.PlaceRoad(first, 0);
            var byBruteForce = GameEngine.LegalRoadEdges(state, 0)
                .Where(e => !before.Contains(e))
                .ToHashSet();
            state.RemoveRoad(first);

            var byShortcut = GameEngine.RoadsOpenedBy(state, 0, first).ToHashSet();

            Assert.Equal(byBruteForce, byShortcut);
        }
    }

    /// <summary>
    /// حساب‌کردنِ جاده‌ی دوم نباید ردی بگذارد؛ این تابع سرِ ساختنِ *نما* صدا زده
    /// می‌شود، جایی که هیچ‌چیز نباید عوض شود.
    /// </summary>
    [Fact]
    public void Looking_ahead_leaves_the_game_untouched()
    {
        var state = Games.New(players: 2);
        state.PlaceRoad(EdgeId.Of(Center, 0), 0);

        var roads = state.Roads.ToDictionary(r => r.Key, r => r.Value);
        var version = state.Version;

        GameEngine.RoadsOpenedBy(state, 0, EdgeId.Of(Center, 1));

        Assert.Equal(roads, state.Roads);
        Assert.Equal(version, state.Version);
    }

    [Fact]
    public void Looking_ahead_past_an_occupied_edge_gives_nothing()
    {
        var state = Games.New(players: 2);
        var taken = EdgeId.Of(Center, 0);
        state.PlaceRoad(taken, 1);

        Assert.Empty(GameEngine.RoadsOpenedBy(state, 0, taken));
        Assert.Equal(1, state.RoadAt(taken));
    }
}
