using Hexara.Domain.Board;
using Hexara.Domain.Common;

namespace Hexara.Domain.Game;

/// <summary>
/// یک بازیکن ماشینی ساده.
///
/// این «قانون» نیست، «سیاست» است — ولی اینجا در دامنه زندگی می‌کند چون به هیچ
/// چیز جز خود قوانین نیاز ندارد، و مهم‌تر: این‌طور تست‌های دامنه می‌توانند بازی‌های
/// کامل را با همین بات جلو ببرند. آزمونِ «بات هیچ‌وقت گیر نمی‌کند» فقط با بازی
/// کردنِ واقعی به دست می‌آید، نه با تست نقطه‌ای.
///
/// هوشش عمداً کم است ولی احمق نیست: جای آبادی را با احتمال تولید می‌سنجد و دزد را
/// روی جلوافتاده‌ترین حریف می‌گذارد. هدفش این است که جای بازیکن غایب را طوری پر
/// کند که بازی برای بقیه خراب نشود.
/// </summary>
public static class BotPlayer
{
    /// <summary>
    /// حرکت بعدی این صندلی، یا <c>null</c> اگر الان نوبتش نیست و چیزی هم بدهکار نیست.
    /// همیشه حرکتی برمی‌گرداند که موتور قبولش می‌کند.
    /// </summary>
    public static GameAction? NextAction(GameState state, int seat, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rng);

        if (state.Phase == TurnPhase.GameOver || seat < 0 || seat >= state.Players.Count)
        {
            return null;
        }

        // مرحله‌ی دور ریختن تنها جایی است که نوبتِ کسی نیست ولی چند نفر باید کاری بکنند.
        if (state.Phase == TurnPhase.Discard)
        {
            return state.PendingDiscards.ContainsKey(seat) ? Discard(state, seat) : null;
        }

        if (state.CurrentPlayer != seat)
        {
            return null;
        }

        return state.Phase switch
        {
            TurnPhase.SetupSettlement => SetupSettlement(state, seat, rng),
            TurnPhase.SetupRoad => SetupRoad(state, seat, rng),
            TurnPhase.Roll => new RollDice(seat),
            TurnPhase.MoveRobber => MoveTheRobber(state, seat, rng),
            TurnPhase.Main => MainPhase(state, seat, rng),
            _ => null
        };
    }

    /// <summary>هر صندلی‌ای که همین حالا منتظر تصمیم اوست.</summary>
    public static IEnumerable<int> SeatsToAct(GameState state)
    {
        if (state.Phase == TurnPhase.GameOver)
        {
            return [];
        }

        return state.Phase == TurnPhase.Discard
            ? state.PendingDiscards.Keys.Order()
            : [state.CurrentPlayer];
    }

    // ── چیدمان اولیه ─────────────────────────────────────────────────────

    /// <summary>
    /// بهترین گوشه از نظر احتمال تولید. برابرها تصادفی شکسته می‌شوند تا دو بات
    /// روی یک برد همیشه یک بازی تکراری نسازند.
    /// </summary>
    private static GameAction? SetupSettlement(GameState state, int seat, Rng rng)
    {
        var spots = GameEngine.LegalSettlementVertices(state, seat).ToList();
        if (spots.Count == 0)
        {
            return null;
        }

        var best = BestBy(spots, v => Pips(state, v), rng);
        return new PlaceInitialSettlement(seat, best);
    }

    private static GameAction? SetupRoad(GameState state, int seat, Rng rng)
    {
        if (state.LastSetupSettlement is not { } settlement)
        {
            return null;
        }

        var edges = settlement.TouchingEdges()
            .Where(e => state.Board.ContainsEdge(e) && state.RoadAt(e) is null)
            .ToList();

        return edges.Count == 0 ? null : new PlaceInitialRoad(seat, Pick(edges, rng));
    }

    /// <summary>مجموع نقطه‌های احتمال خانه‌های چسبیده به یک گوشه.</summary>
    private static int Pips(GameState state, VertexId vertex)
    {
        var total = 0;

        foreach (var hex in vertex.TouchingHexes())
        {
            if (state.Board.TileAt(hex) is { Number: { } number } tile && tile.Resource is not null)
            {
                total += 6 - Math.Abs(7 - number);
            }
        }

        return total;
    }

    // ── دزد و دور ریختن ──────────────────────────────────────────────────

    /// <summary>کارت‌ها از بلندترین دسته برداشته می‌شوند تا دست متوازن‌تر بماند.</summary>
    private static GameAction Discard(GameState state, int seat)
    {
        var player = state.Player(seat);
        var required = state.PendingDiscards[seat];

        var left = TerrainExtensions.AllResources.ToDictionary(r => r, r => player[r]);
        var cards = new Dictionary<Resource, int>();

        for (var i = 0; i < required; i++)
        {
            var richest = left.OrderByDescending(p => p.Value).ThenBy(p => p.Key).First().Key;
            left[richest]--;
            cards[richest] = cards.GetValueOrDefault(richest) + 1;
        }

        return new DiscardCards(seat, cards);
    }

    /// <summary>
    /// دزد روی خانه‌ای می‌رود که بیشترین آسیب را به جلوافتاده‌ترین حریف بزند، و
    /// هرگز روی خانه‌ی خودِ بات نمی‌نشیند.
    /// </summary>
    private static GameAction? MoveTheRobber(GameState state, int seat, Rng rng)
    {
        var targets = state.Board.Tiles
            .Where(t => t.Position != state.Robber)
            .ToList();

        if (targets.Count == 0)
        {
            return null;
        }

        var best = BestBy(targets, tile => Threat(state, tile, seat), rng);
        var victims = GameEngine.RobberVictims(state, best.Position, seat).ToList();

        // از میان قربانی‌های ممکن، آن‌که بیشترین امتیاز عمومی را دارد.
        int? victim = victims.Count == 0
            ? null
            : victims.OrderByDescending(v => state.Player(v).PublicVictoryPoints)
                .ThenByDescending(v => state.Player(v).TotalCards)
                .First();

        return new MoveRobber(seat, best.Position, victim);
    }

    /// <summary>ارزش گذاشتن دزد روی یک خانه: ساختمان حریف خوب است، ساختمان خودی خیلی بد.</summary>
    private static int Threat(GameState state, HexTile tile, int seat)
    {
        var score = 0;

        foreach (var vertex in tile.Vertices())
        {
            if (state.BuildingAt(vertex) is not { } building)
            {
                continue;
            }

            if (building.PlayerIndex == seat)
            {
                return int.MinValue;
            }

            score += building.Yield * (1 + state.Player(building.PlayerIndex).PublicVictoryPoints);
        }

        return score;
    }

    // ── بدنه‌ی نوبت ──────────────────────────────────────────────────────

    /// <summary>
    /// ترتیب اولویت‌ها عمداً ساده است: شهر، آبادی، جاده، کارت توسعه — و اگر هیچ‌کدام
    /// نشد، تبدیل منابع اضافی با بانک، و در نهایت پایان نوبت.
    /// </summary>
    private static GameAction MainPhase(GameState state, int seat, Rng rng)
    {
        var player = state.Player(seat);

        if (Knight(state, seat, rng) is { } knight)
        {
            return knight;
        }

        if (player.CanAfford(BuildCosts.City) && player.CitiesLeft > 0)
        {
            var settlements = state.BuildingsOf(seat)
                .Where(b => b.Value.Kind == BuildingKind.Settlement)
                .Select(b => b.Key)
                .ToList();

            if (settlements.Count > 0)
            {
                return new BuildCity(seat, BestBy(settlements, v => Pips(state, v), rng));
            }
        }

        if (player.CanAfford(BuildCosts.Settlement) && player.SettlementsLeft > 0)
        {
            var spots = GameEngine.LegalSettlementVertices(state, seat).ToList();
            if (spots.Count > 0)
            {
                return new BuildSettlement(seat, BestBy(spots, v => Pips(state, v), rng));
            }
        }

        if (player.CanAfford(BuildCosts.Road) && player.RoadsLeft > 0)
        {
            var edges = GameEngine.LegalRoadEdges(state, seat).ToList();
            if (edges.Count > 0)
            {
                return new BuildRoad(seat, Pick(edges, rng));
            }
        }

        if (player.CanAfford(BuildCosts.DevelopmentCard) && state.DevelopmentDeckCount > 0)
        {
            return new BuyDevelopmentCard(seat);
        }

        if (TradeUp(state, seat) is { } trade)
        {
            return trade;
        }

        return new EndTurn(seat);
    }

    /// <summary>شوالیه فقط وقتی بازی می‌شود که دزد روی خانه‌ی خودِ بات نشسته باشد.</summary>
    private static GameAction? Knight(GameState state, int seat, Rng rng)
    {
        var player = state.Player(seat);
        if (player.PlayedDevelopmentCardThisTurn || player[DevelopmentCard.Knight] <= 0)
        {
            return null;
        }

        var blocked = state.Board.TileAt(state.Robber) is { } tile
            && tile.Vertices().Any(v => state.BuildingAt(v)?.PlayerIndex == seat);

        if (!blocked)
        {
            return null;
        }

        return MoveTheRobber(state, seat, rng) is MoveRobber move
            ? new PlayKnight(seat, move.Hex, move.Victim)
            : null;
    }

    /// <summary>منبع اضافی را با بانک به چیزی که ندارد تبدیل می‌کند تا نوبت‌ها بی‌حاصل نمانند.</summary>
    private static GameAction? TradeUp(GameState state, int seat)
    {
        var player = state.Player(seat);

        foreach (var give in TerrainExtensions.AllResources)
        {
            var rate = GameEngine.MaritimeRate(state, seat, give);
            if (player[give] < rate)
            {
                continue;
            }

            foreach (var take in TerrainExtensions.AllResources)
            {
                if (take != give && player[take] == 0 && state.BankOf(take) > 0)
                {
                    return new MaritimeTrade(seat, give, take);
                }
            }
        }

        return null;
    }

    // ── کمکی ─────────────────────────────────────────────────────────────

    private static T BestBy<T>(IReadOnlyList<T> items, Func<T, int> score, Rng rng)
    {
        var best = items[0];
        var bestScore = score(best);
        var ties = 1;

        for (var i = 1; i < items.Count; i++)
        {
            var value = score(items[i]);

            if (value > bestScore)
            {
                best = items[i];
                bestScore = value;
                ties = 1;
            }
            else if (value == bestScore)
            {
                // نمونه‌گیری مخزنی: بین برابرها یکی به‌طور یکنواخت انتخاب می‌شود.
                ties++;
                if (rng.Next(ties) == 0)
                {
                    best = items[i];
                }
            }
        }

        return best;
    }

    private static T Pick<T>(IReadOnlyList<T> items, Rng rng) => items[rng.Next(items.Count)];
}
