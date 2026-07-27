using Hexara.Domain.Board;

namespace Hexara.Domain.Game;

/// <summary>
/// تنظیمات یک بازی. همه‌ی مقادیر پیش‌فرض همان قوانین کلاسیک‌اند تا اتاق‌های
/// عادی (فاز ۴) بدون تنظیم چیزی ساخته شوند.
/// </summary>
public sealed record GameOptions
{
    public required int PlayerCount { get; init; }

    /// <summary>شعاع برد؛ ۲ یعنی برد کلاسیک ۱۹ خانه‌ای.</summary>
    public int BoardRadius { get; init; } = 2;

    /// <summary>امتیاز لازم برای پیروزی.</summary>
    public int VictoryPoints { get; init; } = 10;

    /// <summary>seed تولید برد و تاس‌ها — برای بازپخش و اشتراک‌گذاری برد.</summary>
    public ulong Seed { get; init; }

    /// <summary>سقف کارت در دست؛ با تاس ۷ هر کس بیشتر داشته باشد نصف را دور می‌ریزد.</summary>
    public int DiscardLimit { get; init; } = 7;

    /// <summary>موجودی اولیه‌ی بانک از هر منبع.</summary>
    public int BankPerResource { get; init; } = 19;

    /// <summary>وریانت «دزد مهربان»: از بازیکنی که هنوز امتیاز کمی دارد نمی‌توان دزدید.</summary>
    public bool FriendlyRobber { get; init; }

    /// <summary>
    /// بازی تیمی. تهی یعنی هر کس برای خودش.
    ///
    /// تیم روی دو قانون اثر می‌گذارد و بس: امتیاز پیروزی روی کل تیم جمع می‌شود و
    /// از هم‌تیمی نمی‌شود دزدید. معامله و بقیه‌ی قوانین دست‌نخورده‌اند.
    /// </summary>
    public TeamAssignment? Teams { get; init; }

    public bool IsTeamGame => Teams is not null;

    /// <summary>حد امتیاز مصونیت در وریانت دزد مهربان.</summary>
    public int FriendlyRobberThreshold { get; init; } = 2;

    /// <summary>حداقل طول جاده برای گرفتن کارت «طولانی‌ترین جاده».</summary>
    public int LongestRoadMinimum { get; init; } = 5;

    /// <summary>حداقل تعداد شوالیه برای گرفتن کارت «بزرگ‌ترین ارتش».</summary>
    public int LargestArmyMinimum { get; init; } = 3;

    /// <summary>نرخ معامله با بانک بدون بندر.</summary>
    public int BankTradeRate { get; init; } = 4;

    public int SettlementsPerPlayer { get; init; } = 5;

    public int CitiesPerPlayer { get; init; } = 4;

    public int RoadsPerPlayer { get; init; } = 15;

    public void Validate()
    {
        if (PlayerCount is < 2 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(PlayerCount), PlayerCount, "تعداد بازیکن باید بین ۲ تا ۶ باشد.");
        }

        if (BoardRadius < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(BoardRadius), BoardRadius, "شعاع برد باید حداقل ۱ باشد.");
        }

        if (VictoryPoints < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(VictoryPoints), VictoryPoints, "امتیاز پیروزی باید حداقل ۳ باشد.");
        }

        if (Teams is not null && !Teams.IsValidFor(PlayerCount))
        {
            throw new ArgumentException(
                "تقسیم تیمی باید برای هر صندلی یک تیم داشته باشد و حداقل دو تیم بسازد.",
                nameof(Teams));
        }
    }
}

/// <summary>هزینه‌ی ثابت ساخت‌وسازها.</summary>
public static class BuildCosts
{
    public static readonly IReadOnlyDictionary<Resource, int> Road = new Dictionary<Resource, int>
    {
        [Resource.Lumber] = 1,
        [Resource.Brick] = 1
    };

    public static readonly IReadOnlyDictionary<Resource, int> Settlement = new Dictionary<Resource, int>
    {
        [Resource.Lumber] = 1,
        [Resource.Brick] = 1,
        [Resource.Wool] = 1,
        [Resource.Grain] = 1
    };

    public static readonly IReadOnlyDictionary<Resource, int> City = new Dictionary<Resource, int>
    {
        [Resource.Ore] = 3,
        [Resource.Grain] = 2
    };

    public static readonly IReadOnlyDictionary<Resource, int> DevelopmentCard = new Dictionary<Resource, int>
    {
        [Resource.Ore] = 1,
        [Resource.Wool] = 1,
        [Resource.Grain] = 1
    };
}
