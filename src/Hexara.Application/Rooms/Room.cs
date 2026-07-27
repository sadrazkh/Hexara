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

    public bool IsValid =>
        MaxPlayers is >= 2 and <= 6
        && VictoryPoints is >= 3 and <= 20
        && BoardRadius is >= 1 and <= 4;

    /// <summary>تبدیل به تنظیمات بازی برای تعداد بازیکنِ واقعی.</summary>
    public GameOptions ToGameOptions(int playerCount, ulong seed) => new()
    {
        PlayerCount = playerCount,
        VictoryPoints = VictoryPoints,
        BoardRadius = BoardRadius,
        FriendlyRobber = FriendlyRobber,
        Seed = seed
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
