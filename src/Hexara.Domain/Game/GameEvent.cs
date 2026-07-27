using Hexara.Domain.Board;

namespace Hexara.Domain.Game;

/// <summary>
/// اتفاقی که در پی یک حرکت افتاد. رویدادها در فاز ۳ به عنوان تاریخچه‌ی بازی ذخیره
/// و در فاز ۵ برای بازیکن‌ها پخش می‌شوند، پس باید خودبسنده و قابل سریال‌سازی بمانند.
///
/// نکته: بعضی رویدادها (مثل <see cref="ResourceStolen"/>) اطلاعات محرمانه دارند و
/// لایه‌ی پخش باید قبل از ارسال به بقیه آن‌ها را سانسور کند.
/// </summary>
public abstract record GameEvent;

public sealed record SetupSettlementPlaced(int PlayerIndex, VertexId Vertex) : GameEvent;

public sealed record SetupRoadPlaced(int PlayerIndex, EdgeId Edge) : GameEvent;

public sealed record SetupCompleted : GameEvent;

public sealed record DiceRolled(int PlayerIndex, int Die1, int Die2) : GameEvent
{
    public int Total => Die1 + Die2;
}

/// <summary>یک سهم تولید: بازیکن، منبع و تعداد.</summary>
public sealed record ResourceGrant(int PlayerIndex, Resource Resource, int Amount);

public sealed record ResourcesProduced(IReadOnlyList<ResourceGrant> Grants) : GameEvent;

/// <summary>بانک برای این منبع کم آورد و طبق قانون هیچ‌کس آن را نگرفت.</summary>
public sealed record ProductionSkippedForBank(Resource Resource) : GameEvent;

public sealed record DiscardRequired(IReadOnlyDictionary<int, int> Amounts) : GameEvent;

public sealed record CardsDiscarded(int PlayerIndex, IReadOnlyDictionary<Resource, int> Cards) : GameEvent;

public sealed record RobberMoved(int PlayerIndex, Axial From, Axial To) : GameEvent;

/// <summary>کارت دزدیده‌شده — فقط دزد و قربانی حق دیدن <see cref="Resource"/> را دارند.</summary>
public sealed record ResourceStolen(int PlayerIndex, int VictimIndex, Resource Resource) : GameEvent;

public sealed record RoadBuilt(int PlayerIndex, EdgeId Edge) : GameEvent;

public sealed record SettlementBuilt(int PlayerIndex, VertexId Vertex) : GameEvent;

public sealed record CityBuilt(int PlayerIndex, VertexId Vertex) : GameEvent;

public sealed record TurnStarted(int PlayerIndex, int TurnNumber) : GameEvent;

public sealed record GameWon(int PlayerIndex, int VictoryPoints) : GameEvent;

/// <summary>نتیجه‌ی اجرای یک حرکت.</summary>
public sealed record MoveResult(bool Success, GameError Error, IReadOnlyList<GameEvent> Events)
{
    public static MoveResult Fail(GameError error) => new(false, error, []);

    public static MoveResult Ok(params GameEvent[] events) => new(true, GameError.None, events);

    public static MoveResult Ok(IReadOnlyList<GameEvent> events) => new(true, GameError.None, events);
}
