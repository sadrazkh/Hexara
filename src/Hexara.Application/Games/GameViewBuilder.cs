using System.Globalization;
using Hexara.Application.Common.Interfaces;
using Hexara.Application.Players;
using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Application.Games;

/// <summary>ساخت نمای بازی برای یک صندلی مشخص.</summary>
public sealed class GameViewBuilder
{
    private readonly IPlayerDirectory _directory;
    private readonly AutoPlayPolicy _policy;

    public GameViewBuilder(IPlayerDirectory directory, AutoPlayPolicy? policy = null)
    {
        _directory = directory;
        _policy = policy ?? AutoPlayPolicy.Default;
    }

    /// <summary>
    /// جدول هزینه‌ها؛ یک‌بار ساخته می‌شود چون ثابت است.
    ///
    /// کلیدها همان نامی هستند که رابط برای ترجمه به کار می‌برد
    /// (‎game.buildRoad‎ …)، پس افزودن یک ساخت‌وسازِ تازه همین‌جا و در فایل
    /// ترجمه دیده می‌شود، نه در دلِ کامپوننت.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<Resource, int>> Prices =
        new Dictionary<string, IReadOnlyDictionary<Resource, int>>
        {
            ["Road"] = BuildCosts.Road,
            ["Settlement"] = BuildCosts.Settlement,
            ["City"] = BuildCosts.City,
            ["DevelopmentCard"] = BuildCosts.DevelopmentCard
        };

    public async Task<GameView> BuildAsync(
        StoredGame game,
        int? viewerSeat,
        IReadOnlySet<Guid>? onlineUserIds = null,
        CancellationToken cancellationToken = default)
    {
        var profiles = (await _directory.GetAsync(game.PlayerIds, cancellationToken))
            .ToDictionary(p => p.Id);

        var state = game.State;

        return new GameView
        {
            GameId = game.Id,
            Version = state.Version,
            Phase = state.Phase,
            CurrentPlayer = state.CurrentPlayer,
            TurnNumber = state.TurnNumber,
            Winner = state.Winner,
            Die1 = state.Die1,
            Die2 = state.Die2,
            UpdatedAt = game.UpdatedAt,
            DeadlineSeconds = (int)_policy.TurnDeadline.TotalSeconds,
            AbsentGraceSeconds = (int)_policy.AbsentGrace.TotalSeconds,
            Robber = new HexSnapshot(state.Robber.Q, state.Robber.R),
            Tiles = [.. state.Board.Tiles.Select(t =>
                new TileSnapshot(t.Position.Q, t.Position.R, t.Terrain, t.Number))],
            Ports = [.. state.Board.Ports.Select(p =>
                new PortSnapshot(p.Edge.Hex.Q, p.Edge.Hex.R, p.Edge.Side, p.Resource))],
            Buildings = [.. state.Buildings.Select(b =>
                new BuildingSnapshot(b.Key.Hex.Q, b.Key.Hex.R, b.Key.Corner, b.Value.PlayerIndex, b.Value.Kind))],
            Roads = [.. state.Roads.Select(r =>
                new RoadSnapshot(r.Key.Hex.Q, r.Key.Hex.R, r.Key.Side, r.Value))],
            Bank = new Dictionary<Resource, int>(state.Bank),
            Costs = Prices,
            LongestRoadMinimum = state.Options.LongestRoadMinimum,
            LargestArmyMinimum = state.Options.LargestArmyMinimum,
            DevelopmentDeckCount = state.DevelopmentDeckCount,
            Players = [.. state.Players.Select(p =>
                ToView(p, game.PlayerIds[p.Index], profiles, onlineUserIds, state, state.Winner is not null))],
            Seat = viewerSeat,
            Hand = viewerSeat is { } seat ? ToHand(state, seat) : null,
            PendingDiscards = new Dictionary<int, int>(state.PendingDiscards),
            PendingTrade = state.PendingTrade is { } trade
                ? new TradeOfferView(
                    trade.Proposer,
                    new Dictionary<Resource, int>(trade.Give),
                    new Dictionary<Resource, int>(trade.Take),
                    new Dictionary<int, TradeResponse>(trade.Responses),
                    trade.ExpiresAt)
                : null,
            Legal = LegalFor(state, viewerSeat)
        };
    }

    private static PlayerView ToView(
        PlayerState player,
        Guid userId,
        IReadOnlyDictionary<Guid, PlayerProfile> profiles,
        IReadOnlySet<Guid>? online,
        GameState state,
        bool revealHiddenPoints)
    {
        var profile = profiles.GetValueOrDefault(userId);

        return new PlayerView
        {
            Index = player.Index,
            UserId = userId,
            DisplayName = profile?.DisplayName ?? string.Empty,
            AvatarColor = profile?.AvatarColor ?? AvatarPalette.Default,

            // بعد از تمام شدن بازی رازی نمانده، و اگر کارت‌های پیروزیِ پنهان
            // شمرده نشوند امتیازِ برنده کمتر از حدِ برد نشان داده می‌شود.
            PublicVictoryPoints = revealHiddenPoints
                ? player.VictoryPoints
                : player.PublicVictoryPoints,
            Team = state.Options.Teams?.TeamOf(player.Index),
            CardCount = player.TotalCards,
            DevelopmentCardCount = player.TotalDevelopmentCards,
            KnightsPlayed = player.KnightsPlayed,
            HasLongestRoad = player.HasLongestRoad,
            HasLargestArmy = player.HasLargestArmy,
            LongestRoadLength = player.LongestRoadLength,

            // بندر و نرخ هر دو عمومی‌اند: بندرِ حریف روی برد پیداست و نرخش در
            // معامله تعیین‌کننده است. نرخ از موتور می‌آید تا قاعده‌اش یک جا بماند.
            Ports = [.. GameEngine.PortsOf(state, player.Index)
                .Select(p => new PortSnapshot(p.Edge.Hex.Q, p.Edge.Hex.R, p.Edge.Side, p.Resource))],
            TradeRates = Enum.GetValues<Resource>()
                .ToDictionary(r => r, r => GameEngine.MaritimeRate(state, player.Index, r)),

            SettlementsLeft = player.SettlementsLeft,
            CitiesLeft = player.CitiesLeft,
            RoadsLeft = player.RoadsLeft,
            IsOnline = online?.Contains(userId) ?? false
        };
    }

    private static HandView ToHand(GameState state, int seat)
    {
        var player = state.Player(seat);

        return new HandView
        {
            Resources = new Dictionary<Resource, int>(player.Resources),
            DevelopmentCards = new Dictionary<DevelopmentCard, int>(player.DevelopmentCards),
            NewDevelopmentCards = new Dictionary<DevelopmentCard, int>(player.NewDevelopmentCards),
            VictoryPoints = player.VictoryPoints,
            Score = state.ScoreOf(seat),
            PlayedDevelopmentCardThisTurn = player.PlayedDevelopmentCardThisTurn,
            MustDiscard = state.PendingDiscards.GetValueOrDefault(seat)
        };
    }

    /// <summary>
    /// کلید یالی که کلاینت هم می‌سازد. همان قالبِ فشرده‌ی همیشگی، با فرهنگ
    /// ناوابسته — علامت منفی در فارسی ‎ASCII‎ نیست و کلیدها هرگز جور درنمی‌آمدند.
    /// </summary>
    private static string Key(EdgeId edge) =>
        string.Create(CultureInfo.InvariantCulture, $"{edge.Hex.Q},{edge.Hex.R},{edge.Side}");

    /// <summary>
    /// حرکت‌های قانونی فقط وقتی محاسبه می‌شوند که واقعاً نوبت این صندلی باشد —
    /// هم برای صرفه‌جویی و هم چون در غیر این صورت هیچ‌کدامشان معنا ندارند.
    /// </summary>
    private static LegalMovesView LegalFor(GameState state, int? viewerSeat)
    {
        if (viewerSeat is not { } seat || state.Phase == TurnPhase.GameOver)
        {
            return LegalMovesView.None;
        }

        // در مرحله‌ی دور ریختن، نوبتِ کسی نیست ولی بدهکارها باید کاری بکنند.
        var isMyTurn = state.CurrentPlayer == seat
            || (state.Phase == TurnPhase.Discard && state.PendingDiscards.ContainsKey(seat));

        if (!isMyTurn)
        {
            return LegalMovesView.None;
        }

        var settlements = state.Phase is TurnPhase.SetupSettlement or TurnPhase.Main
            ? GameEngine.LegalSettlementVertices(state, seat).ToList()
            : [];

        var roads = state.Phase switch
        {
            TurnPhase.SetupRoad => state.LastSetupSettlement is { } v
                ? v.TouchingEdges().Where(e => state.Board.ContainsEdge(e) && state.RoadAt(e) is null).ToList()
                : [],
            TurnPhase.Main => GameEngine.LegalRoadEdges(state, seat).ToList(),
            _ => []
        };

        var cities = state.Phase == TurnPhase.Main
            ? state.BuildingsOf(seat)
                .Where(b => b.Value.Kind == BuildingKind.Settlement)
                .Select(b => b.Key)
                .ToList()
            : [];

        var playable = GameEngine.PlayableDevelopmentCards(state, seat);

        // خانه‌های دزد هم برای مرحله‌ی دزد لازم است و هم برای شوالیه، چون
        // شوالیه هم دزد را جابه‌جا می‌کند.
        var needsRobberTargets =
            state.Phase == TurnPhase.MoveRobber || playable.Contains(DevelopmentCard.Knight);

        var robberTargets = needsRobberTargets
            ? state.Board.Tiles.Where(t => t.Position != state.Robber).Select(t => t.Position).ToList()
            : [];

        // جاده‌ی رایگان همان جاهایی است که جاده‌ی خریدنی می‌رود، ولی در مرحله‌ی
        // تاس هم معنا دارد — و آن‌جا فهرستِ خریدنی خالی است.
        var freeRoads = playable.Contains(DevelopmentCard.RoadBuilding)
            ? GameEngine.LegalRoadEdges(state, seat).ToList()
            : [];

        // برای هر انتخابِ اول، جاهای تازه‌ی جاده‌ی دوم. فقط وقتی حساب می‌شود که
        // کارت واقعاً در دست باشد، پس هزینه‌اش سرِ بازی عادی صفر است.
        var followUps = freeRoads.ToDictionary(
            first => Key(first),
            first => (IReadOnlyList<RoadSnapshot>)
            [
                .. GameEngine.LegalRoadEdgesAfter(state, seat, first)
                    .Where(e => e != first)
                    .Select(e => new RoadSnapshot(e.Hex.Q, e.Hex.R, e.Side, seat))
            ],
            StringComparer.Ordinal);

        return new LegalMovesView
        {
            IsMyTurn = true,
            FreeRoads = [.. freeRoads.Select(e => new RoadSnapshot(e.Hex.Q, e.Hex.R, e.Side, seat))],
            FollowUpRoads = followUps,
            PlayableCards = playable,
            Settlements = [.. settlements.Select(v => new VertexSnapshot(v.Hex.Q, v.Hex.R, v.Corner))],
            Roads = [.. roads.Select(e => new RoadSnapshot(e.Hex.Q, e.Hex.R, e.Side, seat))],
            Cities = [.. cities.Select(v => new VertexSnapshot(v.Hex.Q, v.Hex.R, v.Corner))],
            RobberTargets = [.. robberTargets.Select(h => new HexSnapshot(h.Q, h.R))]
        };
    }
}
