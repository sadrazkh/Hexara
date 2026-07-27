using Hexara.Domain.Board;
using Hexara.Domain.Common;
using Hexara.Domain.Game;

namespace Hexara.Domain.Tests;

/// <summary>
/// کمک‌کننده‌های ساخت سناریو برای تست‌ها. همه‌چیز قطعی است: با seed یکسان دقیقاً
/// همان برد، همان چیدمان اولیه و همان تاس‌ها تکرار می‌شود.
/// </summary>
internal static class Games
{
    public static GameState New(int players = 3, ulong seed = 1, Func<GameOptions, GameOptions>? tweak = null)
    {
        var options = new GameOptions { PlayerCount = players, Seed = seed };
        options = tweak?.Invoke(options) ?? options;

        var ids = Enumerable.Range(0, players).Select(i => Guid.Parse($"00000000-0000-0000-0000-00000000000{i}")).ToList();
        return GameState.Create(options, ids);
    }

    /// <summary>چیدمان اولیه را با انتخاب اولین جای مجاز (به ترتیب قطعی) کامل می‌کند.</summary>
    public static void RunSetup(GameState state)
    {
        while (state.IsSetup)
        {
            var player = state.CurrentPlayer;

            var vertex = GameEngine.LegalSettlementVertices(state, player)
                .OrderBy(v => v.ToString(), StringComparer.Ordinal)
                .First();
            Assert.True(GameEngine.Apply(state, new PlaceInitialSettlement(player, vertex)).Success);

            var edge = vertex.TouchingEdges()
                .Where(e => state.Board.ContainsEdge(e) && state.RoadAt(e) is null)
                .OrderBy(e => e.ToString(), StringComparer.Ordinal)
                .First();
            Assert.True(GameEngine.Apply(state, new PlaceInitialRoad(player, edge)).Success);
        }
    }

    /// <summary>
    /// بازی‌ای با چیدمان کامل که تاس بعدی‌اش دقیقاً عدد خواسته‌شده است. مولد تاس بدون
    /// مصرف شدن «سرک کشیده» می‌شود، پس خودِ بازی دست‌نخورده می‌ماند.
    /// </summary>
    public static GameState SetupWithNextRoll(int total, int players = 3)
    {
        for (ulong seed = 1; seed < 5000; seed++)
        {
            var state = New(players, seed);
            RunSetup(state);

            var peek = Rng.FromState(state.Rng.State);
            if (peek.RollDie() + peek.RollDie() == total)
            {
                return state;
            }
        }

        throw new InvalidOperationException($"هیچ seedی با تاس اول {total} پیدا نشد.");
    }

    /// <summary>
    /// بازی‌ای که تاس بعدی‌اش ۷ نیست و حداقل یک ساختمان روی خانه‌های همان عدد وجود
    /// دارد — یعنی آن تاس واقعاً چیزی تولید می‌کند.
    /// </summary>
    public static GameState SetupWithProductiveRoll(out int roll, int players = 3)
    {
        for (ulong seed = 1; seed < 5000; seed++)
        {
            var state = New(players, seed);
            RunSetup(state);

            var peek = Rng.FromState(state.Rng.State);
            var total = peek.RollDie() + peek.RollDie();
            if (total == 7)
            {
                continue;
            }

            var produces = state.Board.TilesWithNumber(total)
                .Any(t => t.Position != state.Robber
                    && t.Resource is not null
                    && t.Vertices().Any(v => state.BuildingAt(v) is not null));

            if (produces)
            {
                roll = total;
                return state;
            }
        }

        throw new InvalidOperationException("هیچ seedی با تاس اولِ تولیدکننده پیدا نشد.");
    }

    /// <summary>
    /// بازی بدون چیدمان اولیه که مستقیم روی مرحله‌ی تاس گذاشته می‌شود و تاس بعدی‌اش
    /// معلوم است — برای سناریوهایی که ساختمان‌ها را دستی می‌چینیم.
    /// </summary>
    public static GameState FreshWithKnownRoll(out int roll, int players = 3)
    {
        for (ulong seed = 1; seed < 5000; seed++)
        {
            var state = New(players, seed);

            var peek = Rng.FromState(state.Rng.State);
            var total = peek.RollDie() + peek.RollDie();
            if (total == 7 || !state.Board.TilesWithNumber(total).Any(t => t.Resource is not null))
            {
                continue;
            }

            state.Phase = TurnPhase.Roll;
            state.CurrentPlayer = 0;
            state.TurnNumber = 1;
            roll = total;
            return state;
        }

        throw new InvalidOperationException("هیچ seedی با تاس اولِ مناسب پیدا نشد.");
    }

    /// <summary>منابعی که این تاس باید تولید کند، مستقل از موتور محاسبه می‌شود.</summary>
    public static Dictionary<(int Player, Resource Resource), int> ExpectedProduction(GameState state, int roll)
    {
        var expected = new Dictionary<(int, Resource), int>();

        foreach (var tile in state.Board.TilesWithNumber(roll))
        {
            if (tile.Position == state.Robber || tile.Resource is not { } resource)
            {
                continue;
            }

            foreach (var vertex in tile.Vertices())
            {
                if (state.BuildingAt(vertex) is not { } building)
                {
                    continue;
                }

                var key = (building.PlayerIndex, resource);
                expected[key] = expected.GetValueOrDefault(key) + building.Yield;
            }
        }

        return expected;
    }

    /// <summary>عکس فوری از دست همه‌ی بازیکن‌ها برای مقایسه‌ی قبل و بعد.</summary>
    public static Dictionary<(int Player, Resource Resource), int> Hands(GameState state) =>
        state.Players
            .SelectMany(p => TerrainExtensions.AllResources.Select(r => (Key: (p.Index, r), Value: p[r])))
            .ToDictionary(x => x.Key, x => x.Value);

    /// <summary>دادن منابع به یک بازیکن برای چیدن سناریو؛ موجودی بانک هم به‌روز می‌شود.</summary>
    public static void Give(GameState state, int player, params (Resource Resource, int Amount)[] cards)
    {
        foreach (var (resource, amount) in cards)
        {
            state.Player(player).Add(resource, amount);
            state.BankTake(resource, amount);
        }
    }

    public static void GiveSettlementCost(GameState state, int player) =>
        Give(state, player, (Resource.Lumber, 1), (Resource.Brick, 1), (Resource.Wool, 1), (Resource.Grain, 1));

    public static void GiveRoadCost(GameState state, int player) =>
        Give(state, player, (Resource.Lumber, 1), (Resource.Brick, 1));

    public static void GiveCityCost(GameState state, int player) =>
        Give(state, player, (Resource.Ore, 3), (Resource.Grain, 2));

    /// <summary>وضعیت را به ابتدای بدنه‌ی نوبت بازیکن داده‌شده می‌برد، بدون انداختن تاس.</summary>
    public static void StartMainPhase(GameState state, int player)
    {
        state.CurrentPlayer = player;
        state.Phase = TurnPhase.Main;
    }
}
