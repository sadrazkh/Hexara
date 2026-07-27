using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Application.Games;

/// <summary>
/// وضعیت بازی از دید یک صندلی مشخص.
///
/// این تنها شکلی است که وضعیت به کلاینت می‌رسد: هر چیزی که این صندلی حق دیدنش را
/// ندارد اصلاً ساخته نمی‌شود. رکوردهای هندسی همان‌هایی هستند که در عکس وضعیت
/// استفاده می‌شوند تا سرور و کلاینت یک واژگان داشته باشند.
/// </summary>
public sealed record GameView
{
    public required Guid GameId { get; init; }

    public required long Version { get; init; }

    public required TurnPhase Phase { get; init; }

    public required int CurrentPlayer { get; init; }

    public required int TurnNumber { get; init; }

    public int? Winner { get; init; }

    public int? Die1 { get; init; }

    public int? Die2 { get; init; }

    /// <summary>آخرین باری که بازی جلو رفت — مبنای شمارش معکوس نوبت در کلاینت.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// چند ثانیه بعد از این، بات جای بازیکنِ معطل را می‌گیرد. صفر یعنی پوشش خودکار
    /// خاموش است و کلاینت نباید شمارش معکوس نشان دهد.
    /// </summary>
    public required int DeadlineSeconds { get; init; }

    /// <summary>مهلت کوتاه‌ترِ کسی که اتصالش قطع شده.</summary>
    public required int AbsentGraceSeconds { get; init; }

    public required HexSnapshot Robber { get; init; }

    public required IReadOnlyList<TileSnapshot> Tiles { get; init; }

    public required IReadOnlyList<PortSnapshot> Ports { get; init; }

    public required IReadOnlyList<BuildingSnapshot> Buildings { get; init; }

    public required IReadOnlyList<RoadSnapshot> Roads { get; init; }

    public required IReadOnlyDictionary<Resource, int> Bank { get; init; }

    public required int DevelopmentDeckCount { get; init; }

    public required IReadOnlyList<PlayerView> Players { get; init; }

    /// <summary>صندلی بیننده؛ تهی یعنی تماشاچی.</summary>
    public int? Seat { get; init; }

    /// <summary>دست خودِ بیننده — برای بقیه فقط تعداد فرستاده می‌شود.</summary>
    public HandView? Hand { get; init; }

    public required IReadOnlyDictionary<int, int> PendingDiscards { get; init; }

    public TradeOfferView? PendingTrade { get; init; }

    /// <summary>حرکت‌های قانونی بیننده — تا کلاینت مجبور نباشد قوانین را دوباره پیاده کند.</summary>
    public required LegalMovesView Legal { get; init; }
}

/// <summary>آنچه همه از یک بازیکن می‌بینند.</summary>
public sealed record PlayerView
{
    public required int Index { get; init; }

    public required Guid UserId { get; init; }

    public required string DisplayName { get; init; }

    public required string AvatarColor { get; init; }

    /// <summary>امتیاز بدون کارت‌های پیروزی پنهان.</summary>
    public required int PublicVictoryPoints { get; init; }

    public required int CardCount { get; init; }

    public required int DevelopmentCardCount { get; init; }

    public required int KnightsPlayed { get; init; }

    public required bool HasLongestRoad { get; init; }

    public required bool HasLargestArmy { get; init; }

    public required int LongestRoadLength { get; init; }

    public required int SettlementsLeft { get; init; }

    public required int CitiesLeft { get; init; }

    public required int RoadsLeft { get; init; }

    public required bool IsOnline { get; init; }
}

/// <summary>دست خصوصی بیننده.</summary>
public sealed record HandView
{
    public required IReadOnlyDictionary<Resource, int> Resources { get; init; }

    public required IReadOnlyDictionary<DevelopmentCard, int> DevelopmentCards { get; init; }

    /// <summary>کارت‌هایی که همین نوبت خریده شده‌اند و هنوز قابل بازی نیستند.</summary>
    public required IReadOnlyDictionary<DevelopmentCard, int> NewDevelopmentCards { get; init; }

    /// <summary>امتیاز واقعی، شامل کارت‌های پیروزی پنهان.</summary>
    public required int VictoryPoints { get; init; }

    public required bool PlayedDevelopmentCardThisTurn { get; init; }

    /// <summary>اگر تاس ۷ آمده و این بازیکن باید کارت دور بریزد، تعدادش.</summary>
    public int MustDiscard { get; init; }
}

public sealed record TradeOfferView(
    int Proposer,
    IReadOnlyDictionary<Resource, int> Give,
    IReadOnlyDictionary<Resource, int> Take,
    IReadOnlyDictionary<int, TradeResponse> Responses);

/// <summary>
/// حرکت‌های قانونی بیننده در همین لحظه. اگر نوبتش نباشد همه خالی‌اند.
/// </summary>
public sealed record LegalMovesView
{
    public required bool IsMyTurn { get; init; }

    public required IReadOnlyList<VertexSnapshot> Settlements { get; init; }

    public required IReadOnlyList<RoadSnapshot> Roads { get; init; }

    public required IReadOnlyList<VertexSnapshot> Cities { get; init; }

    /// <summary>خانه‌هایی که دزد می‌تواند برود.</summary>
    public required IReadOnlyList<HexSnapshot> RobberTargets { get; init; }

    public static LegalMovesView None { get; } = new()
    {
        IsMyTurn = false,
        Settlements = [],
        Roads = [],
        Cities = [],
        RobberTargets = []
    };
}
