using System.Text.Json.Serialization;
using Hexara.Domain.Game;

namespace Hexara.Application.Rooms;

public enum RoomStatus
{
    /// <summary>باز برای پیوستن.</summary>
    Open = 0,

    /// <summary>بازی شروع شده؛ اتاق فقط راهِ رسیدن به بازی است.</summary>
    Started = 1,

    /// <summary>بسته شده — همه رفته‌اند یا میزبان لغو کرده است.</summary>
    Closed = 2
}

/// <summary>
/// قواعدی که میزبان می‌تواند دست بزند.
///
/// جدا از <see cref="RoomSettings"/> نگه داشته شده و در یک ستون ‎jsonb‎ می‌نشیند،
/// نه یکی-یکی در ستون‌های خودشان: قانونِ بعدی که بخواهیم باز کنیم آن‌وقت یک
/// مهاجرتِ تازه نمی‌خواهد. مقدارهای پیش‌فرض **دقیقاً** همان‌هایی هستند که
/// <see cref="GameOptions"/> دارد، پس اتاقی که دستش نزند هیچ فرقی نمی‌کند.
///
/// **کران‌ها اینجا سنجیده می‌شوند نه در فرم.** فرم فقط راهنماست؛ یک کلاینتِ
/// دستکاری‌شده می‌تواند هر عددی بفرستد و ‎RoadsPerPlayer = ۲ میلیارد‎ بازی را
/// همان لحظه‌ی ساخت می‌ترکاند.
/// </summary>
public sealed record HouseRules
{
    public int DiscardLimit { get; init; } = 7;

    public int BankPerResource { get; init; } = 19;

    public int FriendlyRobberThreshold { get; init; } = 2;

    public int LongestRoadMinimum { get; init; } = 5;

    public int LargestArmyMinimum { get; init; } = 3;

    public int BankTradeRate { get; init; } = 4;

    public int TradeWindowSeconds { get; init; } = 30;

    public int SettlementsPerPlayer { get; init; } = 5;

    public int CitiesPerPlayer { get; init; } = 4;

    public int RoadsPerPlayer { get; init; } = 15;

    /// <summary>همان بازیِ کلاسیک — برای مقایسه و برای دکمه‌ی «برگرداندن».</summary>
    public static HouseRules Classic { get; } = new();

    /// <summary>
    /// آیا چیزی از حالت کلاسیک عوض شده؟
    ///
    /// در ‎JSON‎ نمی‌آید: این یک نتیجه است نه یک ورودی، و نشستنش در ستونِ
    /// دیتابیس یعنی مقداری که ممکن است روزی با محاسبه‌اش نخواند.
    /// </summary>
    [JsonIgnore]
    public bool IsClassic => this == Classic;

    /// <summary>
    /// کران‌ها سخاوتمندند ولی بی‌انتها نیستند.
    ///
    /// هر کدام یک دلیل دارند: جاده کمتر از طولِ لازمِ نشان یعنی «طولانی‌ترین
    /// جاده» هرگز گرفته نمی‌شود، بانکِ خالی یعنی هیچ تولیدی، و مهلتِ صفر یعنی
    /// معامله پیش از دیده‌شدن منقضی می‌شود.
    /// </summary>
    [JsonIgnore]
    public bool IsValid =>
        DiscardLimit is >= 2 and <= 30
        && BankPerResource is >= 5 and <= 60
        && FriendlyRobberThreshold is >= 0 and <= 10
        && LongestRoadMinimum is >= 2 and <= 15
        && LargestArmyMinimum is >= 1 and <= 10
        && BankTradeRate is >= 2 and <= 6
        && TradeWindowSeconds is >= 5 and <= 300
        && SettlementsPerPlayer is >= 2 and <= 10
        && CitiesPerPlayer is >= 1 and <= 10
        && RoadsPerPlayer is >= 2 and <= 30

        // با جاده‌ی کمتر از حدِ نشان، «طولانی‌ترین جاده» هرگز گرفتنی نیست — یک
        // بازیِ سرِ پا ولی با یک قانونِ مرده.
        && RoadsPerPlayer >= LongestRoadMinimum;
}

/// <summary>
/// تنظیمات یک اتاق. <see cref="MaxPlayers"/> سقف صندلی‌هاست، نه تعداد قطعی
/// بازیکن؛ بازی با هر تعدادی از دو نفر به بالا شروع می‌شود.
/// </summary>
public sealed record RoomSettings
{
    public int MaxPlayers { get; init; } = 4;

    public int VictoryPoints { get; init; } = 10;

    public int BoardRadius { get; init; } = 2;

    public bool FriendlyRobber { get; init; }

    /// <summary>seed دلخواه برای برد؛ اگر تهی باشد هنگام شروع تصادفی انتخاب می‌شود.</summary>
    public ulong? Seed { get; init; }

    /// <summary>
    /// چیدمان دستیِ برد. اگر باشد از <see cref="Seed"/> و <see cref="BoardRadius"/>
    /// جلو می‌افتد — کد خودش اندازه‌ی برد را هم در خود دارد.
    /// </summary>
    public string? BoardCode { get; init; }

    public bool HasCustomBoard => !string.IsNullOrWhiteSpace(BoardCode);

    /// <summary>
    /// بازی تیمی. تقسیم یک‌درمیان است تا هم‌تیمی‌ها پشت سر هم نوبت نگیرند؛
    /// با تعداد فرد بازیکن یک تیم یک نفر بیشتر دارد.
    /// </summary>
    public bool Teams { get; init; }

    /// <summary>قواعد خانگی؛ پیش‌فرضش همان بازیِ کلاسیک است.</summary>
    public HouseRules Rules { get; init; } = HouseRules.Classic;

    public bool IsValid =>
        MaxPlayers is >= 2 and <= 6
        && VictoryPoints is >= 3 and <= 20
        && BoardRadius is >= 1 and <= 4
        && Rules.IsValid
        && (!HasCustomBoard || Domain.Board.BoardCode.IsValid(BoardCode));

    /// <summary>تبدیل به تنظیمات بازی برای تعداد بازیکنِ واقعی.</summary>
    public GameOptions ToGameOptions(int playerCount, ulong seed) => new()
    {
        PlayerCount = playerCount,
        VictoryPoints = VictoryPoints,
        BoardRadius = BoardRadius,
        FriendlyRobber = FriendlyRobber,
        Seed = seed,

        // با دو نفر تیم‌بندی همان بازی انفرادی است، پس نادیده گرفته می‌شود.
        Teams = Teams && playerCount >= 4 ? TeamAssignment.Alternating(playerCount) : null,

        DiscardLimit = Rules.DiscardLimit,
        BankPerResource = Rules.BankPerResource,
        FriendlyRobberThreshold = Rules.FriendlyRobberThreshold,
        LongestRoadMinimum = Rules.LongestRoadMinimum,
        LargestArmyMinimum = Rules.LargestArmyMinimum,
        BankTradeRate = Rules.BankTradeRate,
        TradeWindowSeconds = Rules.TradeWindowSeconds,
        SettlementsPerPlayer = Rules.SettlementsPerPlayer,
        CitiesPerPlayer = Rules.CitiesPerPlayer,
        RoadsPerPlayer = Rules.RoadsPerPlayer
    };
}

/// <summary>یک نفر که روی صندلی اتاق نشسته است.</summary>
public sealed record RoomMember(int Seat, Guid UserId, string DisplayName, string AvatarColor, bool IsGuest);

/// <summary>
/// اتاق پیش از بازی: تنظیمات، میزبان و صندلی‌ها.
///
/// بعد از شروع، اتاق باقی می‌ماند و به بازی اشاره می‌کند تا لینک دعوتی که قبلاً
/// پخش شده همچنان کار کند و آدم را به بازی برساند.
/// </summary>
public sealed class Room
{
    public Room(
        Guid id,
        string code,
        Guid hostId,
        RoomStatus status,
        RoomSettings settings,
        IReadOnlyList<RoomMember> members,
        Guid? gameId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Code = code;
        HostId = hostId;
        Status = status;
        Settings = settings;
        Members = members;
        GameId = gameId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    /// <summary>کد کوتاه دعوت.</summary>
    public string Code { get; }

    public Guid HostId { get; }

    public RoomStatus Status { get; }

    public RoomSettings Settings { get; }

    /// <summary>صندلی‌ها به ترتیب شماره؛ همین ترتیب اندیس بازیکن در بازی می‌شود.</summary>
    public IReadOnlyList<RoomMember> Members { get; }

    public Guid? GameId { get; }

    public DateTimeOffset CreatedAt { get; }

    public bool IsFull => Members.Count >= Settings.MaxPlayers;

    public bool Contains(Guid userId) => Members.Any(m => m.UserId == userId);

    public bool IsHost(Guid userId) => HostId == userId;

    /// <summary>کوچک‌ترین شماره‌ی صندلی خالی.</summary>
    public int FirstFreeSeat()
    {
        var taken = Members.Select(m => m.Seat).ToHashSet();
        for (var seat = 0; seat < Settings.MaxPlayers; seat++)
        {
            if (!taken.Contains(seat))
            {
                return seat;
            }
        }

        return -1;
    }
}

/// <summary>خلاصه‌ی اتاق برای فهرست لابی.</summary>
public sealed record RoomSummary(
    Guid Id,
    string Code,
    RoomStatus Status,
    string HostName,
    int MemberCount,
    int MaxPlayers,
    int VictoryPoints,
    DateTimeOffset CreatedAt);
