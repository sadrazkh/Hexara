using Hexara.Domain.Board;

namespace Hexara.Domain.Game;

/// <summary>
/// محاسبه‌ی طول بلندترین جاده‌ی پیوسته‌ی یک بازیکن.
///
/// «بلندترین جاده» یعنی بلندترین مسیری که هیچ ضلعی در آن تکرار نشود؛ گذر از
/// گوشه‌ای که آبادی یا شهر حریف روی آن است ممنوع است، هرچند مسیر می‌تواند همان‌جا
/// تمام شود. چون هر بازیکن حداکثر ۱۵ جاده دارد، جست‌وجوی کامل عمقی کافی و سریع است.
/// </summary>
public static class RoadNetwork
{
    public static int LongestRoad(GameState state, int playerIndex)
    {
        var edges = state.RoadsOf(playerIndex).ToHashSet();
        if (edges.Count == 0)
        {
            return 0;
        }

        var byVertex = new Dictionary<VertexId, List<EdgeId>>();
        foreach (var edge in edges)
        {
            foreach (var vertex in edge.Endpoints())
            {
                if (!byVertex.TryGetValue(vertex, out var list))
                {
                    byVertex[vertex] = list = [];
                }

                list.Add(edge);
            }
        }

        var blocked = byVertex.Keys
            .Where(v => state.BuildingAt(v) is { } b && b.PlayerIndex != playerIndex)
            .ToHashSet();

        var used = new HashSet<EdgeId>();
        var best = 0;

        foreach (var edge in edges)
        {
            foreach (var start in edge.Endpoints())
            {
                var far = Other(edge, start);
                used.Add(edge);
                best = Math.Max(best, 1 + Extend(far));
                used.Remove(edge);
            }
        }

        return best;

        int Extend(VertexId at)
        {
            if (blocked.Contains(at))
            {
                return 0;
            }

            var longest = 0;
            foreach (var next in byVertex[at])
            {
                if (!used.Add(next))
                {
                    continue;
                }

                longest = Math.Max(longest, 1 + Extend(Other(next, at)));
                used.Remove(next);
            }

            return longest;
        }
    }

    private static VertexId Other(EdgeId edge, VertexId from) =>
        edge.Endpoints().First(v => v != from);
}
