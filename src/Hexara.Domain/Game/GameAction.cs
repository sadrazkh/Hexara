using Hexara.Domain.Board;

namespace Hexara.Domain.Game;

/// <summary>
/// حرکتی که یک بازیکن درخواست می‌کند. اعتبارسنجی همیشه سمت سرور و داخل
/// <c>GameEngine</c> انجام می‌شود؛ کلاینت هرگز مرجع نیست.
/// </summary>
public abstract record GameAction(int PlayerIndex);

public sealed record PlaceInitialSettlement(int PlayerIndex, VertexId Vertex) : GameAction(PlayerIndex);

public sealed record PlaceInitialRoad(int PlayerIndex, EdgeId Edge) : GameAction(PlayerIndex);

public sealed record RollDice(int PlayerIndex) : GameAction(PlayerIndex);

public sealed record BuildRoad(int PlayerIndex, EdgeId Edge) : GameAction(PlayerIndex);

public sealed record BuildSettlement(int PlayerIndex, VertexId Vertex) : GameAction(PlayerIndex);

public sealed record BuildCity(int PlayerIndex, VertexId Vertex) : GameAction(PlayerIndex);

/// <summary>دور ریختن کارت بعد از تاس ۷.</summary>
public sealed record DiscardCards(int PlayerIndex, IReadOnlyDictionary<Resource, int> Cards) : GameAction(PlayerIndex);

/// <summary>جابه‌جایی دزد و دزدیدن یک کارت از قربانی. اگر قربانی‌ای نباشد <c>null</c>.</summary>
public sealed record MoveRobber(int PlayerIndex, Axial Hex, int? Victim) : GameAction(PlayerIndex);

public sealed record EndTurn(int PlayerIndex) : GameAction(PlayerIndex);

// ── کارت توسعه ───────────────────────────────────────────────────────────

public sealed record BuyDevelopmentCard(int PlayerIndex) : GameAction(PlayerIndex);

/// <summary>شوالیه: دزد را جابه‌جا می‌کند و به ارتش بازیکن اضافه می‌شود.</summary>
public sealed record PlayKnight(int PlayerIndex, Axial Hex, int? Victim) : GameAction(PlayerIndex);

/// <summary>جاده‌سازی: تا دو جاده‌ی رایگان. جاده‌ی دوم اختیاری است.</summary>
public sealed record PlayRoadBuilding(int PlayerIndex, EdgeId First, EdgeId? Second) : GameAction(PlayerIndex);

/// <summary>سال فراوانی: دو منبع دلخواه از بانک.</summary>
public sealed record PlayYearOfPlenty(int PlayerIndex, Resource First, Resource Second) : GameAction(PlayerIndex);

/// <summary>انحصار: همه‌ی بازیکنان تمام کارت‌های یک منبع را تحویل می‌دهند.</summary>
public sealed record PlayMonopoly(int PlayerIndex, Resource Resource) : GameAction(PlayerIndex);

// ── معامله ───────────────────────────────────────────────────────────────

/// <summary>معامله با بانک یا بندر؛ نرخ از روی بندرهای بازیکن تعیین می‌شود.</summary>
public sealed record MaritimeTrade(int PlayerIndex, Resource Give, Resource Take) : GameAction(PlayerIndex);

/// <summary>پیشنهاد معامله به بقیه. اگر <paramref name="Recipients"/> خالی باشد یعنی به همه.</summary>
public sealed record ProposeTrade(
    int PlayerIndex,
    IReadOnlyDictionary<Resource, int> Give,
    IReadOnlyDictionary<Resource, int> Take,
    IReadOnlyList<int> Recipients) : GameAction(PlayerIndex);

public sealed record RespondToTrade(int PlayerIndex, bool Accept) : GameAction(PlayerIndex);

/// <summary>پیشنهاددهنده معامله را با یکی از پذیرندگان قطعی می‌کند.</summary>
public sealed record ConfirmTrade(int PlayerIndex, int Partner) : GameAction(PlayerIndex);

public sealed record CancelTrade(int PlayerIndex) : GameAction(PlayerIndex);

/// <summary>
/// پیشنهاد متقابل: گیرنده به‌جای پذیرفتن، شرط خودش را می‌گذارد.
///
/// پیشنهاد روی میز را برمی‌دارد و یکی تازه می‌گذارد که پیشنهاددهنده‌اش خودِ
/// اوست و تنها گیرنده‌اش پیشنهاددهنده‌ی قبلی. پس معامله همچنان بین همان دو نفر
/// می‌ماند و کسی که نوبتش نیست نمی‌تواند از این راه معامله‌ی تازه‌ای باز کند.
/// </summary>
public sealed record CounterTrade(
    int PlayerIndex,
    IReadOnlyDictionary<Resource, int> Give,
    IReadOnlyDictionary<Resource, int> Take) : GameAction(PlayerIndex);
