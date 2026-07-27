namespace Hexara.Domain.Game;

/// <summary>مرحله‌ی جاری بازی؛ تعیین می‌کند کدام حرکت‌ها مجازند.</summary>
public enum TurnPhase
{
    /// <summary>چیدمان اولیه: نوبت گذاشتن آبادی.</summary>
    SetupSettlement = 0,

    /// <summary>چیدمان اولیه: نوبت گذاشتن جاده‌ی چسبیده به همان آبادی.</summary>
    SetupRoad = 1,

    /// <summary>ابتدای نوبت؛ بازیکن باید تاس بیندازد.</summary>
    Roll = 2,

    /// <summary>تاس ۷ آمده و بازیکنانِ پرکارت باید نصف دستشان را دور بریزند.</summary>
    Discard = 3,

    /// <summary>بازیکن باید دزد را جابه‌جا کند و در صورت امکان یک کارت بدزدد.</summary>
    MoveRobber = 4,

    /// <summary>بدنه‌ی نوبت: ساخت‌وساز و معامله.</summary>
    Main = 5,

    /// <summary>بازی تمام شده است.</summary>
    GameOver = 6
}

/// <summary>نوع ساختمان روی یک گوشه.</summary>
public enum BuildingKind
{
    Settlement = 1,
    City = 2
}

/// <summary>ساختمان یک بازیکن روی یک گوشه‌ی برد.</summary>
public sealed record Building(int PlayerIndex, BuildingKind Kind)
{
    /// <summary>تعداد منبعی که با فعال شدن خانه‌ی مجاور تولید می‌کند.</summary>
    public int Yield => Kind == BuildingKind.City ? 2 : 1;

    /// <summary>امتیاز پیروزی این ساختمان.</summary>
    public int Points => Kind == BuildingKind.City ? 2 : 1;
}
