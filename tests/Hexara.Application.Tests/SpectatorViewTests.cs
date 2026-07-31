using Hexara.Application.Common.Interfaces;
using Hexara.Application.Games;
using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Application.Tests;

/// <summary>
/// تماشاچی صندلی ندارد، و این باید یعنی «هیچ رازی».
///
/// خطر اینجا خاموش است: نمای تماشاچی از همان سازنده‌ای می‌آید که نمای بازیکن، و
/// یک شرطِ فراموش‌شده کافی است تا دستِ همه روی مرورگرِ یک غریبه بنشیند بی‌آنکه
/// هیچ‌چیز خراب به نظر برسد.
/// </summary>
public class SpectatorViewTests
{
    private static readonly Guid[] Users =
    [
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333")
    ];

    /// <summary>بازی‌ای در بدنه‌ی نوبت، با کارت و منبع در دستِ بازیکن اول.</summary>
    private static StoredGame Game()
    {
        var options = new GameOptions { PlayerCount = 3, Seed = 5 };
        var state = GameState.Create(options, Users);

        var snapshot = state.ToSnapshot() with
        {
            Phase = TurnPhase.Main,
            CurrentPlayer = 0,
            TurnNumber = 4
        };

        var players = snapshot.Players.ToList();
        players[0] = players[0] with
        {
            Resources = new Dictionary<Resource, int>
            {
                [Resource.Lumber] = 3,
                [Resource.Brick] = 2,
                [Resource.Wool] = 0,
                [Resource.Grain] = 0,
                [Resource.Ore] = 0
            },
            DevelopmentCards = new Dictionary<DevelopmentCard, int> { [DevelopmentCard.Knight] = 2 },
            VictoryPointCards = 1
        };

        return new StoredGame(
            Guid.NewGuid(),
            GameStatus.Active,
            Users,
            GameState.Restore(snapshot with { Players = players }));
    }

    private static GameViewBuilder Builder() => new(new FakeDirectory());

    [Fact]
    public async Task A_spectator_gets_no_hand_at_all()
    {
        var view = await Builder().BuildAsync(Game(), viewerSeat: null);

        Assert.Null(view.Seat);
        Assert.Null(view.Hand);
    }

    /// <summary>
    /// کارت پیروزیِ پنهان تا پایان بازی امتیازِ پنهان است. اگر تماشاچی امتیازِ
    /// واقعی را می‌دید، می‌شد از رویش فهمید چه کسی کارت پیروزی دارد.
    /// </summary>
    [Fact]
    public async Task Hidden_victory_points_stay_hidden_from_a_spectator()
    {
        var game = Game();

        var spectator = await Builder().BuildAsync(game, viewerSeat: null);
        var owner = await Builder().BuildAsync(game, 0);

        // صاحبِ کارت امتیازِ واقعی‌اش را می‌بیند و یک واحد بیشتر از عمومی است.
        Assert.Equal(owner.Players[0].PublicVictoryPoints + 1, owner.Hand!.VictoryPoints);

        // تماشاچی فقط همان عددِ عمومی را می‌بیند و اصلاً جایی برای عددِ واقعی ندارد.
        Assert.Equal(owner.Players[0].PublicVictoryPoints, spectator.Players[0].PublicVictoryPoints);
        Assert.Null(spectator.Hand);
    }

    /// <summary>
    /// شمارشِ کارت عمومی است (همه سرِ میز می‌بینند چند کارت در دست هر کس است)،
    /// ولی *کدام* کارت هرگز.
    /// </summary>
    [Fact]
    public async Task A_spectator_sees_counts_but_never_which_cards()
    {
        var view = await Builder().BuildAsync(Game(), viewerSeat: null);

        Assert.Equal(5, view.Players[0].CardCount);
        Assert.Equal(2, view.Players[0].DevelopmentCardCount);
        Assert.Null(view.Hand);
    }

    [Fact]
    public async Task A_spectator_has_no_legal_moves()
    {
        var view = await Builder().BuildAsync(Game(), viewerSeat: null);

        Assert.False(view.Legal.IsMyTurn);
        Assert.Empty(view.Legal.Settlements);
        Assert.Empty(view.Legal.Roads);
        Assert.Empty(view.Legal.Cities);
        Assert.Empty(view.Legal.PlayableCards);
        Assert.Empty(view.Legal.FreeRoads);
        Assert.Empty(view.Legal.FollowUpRoads);
    }

    /// <summary>برد و بانک و بندرها عمومی‌اند؛ تماشا بدون آن‌ها بی‌معناست.</summary>
    [Fact]
    public async Task A_spectator_still_sees_the_public_table()
    {
        var view = await Builder().BuildAsync(Game(), viewerSeat: null);

        Assert.Equal(19, view.Tiles.Count);
        Assert.Equal(9, view.Ports.Count);
        Assert.Equal(3, view.Players.Count);
        Assert.NotEmpty(view.Bank);
        Assert.Equal(TurnPhase.Main, view.Phase);
    }

    // ── رویدادها ─────────────────────────────────────────────────────────

    /// <summary>
    /// دزدی: دزد و قربانی می‌دانند چه کارتی رد و بدل شد. تماشاچی نباید بداند —
    /// وگرنه با چند دستِ تماشا می‌شود دستِ همه را بازسازی کرد.
    /// </summary>
    [Fact]
    public void A_spectator_never_learns_what_was_stolen()
    {
        var stolen = new ResourceStolen(0, 1, Resource.Ore);

        var forSpectator = GameEventRedactor.ForSeat(stolen, null);

        Assert.IsType<ResourceStolenSecretly>(forSpectator);
        Assert.IsType<ResourceStolen>(GameEventRedactor.ForSeat(stolen, 0));
        Assert.IsType<ResourceStolen>(GameEventRedactor.ForSeat(stolen, 1));
    }

    [Fact]
    public void A_spectator_never_learns_which_card_was_bought()
    {
        var bought = new DevelopmentCardBought(0, DevelopmentCard.Knight);

        Assert.IsType<DevelopmentCardBoughtSecretly>(GameEventRedactor.ForSeat(bought, null));
        Assert.IsType<DevelopmentCardBought>(GameEventRedactor.ForSeat(bought, 0));
    }

    private sealed class FakeDirectory : IPlayerDirectory
    {
        public Task<IReadOnlyList<PlayerProfile>> GetAsync(
            IReadOnlyList<Guid> userIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlayerProfile>>(
                [.. userIds.Select((id, i) => new PlayerProfile(id, $"Player {i}", "#000000", false))]);
    }
}
