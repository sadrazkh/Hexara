using Hexara.Application.Rooms;
using Hexara.Infrastructure.Identity;

namespace Hexara.Infrastructure.Persistence.Entities;

/// <summary>
/// اتاق پیش از بازی.
///
/// تنظیمات اینجا ستون‌های جداگانه‌اند و نه JSON — برخلاف وضعیت بازی، روی این‌ها
/// فیلتر و مرتب‌سازی می‌زنیم (فهرست لابی) و تعدادشان هم کم است.
/// </summary>
public class RoomRecord
{
    public Guid Id { get; set; }

    /// <summary>کد کوتاه دعوت؛ یکتا و همیشه با حروف بزرگ.</summary>
    public string Code { get; set; } = string.Empty;

    public Guid HostId { get; set; }

    public RoomStatus Status { get; set; }

    public int MaxPlayers { get; set; }

    public int VictoryPoints { get; set; }

    public int BoardRadius { get; set; }

    public bool FriendlyRobber { get; set; }

    /// <summary>
    /// seed دلخواه برد. <c>ulong</c> در Postgres ستون ندارد، پس بدون بررسی سرریز به
    /// <c>long</c> تبدیل می‌شود؛ الگوی بیت‌ها دست‌نخورده می‌ماند و برگشتش دقیق است.
    /// </summary>
    public long? Seed { get; set; }

    public Guid? GameId { get; set; }

    public DateTime CreatedAt { get; set; }

    public AppUser? Host { get; set; }

    public GameRecord? Game { get; set; }

    public ICollection<RoomMemberRecord> Members { get; set; } = [];
}

/// <summary>یک صندلی اشغال‌شده در اتاق.</summary>
public class RoomMemberRecord
{
    public Guid RoomId { get; set; }

    public Guid UserId { get; set; }

    public int Seat { get; set; }

    public DateTime JoinedAt { get; set; }

    public RoomRecord? Room { get; set; }

    public AppUser? User { get; set; }
}
