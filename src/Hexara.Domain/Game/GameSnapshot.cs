using Hexara.Domain.Board;

namespace Hexara.Domain.Game;

/// <summary>
/// عکس کامل و تخت وضعیت بازی — تنها قالبی که برای ذخیره‌سازی از دامنه بیرون می‌رود.
///
/// عمداً هیچ نوع «هوشمندی» در آن نیست: نه شناسه‌ی کانونی گوشه و ضلع، نه دیکشنری‌های
/// داخلی. همه‌چیز عدد و enum ساده است تا هر سریال‌سازی‌ای (JSON در فاز ۳) بدون
/// مبدل سفارشی کار کند و دامنه به قالب ذخیره‌سازی وابسته نشود.
/// </summary>
public sealed record GameSnapshot
{
    /// <summary>نسخه‌ی قالب؛ اگر ساختار عوض شود، مهاجرت داده از روی همین انجام می‌شود.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required GameOptions Options { get; init; }

    public required IReadOnlyList<TileSnapshot> Tiles { get; init; }

    public required IReadOnlyList<PortSnapshot> Ports { get; init; }

    public required IReadOnlyList<PlayerSnapshot> Players { get; init; }

    public required IReadOnlyList<BuildingSnapshot> Buildings { get; init; }

    public required IReadOnlyList<RoadSnapshot> Roads { get; init; }

    public required IReadOnlyDictionary<Resource, int> Bank { get; init; }

    public required IReadOnlyList<DevelopmentCard> Deck { get; init; }

    public required HexSnapshot Robber { get; init; }

    public required TurnPhase Phase { get; init; }

    public required int CurrentPlayer { get; init; }

    public required int TurnNumber { get; init; }

    public required long Version { get; init; }

    public int? Die1 { get; init; }

    public int? Die2 { get; init; }

    public int? Winner { get; init; }

    public required int SetupStep { get; init; }

    public VertexSnapshot? LastSetupSettlement { get; init; }

    public required IReadOnlyDictionary<int, int> PendingDiscards { get; init; }

    /// <summary>وضعیت مولد تصادفی — بدون آن، ادامه‌ی بازی بعد از بارگذاری قابل بازپخش نیست.</summary>
    public required ulong RngState { get; init; }

    public TradeOfferSnapshot? PendingTrade { get; init; }
}

public sealed record HexSnapshot(int Q, int R);

public sealed record VertexSnapshot(int Q, int R, int Corner);

public sealed record TileSnapshot(int Q, int R, Terrain Terrain, int? Number);

public sealed record PortSnapshot(int Q, int R, int Side, Resource? Resource);

public sealed record BuildingSnapshot(int Q, int R, int Corner, int PlayerIndex, BuildingKind Kind);

public sealed record RoadSnapshot(int Q, int R, int Side, int PlayerIndex);

public sealed record PlayerSnapshot
{
    public required int Index { get; init; }

    public required Guid Id { get; init; }

    public required IReadOnlyDictionary<Resource, int> Resources { get; init; }

    public required IReadOnlyDictionary<DevelopmentCard, int> DevelopmentCards { get; init; }

    public required IReadOnlyDictionary<DevelopmentCard, int> NewDevelopmentCards { get; init; }

    public required int SettlementsLeft { get; init; }

    public required int CitiesLeft { get; init; }

    public required int RoadsLeft { get; init; }

    public required int BuildingPoints { get; init; }

    public required int VictoryPointCards { get; init; }

    public required bool HasLongestRoad { get; init; }

    public required bool HasLargestArmy { get; init; }

    public required int LongestRoadLength { get; init; }

    public required int KnightsPlayed { get; init; }

    public required bool PlayedDevelopmentCardThisTurn { get; init; }
}

public sealed record TradeOfferSnapshot
{
    public required int Proposer { get; init; }

    public required IReadOnlyDictionary<Resource, int> Give { get; init; }

    public required IReadOnlyDictionary<Resource, int> Take { get; init; }

    public required IReadOnlyDictionary<int, TradeResponse> Responses { get; init; }
}
