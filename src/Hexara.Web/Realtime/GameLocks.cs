using System.Collections.Concurrent;

namespace Hexara.Web.Realtime;

/// <summary>
/// حرکت‌های یک بازی را پشت سر هم اجرا می‌کند.
///
/// بدون این، دو حرکت هم‌زمان روی یک بازی به برخورد نسخه می‌خورند و یکی باید دوباره
/// تلاش کند. در بازی نوبتی این کم پیش می‌آید، به جز یک حالت که اتفاقاً خیلی هم
/// پیش می‌آید: بعد از تاس ۷ چند نفر هم‌زمان کارت دور می‌ریزند.
///
/// مثل <see cref="GamePresence"/> در حافظه است و فرضش یک نمونه سرور است.
/// </summary>
public sealed class GameLocks
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<T> RunAsync<T>(Guid gameId, Func<Task<T>> work, CancellationToken cancellationToken = default)
    {
        var gate = _locks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            return await work();
        }
        finally
        {
            gate.Release();
        }
    }
}
