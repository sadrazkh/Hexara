using System.ComponentModel.DataAnnotations;
using Hexara.Application.Games;
using Hexara.Application.Rooms;

namespace Hexara.Web.ViewModels;

/// <summary>صفحه‌ی لابی: اتاق‌های باز، اتاق‌های خودم، و فرم‌های ساخت و پیوستن.</summary>
public class LobbyIndexViewModel
{
    public IReadOnlyList<RoomSummary> OpenRooms { get; init; } = [];

    public IReadOnlyList<RoomSummary> MyRooms { get; init; } = [];

    public CreateRoomViewModel Create { get; init; } = new();

    public string? JoinCode { get; init; }
}

public class CreateRoomViewModel
{
    [Range(2, 6)]
    public int MaxPlayers { get; set; } = 4;

    [Range(3, 20)]
    public int VictoryPoints { get; set; } = 10;

    [Range(1, 4)]
    public int BoardRadius { get; set; } = 2;

    public bool FriendlyRobber { get; set; }

    public RoomSettings ToSettings() => new()
    {
        MaxPlayers = MaxPlayers,
        VictoryPoints = VictoryPoints,
        BoardRadius = BoardRadius,
        FriendlyRobber = FriendlyRobber
    };
}

/// <summary>صفحه‌ی یک اتاق.</summary>
public class RoomViewModel
{
    public required Room Room { get; init; }

    public required Guid CurrentUserId { get; init; }

    public bool IsHost => Room.IsHost(CurrentUserId);

    public bool IsMember => Room.Contains(CurrentUserId);

    public bool CanStart => IsHost && Room.Status == RoomStatus.Open && Room.Members.Count >= 2;

    /// <summary>صندلی‌ها به ترتیب، با جای خالی‌ها — تا لابی همیشه شکل ثابتی داشته باشد.</summary>
    public IReadOnlyList<RoomMember?> Seats
    {
        get
        {
            var bySeat = Room.Members.ToDictionary(m => m.Seat);
            return [.. Enumerable.Range(0, Room.Settings.MaxPlayers).Select(i => bySeat.GetValueOrDefault(i))];
        }
    }
}

/// <summary>صفحه‌ی ویرایشگر برد سفارشی.</summary>
public class BoardEditViewModel
{
    public required Guid RoomId { get; init; }

    public required string RoomCode { get; init; }

    public required BoardDraft Draft { get; init; }

    public required string Code { get; init; }

    /// <summary>آیا این برد قبلاً روی اتاق ذخیره شده یا فقط پیش‌نویسِ نمایشی است؟</summary>
    public required bool IsSaved { get; init; }
}

/// <summary>صفحه‌ی بازی — در این فاز فقط وضعیت و صندلی‌ها.</summary>
public class GamePlayViewModel
{
    public required StoredGame Game { get; init; }

    /// <summary>صندلی خودِ بیننده.</summary>
    public required int Seat { get; init; }

    public bool IsMyTurn => Game.State.CurrentPlayer == Seat;
}
