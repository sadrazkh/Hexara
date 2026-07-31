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

    /// <summary>
    /// هزینه‌ی هر ساخت‌وساز.
    ///
    /// از سرور می‌آید تا کلاینت جدولِ هزینه را دوباره ننویسد؛ همان قاعده‌ای که
    /// برای نرخ بندرها هم برقرار است. ثابت است و با بازی عوض نمی‌شود، ولی
    /// فرستادنش ارزان‌تر از دو منبعِ حقیقت است.
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<Resource, int>> Costs { get; init; }

    /// <summary>
    /// چند جاده برای گرفتن «طولانی‌ترین جاده» لازم است.
    ///
    /// از تنظیمات بازی می‌آید نه از یک عددِ ثابت در کلاینت، چون قابل تغییر است و
    /// رابط باید بتواند بگوید «۲ تا مانده» — همان دلیلی که جدول هزینه را هم از
    /// سرور می‌گیریم.
    /// </summary>
    public required int LongestRoadMinimum { get; init; }

    /// <summary>چند شوالیه برای گرفتن «بزرگ‌ترین ارتش» لازم است.</summary>
    public required int LargestArmyMinimum { get; init; }

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

    /// <summary>تیم این بازیکن؛ تهی یعنی بازی انفرادی است.</summary>
    public int? Team { get; init; }

    public required int CardCount { get; init; }

    public required int DevelopmentCardCount { get; init; }

    public required int KnightsPlayed { get; init; }

    public required bool HasLongestRoad { get; init; }

    public required bool HasLargestArmy { get; init; }

    public required int LongestRoadLength { get; init; }

    /// <summary>
    /// بندرهایی که این بازیکن با آبادی‌هایش در اختیار دارد.
    ///
    /// عمومی است و باید باشد: بندرِ حریف روی برد پیداست و در معامله تعیین‌کننده
    /// است — دانستنِ اینکه او سنگ را ۲:۱ می‌دهد بخشی از خودِ بازی است.
    /// </summary>
    public required IReadOnlyList<PortSnapshot> Ports { get; init; }

    /// <summary>
    /// نرخ معامله‌ی این بازیکن با بانک: از هر منبع چند تا بدهد تا یکی بگیرد.
    ///
    /// سرور حسابش می‌کند نه کلاینت — نرخ به بندرها بستگی دارد (۲ با بندر
    /// اختصاصی، ۳ با عمومی، وگرنه ۴) و پیاده‌کردن دوباره‌ی همان قاعده یعنی دو جا
    /// که می‌توانند از هم بلغزند. کنارِ خودِ بازیکن می‌نشیند نه کنارِ «حرکت‌های
    /// قانونی»، چون یک واقعیتِ همیشگی است و نه چیزی که فقط سرِ نوبت معنا دارد.
    /// </summary>
    public required IReadOnlyDictionary<Resource, int> TradeRates { get; init; }

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

    /// <summary>
    /// امتیازی که برای پیروزی شمرده می‌شود. در بازی انفرادی همان بالایی است و در
    /// بازی تیمی مجموع تیم — از جمله کارت‌های پنهانِ هم‌تیمی، که در تیم مشترک است.
    /// </summary>
    public required int Score { get; init; }

    public required bool PlayedDevelopmentCardThisTurn { get; init; }

    /// <summary>اگر تاس ۷ آمده و این بازیکن باید کارت دور بریزد، تعدادش.</summary>
    public int MustDiscard { get; init; }
}

public sealed record TradeOfferView(
    int Proposer,
    IReadOnlyDictionary<Resource, int> Give,
    IReadOnlyDictionary<Resource, int> Take,
    IReadOnlyDictionary<int, TradeResponse> Responses,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// حرکت‌های قانونی بیننده در همین لحظه. اگر نوبتش نباشد همه خالی‌اند.
/// </summary>
public sealed record LegalMovesView
{
    public required bool IsMyTurn { get; init; }

    public required IReadOnlyList<VertexSnapshot> Settlements { get; init; }

    public required IReadOnlyList<RoadSnapshot> Roads { get; init; }

    public required IReadOnlyList<VertexSnapshot> Cities { get; init; }

    /// <summary>
    /// خانه‌هایی که دزد می‌تواند برود.
    ///
    /// هم برای مرحله‌ی دزد پر می‌شود و هم وقتی شوالیه قابل بازی است — چون
    /// شوالیه هم دزد را جابه‌جا می‌کند و همان فهرست را لازم دارد.
    /// </summary>
    public required IReadOnlyList<HexSnapshot> RobberTargets { get; init; }

    /// <summary>
    /// یال‌هایی که کارت جاده‌سازی می‌تواند رویشان جاده‌ی رایگان بگذارد.
    ///
    /// جدا از <see cref="Roads"/> است چون آن یکی «چه چیزی را می‌توانی
    /// *بخری*» را می‌گوید و این یکی «کجا را می‌توانی مجانی بگیری» — و این دو
    /// در مرحله‌ی تاس با هم فرق دارند.
    /// </summary>
    public required IReadOnlyList<RoadSnapshot> FreeRoads { get; init; }

    /// <summary>
    /// برای هر جاده‌ی رایگانِ اول، جاهایی که *بعد از* گذاشتنش برای جاده‌ی دوم باز
    /// می‌شوند. کلید همان «‎q,r,side‎» است.
    ///
    /// بدون این، بازیکن نمی‌توانست با کارت جاده‌سازی زنجیره بسازد: جاده‌ی دومِ
    /// چسبیده به اولی در فهرستِ <see cref="FreeRoads"/> نیست، چون آن فهرست پیش از
    /// گذاشتن اولی حساب شده. حساب‌کردنش در کلاینت یعنی قاعده‌ی اتصال جاده دو جا
    /// نوشته شود.
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<RoadSnapshot>> FollowUpRoads { get; init; }

    /// <summary>کارت‌های توسعه‌ای که همین حالا می‌شود بازی کرد.</summary>
    public required IReadOnlyList<DevelopmentCard> PlayableCards { get; init; }

    public static LegalMovesView None { get; } = new()
    {
        IsMyTurn = false,
        Settlements = [],
        Roads = [],
        Cities = [],
        RobberTargets = [],
        FreeRoads = [],
        FollowUpRoads = new Dictionary<string, IReadOnlyList<RoadSnapshot>>(),
        PlayableCards = []
    };
}
