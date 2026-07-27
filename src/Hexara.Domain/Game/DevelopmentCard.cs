using Hexara.Domain.Common;

namespace Hexara.Domain.Game;

/// <summary>کارت‌های توسعه.</summary>
public enum DevelopmentCard
{
    Knight = 1,
    RoadBuilding = 2,
    YearOfPlenty = 3,
    Monopoly = 4,
    VictoryPoint = 5
}

/// <summary>ساخت و درهم‌ریزی دسته‌ی کارت توسعه.</summary>
public static class DevelopmentDeck
{
    /// <summary>ترکیب دسته‌ی کلاسیک ۲۵ کارتی.</summary>
    public static readonly (DevelopmentCard Card, int Count)[] ClassicComposition =
    [
        (DevelopmentCard.Knight, 14),
        (DevelopmentCard.VictoryPoint, 5),
        (DevelopmentCard.RoadBuilding, 2),
        (DevelopmentCard.YearOfPlenty, 2),
        (DevelopmentCard.Monopoly, 2)
    ];

    /// <summary>
    /// دسته‌ای متناسب با اندازه‌ی برد. برد کلاسیک ۱۹ خانه‌ای همان ۲۵ کارت را
    /// می‌گیرد و بردهای بزرگ‌تر به همان نسبت چند برابر می‌شوند.
    /// </summary>
    public static List<DevelopmentCard> Build(int tileCount, Rng rng)
    {
        var multiplier = Math.Max(1, (int)Math.Round(tileCount / 19.0, MidpointRounding.AwayFromZero));

        var deck = new List<DevelopmentCard>();
        foreach (var (card, count) in ClassicComposition)
        {
            deck.AddRange(Enumerable.Repeat(card, count * multiplier));
        }

        rng.Shuffle(deck);
        return deck;
    }
}
