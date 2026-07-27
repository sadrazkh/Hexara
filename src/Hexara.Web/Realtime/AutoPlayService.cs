using Hexara.Application.Games;
using Microsoft.Extensions.Options;

namespace Hexara.Web.Realtime;

/// <summary>تنظیمات پوشش خودکار؛ از ‎appsettings‎ خوانده می‌شود.</summary>
public sealed class AutoPlayOptions
{
    public const string Section = "AutoPlay";

    public bool Enabled { get; set; } = true;

    /// <summary>چند ثانیه بعد از قطع شدنِ یک بازیکن، بات جایش را بگیرد.</summary>
    public int AbsentGraceSeconds { get; set; } = 25;

    /// <summary>مهلت نوبت برای بازیکنی که حاضر است ولی کاری نمی‌کند.</summary>
    public int TurnDeadlineSeconds { get; set; } = 180;

    /// <summary>فاصله‌ی هر بار سرکشی.</summary>
    public int PollSeconds { get; set; } = 5;

    /// <summary>سقف بازی‌هایی که در هر دور بررسی می‌شوند.</summary>
    public int BatchSize { get; set; } = 20;

    public AutoPlayPolicy ToPolicy() => new(
        TimeSpan.FromSeconds(Math.Max(1, AbsentGraceSeconds)),
        TimeSpan.FromSeconds(Math.Max(1, TurnDeadlineSeconds)));
}

/// <summary>
/// بات را به جای بازیکنی که غایب است یا از مهلتش گذشته وارد بازی می‌کند.
///
/// چرا سرکشی به دیتابیس و نه تایمر در حافظه: تایمر با ری‌استارت سرور گم می‌شود و
/// بازی برای همیشه معطل می‌ماند. ستون ‎UpdatedAt‎ همان چیزی است که لازم داریم و
/// از ری‌استارت جان سالم به در می‌برد.
///
/// حرکت‌ها پشت همان قفلی می‌روند که هاب استفاده می‌کند، پس بات و یک بازیکنِ
/// دیرجنب هرگز هم‌زمان روی یک بازی نمی‌نویسند.
/// </summary>
public sealed class AutoPlayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly GamePresence _presence;
    private readonly GameLocks _locks;
    private readonly AutoPlayOptions _options;
    private readonly ILogger<AutoPlayService> _logger;

    public AutoPlayService(
        IServiceScopeFactory scopes,
        GamePresence presence,
        GameLocks locks,
        IOptions<AutoPlayOptions> options,
        ILogger<AutoPlayService> logger)
    {
        _scopes = scopes;
        _presence = presence;
        _locks = locks;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("پوشش خودکار بازیکن غایب خاموش است.");
            return;
        }

        var policy = _options.ToPolicy();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.PollSeconds)));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(policy, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // یک دور ناموفق نباید سرویس را بکشد؛ دور بعد دوباره تلاش می‌شود.
                _logger.LogError(ex, "سرکشی پوشش خودکار ناموفق بود.");
            }
        }
    }

    private async Task SweepAsync(AutoPlayPolicy policy, CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var games = scope.ServiceProvider.GetRequiredService<GameService>();

        var idle = await games.ListIdleAsync(policy.Shortest, _options.BatchSize, cancellationToken);

        foreach (var gameId in idle)
        {
            // هر بازی scope خودش را می‌گیرد تا یک خطا بقیه را زمین نزند و
            // DbContext بین بازی‌ها آلوده نشود.
            await using var gameScope = _scopes.CreateAsyncScope();
            await CoverAsync(gameScope.ServiceProvider, gameId, policy, cancellationToken);
        }
    }

    private async Task CoverAsync(
        IServiceProvider services,
        Guid gameId,
        AutoPlayPolicy policy,
        CancellationToken cancellationToken)
    {
        var games = services.GetRequiredService<GameService>();
        var broadcaster = services.GetRequiredService<GameBroadcaster>();

        try
        {
            var outcome = await _locks.RunAsync(
                gameId,
                () => games.AutoPlayAsync(gameId, _presence.OnlineIn(gameId), policy, cancellationToken),
                cancellationToken);

            if (outcome is null)
            {
                return;
            }

            var game = await games.GetAsync(gameId, cancellationToken);
            if (game is not null)
            {
                await broadcaster.SendAsync(game, outcome.Events, cancellationToken);
            }

            _logger.LogDebug("بات در بازی {GameId} حرکتی زد (نسخه {Version}).", gameId, outcome.Version);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "پوشش خودکار بازی {GameId} ناموفق بود.", gameId);
        }
    }
}
