namespace Hexara.Application.Rooms;

/// <summary>
/// اتاق از دید کلاینت.
///
/// چرا نه خودِ <see cref="Room"/>: قرارداد سیم باید صریح باشد و با تغییر مدل دامنه
/// بی‌خبر عوض نشود. ‎Seed‎ هم عمداً بیرون است — عددش دنباله‌ی تاس‌ها را لو می‌دهد و
/// کسی که برد را ندیده نباید از پیش بداند چه می‌آید. کدِ برد ولی می‌آید، چون
/// ساخته شده تا دست‌به‌دست بچرخد.
/// </summary>
public sealed record RoomView
{
    public required Guid Id { get; init; }

    public required string Code { get; init; }

    public required RoomStatus Status { get; init; }

    public required Guid HostId { get; init; }

    /// <summary>وقتی بازی شروع شد، همه با همین شناسه به صفحه‌ی بازی می‌روند.</summary>
    public Guid? GameId { get; init; }

    public required int MaxPlayers { get; init; }

    public required int VictoryPoints { get; init; }

    public required int BoardRadius { get; init; }

    public required bool FriendlyRobber { get; init; }

    public required bool Teams { get; init; }

    /// <summary>قواعد خانگی — همیشه پر است، حتی وقتی همان کلاسیک باشد.</summary>
    public required HouseRules Rules { get; init; }

    /// <summary>آیا چیزی از حالت کلاسیک عوض شده؟ رابط با همین نشان می‌دهد.</summary>
    public bool CustomRules => !Rules.IsClassic;

    public string? BoardCode { get; init; }

    public required IReadOnlyList<RoomSeatView> Seats { get; init; }

    /// <summary>شروع بازی از دو نفر ممکن است؛ کلاینت هم باید همین را بداند.</summary>
    public bool CanStart => Status == RoomStatus.Open && Seats.Count >= 2;

    public static RoomView Of(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        return new RoomView
        {
            Id = room.Id,
            Code = room.Code,
            Status = room.Status,
            HostId = room.HostId,
            GameId = room.GameId,
            MaxPlayers = room.Settings.MaxPlayers,
            VictoryPoints = room.Settings.VictoryPoints,
            BoardRadius = room.Settings.BoardRadius,
            FriendlyRobber = room.Settings.FriendlyRobber,
            Teams = room.Settings.Teams,
            Rules = room.Settings.Rules,
            BoardCode = room.Settings.BoardCode,
            Seats =
            [
                .. room.Members
                    .OrderBy(m => m.Seat)
                    .Select(m => new RoomSeatView(
                        m.Seat,
                        m.UserId,
                        m.DisplayName,
                        m.AvatarColor,
                        m.IsGuest,
                        m.UserId == room.HostId))
            ]
        };
    }
}

public sealed record RoomSeatView(
    int Seat,
    Guid UserId,
    string DisplayName,
    string AvatarColor,
    bool IsGuest,
    bool IsHost);

/// <summary>تنظیماتی که میزبان از راه هاب عوض می‌کند — برد سفارشی و seed اینجا نیستند.</summary>
public sealed record RoomSettingsInput(
    int MaxPlayers,
    int VictoryPoints,
    int BoardRadius,
    bool FriendlyRobber,
    bool Teams,
    /// <summary>
    /// قواعد خانگی؛ تهی یعنی «دست نزن».
    ///
    /// اختیاری است تا کلاینتی که این بخش را نمی‌فرستد قواعد اتاق را بی‌صدا به
    /// کلاسیک برنگرداند.
    /// </summary>
    HouseRules? Rules = null);
