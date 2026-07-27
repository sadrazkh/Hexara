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
