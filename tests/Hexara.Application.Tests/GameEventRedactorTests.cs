using Hexara.Application.Games;
using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Application.Tests;

/// <summary>
/// این تست‌ها مرز اطلاعات پنهان را نگه می‌دارند. اگر روزی رویداد تازه‌ای اسرار
/// حمل کند و اینجا اضافه نشود، «هیچ رویدادی جز این دو تغییر نمی‌کند» شکست می‌خورد.
/// </summary>
public class GameEventRedactorTests
{
    [Fact]
    public void The_thief_sees_what_was_stolen()
    {
        var stolen = new ResourceStolen(0, 1, Resource.Ore);

        Assert.Same(stolen, GameEventRedactor.ForSeat(stolen, 0));
    }

    [Fact]
    public void The_victim_sees_what_was_stolen()
    {
        var stolen = new ResourceStolen(0, 1, Resource.Ore);

        Assert.Same(stolen, GameEventRedactor.ForSeat(stolen, 1));
    }

    [Fact]
    public void Everyone_else_only_sees_that_something_moved()
    {
        var redacted = GameEventRedactor.ForSeat(new ResourceStolen(0, 1, Resource.Ore), 2);

        var secret = Assert.IsType<ResourceStolenSecretly>(redacted);
        Assert.Equal(0, secret.PlayerIndex);
        Assert.Equal(1, secret.VictimIndex);
    }

    [Fact]
    public void A_spectator_never_sees_a_stolen_card()
    {
        Assert.IsType<ResourceStolenSecretly>(
            GameEventRedactor.ForSeat(new ResourceStolen(0, 1, Resource.Ore), null));
    }

    [Fact]
    public void Only_the_buyer_sees_which_card_was_bought()
    {
        var bought = new DevelopmentCardBought(1, DevelopmentCard.Knight);

        Assert.Same(bought, GameEventRedactor.ForSeat(bought, 1));
        Assert.IsType<DevelopmentCardBoughtSecretly>(GameEventRedactor.ForSeat(bought, 0));
        Assert.IsType<DevelopmentCardBoughtSecretly>(GameEventRedactor.ForSeat(bought, null));
    }

    [Fact]
    public void Public_events_pass_through_untouched()
    {
        GameEvent[] events =
        [
            new DiceRolled(0, 3, 4),
            new ResourcesProduced(
                [new ResourceGrant(1, Resource.Ore, 2)],
                [new ProductionSource(1, new Axial(1, 0), Resource.Ore)]),
            new RobberMoved(0, new Axial(0, 0), new Axial(1, 0)),
            new CardsDiscarded(1, new Dictionary<Resource, int> { [Resource.Wool] = 2 }),
            new GameWon(2, 10)
        ];

        foreach (var seat in new int?[] { 0, 1, 2, null })
        {
            Assert.Equal(events, GameEventRedactor.ForSeat(events, seat));
        }
    }

    /// <summary>
    /// هر رویدادی که در دامنه هست یا باید عمومی باشد یا صریحاً اینجا سانسور شود.
    /// این تست جلوی «اضافه کردم و یادم رفت» را می‌گیرد.
    /// </summary>
    [Fact]
    public void Only_the_two_known_events_are_secret()
    {
        var secret = typeof(GameEvent).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(GameEvent).IsAssignableFrom(t))
            .Where(t => t.Name is "ResourceStolen" or "DevelopmentCardBought")
            .Select(t => t.Name)
            .Order()
            .ToList();

        Assert.Equal(["DevelopmentCardBought", "ResourceStolen"], secret);

        Assert.True(GameEventRedactor.IsSecret(new ResourceStolen(0, 1, Resource.Ore)));
        Assert.True(GameEventRedactor.IsSecret(new DevelopmentCardBought(0, DevelopmentCard.Knight)));
        Assert.False(GameEventRedactor.IsSecret(new DiceRolled(0, 1, 1)));
    }

    [Fact]
    public void A_whole_list_is_redacted_at_once()
    {
        IReadOnlyList<GameEvent> events =
        [
            new RobberMoved(0, new Axial(0, 0), new Axial(1, 0)),
            new ResourceStolen(0, 1, Resource.Ore)
        ];

        var forOutsider = GameEventRedactor.ForSeat(events, 2);

        Assert.IsType<RobberMoved>(forOutsider[0]);
        Assert.IsType<ResourceStolenSecretly>(forOutsider[1]);
    }
}
