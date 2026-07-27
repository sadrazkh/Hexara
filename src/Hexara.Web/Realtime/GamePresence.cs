using System.Collections.Concurrent;

namespace Hexara.Web.Realtime;

/// <summary>
/// چه کسی همین الان به کدام بازی وصل است.
///
/// در حافظه نگه داشته می‌شود، پس با بیش از یک نمونه‌ی سرور درست کار نمی‌کند؛ برای
/// افقی شدن باید یک backplane (مثل Redis) زیرش گذاشت. تا وقتی روی یک نمونه اجرا
/// می‌شویم این ساده‌ترین چیزِ درست است.
///
/// یک کاربر می‌تواند چند اتصال داشته باشد (دو تب باز)، پس اتصال‌ها شمرده می‌شوند
/// و «آفلاین» یعنی آخرین اتصالش هم رفته باشد.
/// </summary>
public sealed class GamePresence
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Guid>> _connections = new();

    /// <summary>اتصال را ثبت می‌کند و می‌گوید آیا این کاربر تازه آنلاین شده است.</summary>
    public bool Add(Guid gameId, string connectionId, Guid userId)
    {
        var game = _connections.GetOrAdd(gameId, _ => new ConcurrentDictionary<string, Guid>());
        var wasOnline = game.Values.Contains(userId);
        game[connectionId] = userId;

        return !wasOnline;
    }

    /// <summary>
    /// اتصال را برمی‌دارد و می‌گوید کدام کاربر کاملاً آفلاین شد (اگر شد).
    /// </summary>
    public Guid? Remove(Guid gameId, string connectionId)
    {
        if (!_connections.TryGetValue(gameId, out var game) || !game.TryRemove(connectionId, out var userId))
        {
            return null;
        }

        if (game.IsEmpty)
        {
            _connections.TryRemove(gameId, out _);
        }

        return game.Values.Contains(userId) ? null : userId;
    }

    /// <summary>بازی‌هایی که این اتصال در آن‌ها حاضر بود — برای وقتی که سوکت بی‌خبر می‌میرد.</summary>
    public IReadOnlyList<Guid> GamesOf(string connectionId) =>
        [.. _connections.Where(g => g.Value.ContainsKey(connectionId)).Select(g => g.Key)];

    public IReadOnlySet<Guid> OnlineIn(Guid gameId) =>
        _connections.TryGetValue(gameId, out var game)
            ? game.Values.ToHashSet()
            : new HashSet<Guid>();
}
