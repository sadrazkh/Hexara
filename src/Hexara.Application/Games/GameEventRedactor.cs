using Hexara.Domain.Game;

namespace Hexara.Application.Games;

/// <summary>
/// رویدادها را برای هر بیننده سانسور می‌کند.
///
/// لاگ ذخیره‌شده همیشه کامل است (برای بازپخش)، ولی آنچه روی سیم می‌رود باید به
/// اندازه‌ی دانشِ همان بازیکن باشد. اگر این لایه نباشد، یک کلاینت دستکاری‌شده
/// می‌تواند کارت‌های پنهان بقیه را بخواند بدون اینکه هیچ قانونی را بشکند.
/// </summary>
public static class GameEventRedactor
{
    /// <summary>
    /// نسخه‌ی مناسب این رویداد برای صندلی داده‌شده. <paramref name="viewerSeat"/> تهی
    /// یعنی تماشاچی — که کمترین دانش را دارد.
    /// </summary>
    public static GameEvent ForSeat(GameEvent gameEvent, int? viewerSeat) => gameEvent switch
    {
        // دزد و قربانی هر دو می‌دانند چه کارتی رد و بدل شد؛ بقیه فقط می‌بینند اتفاقی افتاد.
        ResourceStolen e when viewerSeat != e.PlayerIndex && viewerSeat != e.VictimIndex =>
            new ResourceStolenSecretly(e.PlayerIndex, e.VictimIndex),

        DevelopmentCardBought e when viewerSeat != e.PlayerIndex =>
            new DevelopmentCardBoughtSecretly(e.PlayerIndex),

        _ => gameEvent
    };

    public static IReadOnlyList<GameEvent> ForSeat(IReadOnlyList<GameEvent> events, int? viewerSeat) =>
        [.. events.Select(e => ForSeat(e, viewerSeat))];

    /// <summary>آیا این رویداد برای بیننده‌های مختلف شکل متفاوتی دارد؟</summary>
    public static bool IsSecret(GameEvent gameEvent) =>
        gameEvent is ResourceStolen or DevelopmentCardBought;
}
