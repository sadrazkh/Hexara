using Hexara.Application.Games;
using Hexara.Infrastructure.Identity;

namespace Hexara.Infrastructure.Persistence.Entities;

/// <summary>
/// یک بازی در دیتابیس.
///
/// خودِ وضعیت بازی به صورت یک سند JSON در <see cref="Snapshot"/> نگه داشته می‌شود،
/// نه ده‌ها جدول رابطه‌ای. دلیلش این است که وضعیت فقط به صورت کامل خوانده و نوشته
/// می‌شود و هیچ‌وقت رویش کوئری جزئی نمی‌زنیم؛ ستون‌های کنارش فقط برای فهرست‌کردن
/// و فیلتر کردن بازی‌ها هستند.
/// </summary>
public class GameRecord
{
    public Guid Id { get; set; }

    public GameStatus Status { get; set; }

    public int PlayerCount { get; set; }

    public int TurnNumber { get; set; }

    public Guid? WinnerId { get; set; }

    /// <summary>عکس وضعیت به صورت JSON (در Postgres ستون jsonb).</summary>
    public string Snapshot { get; set; } = string.Empty;

    /// <summary>
    /// شماره‌ی نسخه‌ی وضعیت که موتور بازی جلو می‌برد. به عنوان توکن هم‌زمانی استفاده
    /// می‌شود تا دو حرکت هم‌زمان روی هم نیفتند.
    /// </summary>
    public long Version { get; set; }

    /// <summary>تعداد حرکت‌های ثبت‌شده — شماره‌ی ردیف بعدی تاریخچه از همین می‌آید.</summary>
    public int MoveCount { get; set; }

    // زمان‌ها همیشه UTC ذخیره می‌شوند. عمداً DateTime است و نه DateTimeOffset:
    // SQLite (که تست‌ها رویش اجرا می‌شوند) نمی‌تواند روی DateTimeOffset مرتب‌سازی کند و
    // چون هیچ‌وقت چیزی جز UTC نمی‌نویسیم، اطلاعاتی از دست نمی‌رود.
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<GamePlayerRecord> Players { get; set; } = [];

    public ICollection<GameMoveRecord> Moves { get; set; } = [];
}

/// <summary>نشستن یک کاربر روی یک صندلی از یک بازی.</summary>
public class GamePlayerRecord
{
    public Guid GameId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>شماره‌ی صندلی — دقیقاً همان اندیس بازیکن در دامنه.</summary>
    public int Seat { get; set; }

    public GameRecord? Game { get; set; }

    public AppUser? User { get; set; }
}

/// <summary>
/// یک حرکت انجام‌شده به همراه رویدادهایی که تولید کرد. این جدول فقط اضافه می‌شود و
/// هرگز تغییر نمی‌کند؛ پایه‌ی بازپخش بازی و در فاز ۵ رساندن اتفاق‌های ازدست‌رفته به
/// بازیکنی که دوباره وصل می‌شود.
/// </summary>
public class GameMoveRecord
{
    public long Id { get; set; }

    public Guid GameId { get; set; }

    public int Sequence { get; set; }

    /// <summary>نسخه‌ی وضعیت بازی بعد از این حرکت.</summary>
    public long Version { get; set; }

    public int PlayerIndex { get; set; }

    /// <summary>خودِ حرکت به صورت JSON چندریختی.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>رویدادهای حاصل، به صورت آرایه‌ی JSON چندریختی.</summary>
    public string Events { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public GameRecord? Game { get; set; }
}
