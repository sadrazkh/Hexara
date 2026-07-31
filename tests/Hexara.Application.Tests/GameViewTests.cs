using Hexara.Application.Common.Interfaces;
using Hexara.Application.Games;
using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Application.Tests;

public class GameViewTests
{
    private static readonly Guid[] Users =
    [
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333")
    ];

    private static StoredGame NewGame(Func<GameSnapshot, GameSnapshot>? tweak = null)
    {
        var options = new GameOptions { PlayerCount = 3, Seed = 5 };
        var state = GameState.Create(options, Users);

        if (tweak is not null)
        {
            state = GameState.Restore(tweak(state.ToSnapshot()));
        }

        return new StoredGame(Guid.NewGuid(), GameStatus.Active, Users, state);
    }

    private static GameViewBuilder NewBuilder() => new(new FakeDirectory());

    [Fact]
    public async Task The_view_carries_the_public_board()
    {
        var game = NewGame();

        var view = await NewBuilder().BuildAsync(game, 0);

        Assert.Equal(game.Id, view.GameId);
        Assert.Equal(19, view.Tiles.Count);
        Assert.Equal(9, view.Ports.Count);
        Assert.Equal(3, view.Players.Count);
        Assert.Equal(25, view.DevelopmentDeckCount);
        Assert.Equal(TurnPhase.SetupSettlement, view.Phase);
    }

    [Fact]
    public async Task Names_come_from_the_directory()
    {
        var view = await NewBuilder().BuildAsync(NewGame(), 0);

        Assert.Equal("Player 0", view.Players[0].DisplayName);
        Assert.Equal("#00000a", view.Players[0].AvatarColor);
        Assert.Equal("Player 2", view.Players[2].DisplayName);
    }

    /// <summary>مهم‌ترین خاصیت نما: دست بقیه فقط شمرده می‌شود، نه فاش.</summary>
    [Fact]
    public async Task Only_my_own_hand_is_spelled_out()
    {
        var game = NewGame(s => s with
        {
            Players =
            [
                s.Players[0] with { Resources = Hand(ore: 3) },
                s.Players[1] with { Resources = Hand(ore: 2, wool: 2) },
                s.Players[2]
            ]
        });

        var view = await NewBuilder().BuildAsync(game, 0);

        Assert.NotNull(view.Hand);
        Assert.Equal(3, view.Hand!.Resources[Resource.Ore]);
        Assert.Equal(4, view.Players[1].CardCount);

        // هیچ جای نما دست بازیکن دیگری تفکیک‌شده نیست.
        Assert.Equal(0, view.Seat);
        Assert.DoesNotContain(view.Players, p => p.Index == 1 && p.CardCount != 4);
    }

    [Fact]
    public async Task A_spectator_gets_no_hand_and_no_moves()
    {
        var view = await NewBuilder().BuildAsync(NewGame(), viewerSeat: null);

        Assert.Null(view.Seat);
        Assert.Null(view.Hand);
        Assert.False(view.Legal.IsMyTurn);
    }

    /// <summary>کارت پیروزی پنهان فقط در امتیاز خودِ صاحبش دیده می‌شود.</summary>
    [Fact]
    public async Task A_hidden_victory_card_stays_out_of_the_public_score()
    {
        var game = NewGame(s => s with
        {
            Players = [s.Players[0] with { BuildingPoints = 3, VictoryPointCards = 2 }, s.Players[1], s.Players[2]]
        });

        var mine = await NewBuilder().BuildAsync(game, 0);
        var theirs = await NewBuilder().BuildAsync(game, 1);

        Assert.Equal(5, mine.Hand!.VictoryPoints);
        Assert.Equal(3, mine.Players[0].PublicVictoryPoints);

        // حریف فقط امتیاز عمومی را می‌بیند و نمای بازیکن اصلاً جایی برای کارت پنهان ندارد.
        Assert.Equal(3, theirs.Players[0].PublicVictoryPoints);
        Assert.Equal(1, theirs.Seat);
    }

    [Fact]
    public async Task Legal_moves_are_only_for_the_player_on_turn()
    {
        var game = NewGame();

        var onTurn = await NewBuilder().BuildAsync(game, 0);
        var waiting = await NewBuilder().BuildAsync(game, 1);

        Assert.True(onTurn.Legal.IsMyTurn);
        Assert.NotEmpty(onTurn.Legal.Settlements);
        Assert.False(waiting.Legal.IsMyTurn);
        Assert.Empty(waiting.Legal.Settlements);
    }

    /// <summary>در چیدمان اولیه فقط ضلع‌های چسبیده به همان آبادی مجازند.</summary>
    [Fact]
    public async Task Setup_road_choices_hang_off_the_new_settlement()
    {
        var game = NewGame();
        var vertex = GameEngine.LegalSettlementVertices(game.State, 0).First();
        GameEngine.Apply(game.State, new PlaceInitialSettlement(0, vertex));

        var view = await NewBuilder().BuildAsync(game, 0);

        Assert.Equal(TurnPhase.SetupRoad, view.Phase);
        Assert.NotEmpty(view.Legal.Roads);
        Assert.All(
            view.Legal.Roads,
            r => Assert.Contains(vertex, EdgeId.Of(new Axial(r.Q, r.R), r.Side).Endpoints()));
    }

    /// <summary>در مرحله‌ی دور ریختن، نوبتِ کسی نیست ولی بدهکارها باید کاری بکنند.</summary>
    [Fact]
    public async Task A_player_who_owes_a_discard_gets_controls_out_of_turn()
    {
        var game = NewGame(s => s with
        {
            Phase = TurnPhase.Discard,
            CurrentPlayer = 0,
            TurnNumber = 4,
            PendingDiscards = new Dictionary<int, int> { [2] = 4 },
            Players =
            [
                s.Players[0],
                s.Players[1],
                s.Players[2] with { Resources = Hand(ore: 8) }
            ]
        });

        var view = await NewBuilder().BuildAsync(game, 2);

        Assert.True(view.Legal.IsMyTurn);
        Assert.Equal(4, view.Hand!.MustDiscard);
    }

    [Fact]
    public async Task Online_players_are_marked()
    {
        var view = await NewBuilder().BuildAsync(NewGame(), 0, new HashSet<Guid> { Users[0], Users[2] });

        Assert.True(view.Players[0].IsOnline);
        Assert.False(view.Players[1].IsOnline);
        Assert.True(view.Players[2].IsOnline);
    }

    [Fact]
    public async Task Robber_targets_show_up_only_in_the_robber_phase()
    {
        var game = NewGame(s => s with { Phase = TurnPhase.MoveRobber, CurrentPlayer = 1, TurnNumber = 3 });

        var view = await NewBuilder().BuildAsync(game, 1);

        Assert.Equal(18, view.Legal.RobberTargets.Count);
        Assert.DoesNotContain(view.Legal.RobberTargets, h => h.Q == view.Robber.Q && h.R == view.Robber.R);
    }

    [Fact]
    public async Task A_finished_game_offers_no_moves()
    {
        var game = NewGame(s => s with { Phase = TurnPhase.GameOver, Winner = 1 });

        var view = await NewBuilder().BuildAsync(game, 1);

        Assert.Equal(1, view.Winner);
        Assert.False(view.Legal.IsMyTurn);
    }

    // ── کارت‌های توسعه ───────────────────────────────────────────────────

    /// <summary>
    /// رابط از همین فهرست تصمیم می‌گیرد کدام کارت کلیک‌شدنی باشد، و کارتی که
    /// همین نوبت خریده شده نباید در آن باشد.
    /// </summary>
    [Fact]
    public async Task The_view_says_which_development_cards_are_playable()
    {
        var game = MainPhaseGame(s => s with
        {
            Players =
            [
                s.Players[0] with
                {
                    DevelopmentCards = new Dictionary<DevelopmentCard, int> { [DevelopmentCard.Monopoly] = 1 },
                    NewDevelopmentCards = new Dictionary<DevelopmentCard, int> { [DevelopmentCard.Knight] = 1 }
                },
                s.Players[1],
                s.Players[2]
            ]
        });

        var view = await NewBuilder().BuildAsync(game, 0);

        Assert.Equal([DevelopmentCard.Monopoly], view.Legal.PlayableCards);
    }

    /// <summary>
    /// شوالیه دزد را جابه‌جا می‌کند، پس خانه‌های هدف باید بیایند حتی وقتی مرحله
    /// «بردن دزد» نیست — وگرنه کارت زده می‌شود و برد جایی برای کلیک ندارد.
    /// </summary>
    [Fact]
    public async Task Robber_targets_come_along_when_the_knight_is_playable()
    {
        var game = MainPhaseGame(s => s with
        {
            Players =
            [
                s.Players[0] with
                {
                    DevelopmentCards = new Dictionary<DevelopmentCard, int> { [DevelopmentCard.Knight] = 1 }
                },
                s.Players[1],
                s.Players[2]
            ]
        });

        var view = await NewBuilder().BuildAsync(game, 0);

        Assert.Equal(TurnPhase.Main, view.Phase);
        Assert.Equal(18, view.Legal.RobberTargets.Count);
    }

    /// <summary>
    /// جاده‌ی رایگان با جاده‌ی خریدنی یکی نیست: در مرحله‌ی تاس اصلاً نمی‌شود جاده
    /// خرید، ولی کارت جاده‌سازی همان‌جا هم بازی می‌شود.
    /// </summary>
    [Fact]
    public async Task Free_roads_are_offered_even_in_the_roll_phase()
    {
        var game = NewGame(s => s with
        {
            Phase = TurnPhase.Roll,
            CurrentPlayer = 0,
            TurnNumber = 4,
            Roads = [new RoadSnapshot(0, 0, 0, 0)],
            Players =
            [
                s.Players[0] with
                {
                    DevelopmentCards = new Dictionary<DevelopmentCard, int> { [DevelopmentCard.RoadBuilding] = 1 }
                },
                s.Players[1],
                s.Players[2]
            ]
        });

        var view = await NewBuilder().BuildAsync(game, 0);

        Assert.Empty(view.Legal.Roads);
        Assert.NotEmpty(view.Legal.FreeRoads);
    }

    /// <summary>
    /// جاده‌ی دوم روی وضعیتِ بعد از اولی سنجیده می‌شود، پس نما باید برای هر
    /// انتخابِ اول جاهای تازه را هم بدهد — وگرنه زنجیره‌ساختن ناممکن است.
    /// </summary>
    [Fact]
    public async Task Each_first_free_road_carries_the_spots_it_opens()
    {
        var game = MainPhaseGame(s => s with
        {
            Roads = [new RoadSnapshot(0, 0, 0, 0)],
            Players =
            [
                s.Players[0] with
                {
                    DevelopmentCards = new Dictionary<DevelopmentCard, int> { [DevelopmentCard.RoadBuilding] = 1 }
                },
                s.Players[1],
                s.Players[2]
            ]
        });

        var view = await NewBuilder().BuildAsync(game, 0);

        var free = view.Legal.FreeRoads.Select(Key).ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(free);
        Assert.Equal(free, view.Legal.FollowUpRoads.Keys.ToHashSet(StringComparer.Ordinal));

        // دست‌کم یک انتخابِ اول باید دری باز کند که در فهرستِ اولیه نبود.
        Assert.Contains(
            view.Legal.FollowUpRoads,
            entry => entry.Value.Any(r => !free.Contains(Key(r))));
    }

    [Fact]
    public async Task Without_the_card_no_free_roads_are_computed()
    {
        var view = await NewBuilder().BuildAsync(MainPhaseGame(), 0);

        Assert.Empty(view.Legal.FreeRoads);
        Assert.Empty(view.Legal.FollowUpRoads);
    }

    // ── بندرها و نشان‌ها ─────────────────────────────────────────────────

    /// <summary>
    /// بندرِ حریف روی برد پیداست و در معامله تعیین‌کننده است، پس عمومی فرستاده
    /// می‌شود — نه فقط برای بیننده.
    /// </summary>
    [Fact]
    public async Task Each_player_carries_the_ports_they_hold()
    {
        var port = NewGame().State.Board.Ports.First(p => !p.IsGeneric);
        var vertex = port.Vertices().First();

        var view = await NewBuilder().BuildAsync(WithSettlement(1, vertex), 0);

        Assert.Empty(view.Players[0].Ports);
        Assert.Equal(
            [new PortSnapshot(port.Edge.Hex.Q, port.Edge.Hex.R, port.Edge.Side, port.Resource)],
            view.Players[1].Ports);
    }

    [Fact]
    public async Task A_dedicated_port_lowers_only_its_own_resource()
    {
        var port = NewGame().State.Board.Ports.First(p => !p.IsGeneric);
        var resource = port.Resource!.Value;

        var view = await NewBuilder().BuildAsync(WithSettlement(0, port.Vertices().First()), 0);

        var rates = view.Players[0].TradeRates;

        Assert.Equal(2, rates[resource]);
        Assert.All(
            rates.Where(r => r.Key != resource),
            r => Assert.Equal(4, r.Value));
    }

    /// <summary>
    /// نرخ یک واقعیتِ همیشگی است نه یک «حرکت قانونی». وقتی کنارِ حرکت‌ها بود،
    /// بیرون از نوبت خالی می‌رسید و رابط ۴:۱ نشان می‌داد — حتی به کسی که بندر داشت.
    /// </summary>
    [Fact]
    public async Task Rates_are_there_even_when_it_is_not_your_turn()
    {
        var port = NewGame().State.Board.Ports.First(p => p.IsGeneric);

        var view = await NewBuilder().BuildAsync(WithSettlement(2, port.Vertices().First()), 2);

        Assert.False(view.Legal.IsMyTurn);
        Assert.All(view.Players[2].TradeRates, r => Assert.Equal(3, r.Value));
    }

    [Fact]
    public async Task Someone_without_a_port_trades_at_four_to_one()
    {
        var view = await NewBuilder().BuildAsync(NewGame(), 0);

        Assert.Empty(view.Players[0].Ports);
        Assert.All(view.Players[0].TradeRates, r => Assert.Equal(4, r.Value));
    }

    /// <summary>حدِ نصاب‌ها قابل تنظیم‌اند، پس رابط نباید عددشان را از بر باشد.</summary>
    [Fact]
    public async Task The_view_carries_the_award_thresholds()
    {
        var view = await NewBuilder().BuildAsync(NewGame(), 0);

        Assert.Equal(5, view.LongestRoadMinimum);
        Assert.Equal(3, view.LargestArmyMinimum);
    }

    /// <summary>
    /// بازی‌ای با یک آبادی روی همان گوشه.
    ///
    /// برد از seed ثابت ساخته می‌شود، پس بندری که آزمون از یک نمونه‌ی دیگر پیدا
    /// کرده در این یکی هم دقیقاً همان‌جاست.
    /// </summary>
    private static StoredGame WithSettlement(int seat, VertexId vertex) =>
        NewGame(s => s with
        {
            Buildings =
            [
                new BuildingSnapshot(vertex.Hex.Q, vertex.Hex.R, vertex.Corner, seat, BuildingKind.Settlement)
            ]
        });

    // ── قربانی‌های دزد ───────────────────────────────────────────────────

    /// <summary>
    /// نما باید برای *هر* خانه‌ی هدف بگوید از چه کسی می‌شود دزدید.
    ///
    /// بی این، کلاینت قاعده را خودش می‌ساخت و فقط دو شرط از چهار شرط را می‌دانست.
    /// </summary>
    [Fact]
    public async Task Every_robber_target_carries_its_victims()
    {
        var game = NewGame(s => s with { Phase = TurnPhase.MoveRobber, CurrentPlayer = 0, TurnNumber = 4 });

        var view = await NewBuilder().BuildAsync(game, 0);

        Assert.NotEmpty(view.Legal.RobberTargets);
        Assert.Equal(
            view.Legal.RobberTargets.Select(t => $"{t.Q},{t.R}").ToHashSet(StringComparer.Ordinal),
            view.Legal.RobberVictims.Keys.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// **این همان حالتی است که دزد را قفل می‌کرد.**
    ///
    /// هم‌تیمی قربانی نیست. کلاینت این را نمی‌دانست، پس دکمه‌ی هم‌تیمی را نشان
    /// می‌داد، سرور ردش می‌کرد، و چون فهرستِ کلاینت خالی نبود دکمه‌ی «بی‌قربانی»
    /// هم نمی‌آمد — هیچ کلیکی حرکت را تمام نمی‌کرد.
    ///
    /// همان سناریو دو بار ساخته می‌شود، با تیم و بی تیم؛ وگرنه معلوم نبود که
    /// نبودنِ صندلی ۲ واقعاً از قاعده‌ی تیم آمده یا از جای دیگری.
    /// </summary>
    [Fact]
    public async Task A_teammate_is_never_offered_as_a_victim()
    {
        var soloVictims = await VictimsAsync(withTeams: false);
        var teamVictims = await VictimsAsync(withTeams: true);

        // بی تیم، صندلی ۲ قربانیِ معتبری است…
        Assert.Contains(soloVictims, seats => seats.Contains(2));

        // …و با تیم (تقسیم یک‌درمیان: ۰ و ۲ هم‌تیمی‌اند) هیچ‌جا نیست.
        Assert.All(teamVictims, seats => Assert.DoesNotContain(2, seats));
    }

    /// <summary>یک صندلیِ کارت‌دار کنار خانه‌ی مرکزی، با یا بی تیم‌بندی.</summary>
    private static async Task<IReadOnlyList<IReadOnlyList<int>>> VictimsAsync(bool withTeams)
    {
        var vertex = VertexId.Of(new Axial(0, 0), 0);

        var game = NewGame(s => s with
        {
            Phase = TurnPhase.MoveRobber,
            CurrentPlayer = 0,
            TurnNumber = 4,
            Options = s.Options with { Teams = withTeams ? TeamAssignment.Alternating(4) : null },
            Buildings =
            [
                new BuildingSnapshot(vertex.Hex.Q, vertex.Hex.R, vertex.Corner, 2, BuildingKind.Settlement)
            ],
            Players =
            [
                s.Players[0],
                s.Players[1],
                s.Players[2] with { Resources = Hand(ore: 5) }
            ]
        });

        var view = await NewBuilder().BuildAsync(game, 0);

        return [.. view.Legal.RobberVictims.Values];
    }

    /// <summary>خودت هرگز قربانیِ خودت نیستی.</summary>
    [Fact]
    public async Task You_are_never_your_own_victim()
    {
        var game = NewGame(s => s with { Phase = TurnPhase.MoveRobber, CurrentPlayer = 0, TurnNumber = 4 });

        var view = await NewBuilder().BuildAsync(game, 0);

        Assert.All(view.Legal.RobberVictims.Values, seats => Assert.DoesNotContain(0, seats));
    }

    private static string Key(RoadSnapshot road) => $"{road.Q},{road.R},{road.Side}";

    /// <summary>بازی‌ای در بدنه‌ی نوبتِ بازیکن اول، بدون چیدمان اولیه.</summary>
    private static StoredGame MainPhaseGame(Func<GameSnapshot, GameSnapshot>? tweak = null) =>
        NewGame(s =>
        {
            var main = s with { Phase = TurnPhase.Main, CurrentPlayer = 0, TurnNumber = 4 };
            return tweak?.Invoke(main) ?? main;
        });

    private static Dictionary<Resource, int> Hand(int ore = 0, int wool = 0) => new()
    {
        [Resource.Lumber] = 0,
        [Resource.Brick] = 0,
        [Resource.Wool] = wool,
        [Resource.Grain] = 0,
        [Resource.Ore] = ore
    };

    private sealed class FakeDirectory : IPlayerDirectory
    {
        public Task<IReadOnlyList<PlayerProfile>> GetAsync(
            IReadOnlyList<Guid> userIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlayerProfile>>(
                [.. userIds.Select((id, i) => new PlayerProfile(id, $"Player {i}", $"#00000{(char)('a' + i)}", false))]);
    }
}
