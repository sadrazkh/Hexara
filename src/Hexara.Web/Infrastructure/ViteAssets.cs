using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Hexara.Web.Infrastructure;

public sealed class ViteOptions
{
    /// <summary>در حالت توسعه اگر فعال باشد، دارایی‌ها از dev server ویت با HMR سرو می‌شوند.</summary>
    public bool UseDevServer { get; set; }

    public string DevServerUrl { get; set; } = "http://localhost:5173";

    /// <summary>مسیر عمومی خروجی build نسبت به ریشه سایت.</summary>
    public string PublicBase { get; set; } = "/dist";
}

internal sealed record ViteChunk
{
    [JsonPropertyName("file")] public string File { get; init; } = string.Empty;
    [JsonPropertyName("css")] public string[]? Css { get; init; }
    [JsonPropertyName("imports")] public string[]? Imports { get; init; }
    [JsonPropertyName("isEntry")] public bool IsEntry { get; init; }
}

/// <summary>
/// خواننده‌ی <c>manifest.json</c> ویت. خروجی build با هش نام‌گذاری می‌شود، پس Razor
/// نمی‌تواند نام فایل را حدس بزند و باید از manifest بخواند.
/// </summary>
public sealed class ViteManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWebHostEnvironment _env;
    private readonly ViteOptions _options;
    private readonly ILogger<ViteManifest> _logger;
    private readonly object _sync = new();

    private Dictionary<string, ViteChunk>? _chunks;
    private DateTime _loadedAtUtc;
    private long _loadedLength;

    public ViteManifest(IWebHostEnvironment env, IOptions<ViteOptions> options, ILogger<ViteManifest> logger)
    {
        _env = env;
        _options = options.Value;
        _logger = logger;
    }

    public bool DevServerEnabled => _options.UseDevServer && _env.IsDevelopment();

    public string DevServerUrl => _options.DevServerUrl.TrimEnd('/');

    /// <summary>آدرس فایل JS ورودی و تمام CSS مورد نیاز آن (شامل CSS چانک‌های وارداتی).</summary>
    public (string Script, IReadOnlyList<string> Styles) Resolve(string entry)
    {
        if (DevServerEnabled)
        {
            return ($"{DevServerUrl}/{entry.TrimStart('/')}", Array.Empty<string>());
        }

        var chunks = LoadChunks();
        if (chunks is null || !chunks.TryGetValue(entry, out var chunk))
        {
            _logger.LogWarning("ورودی ویت «{Entry}» در manifest پیدا نشد. آیا npm run build اجرا شده است؟", entry);
            return (string.Empty, Array.Empty<string>());
        }

        var css = new List<string>();
        CollectCss(chunks, entry, css, new HashSet<string>(StringComparer.Ordinal));

        var basePath = "/" + _options.PublicBase.Trim('/');
        return ($"{basePath}/{chunk.File}", css.Select(c => $"{basePath}/{c}").ToList());
    }

    private static void CollectCss(
        Dictionary<string, ViteChunk> chunks,
        string key,
        List<string> sink,
        HashSet<string> visited)
    {
        if (!visited.Add(key) || !chunks.TryGetValue(key, out var chunk))
        {
            return;
        }

        // CSS چانک‌های وارداتی اول می‌آید تا ترتیب cascade مثل زمان build حفظ شود.
        foreach (var import in chunk.Imports ?? Array.Empty<string>())
        {
            CollectCss(chunks, import, sink, visited);
        }

        foreach (var css in chunk.Css ?? Array.Empty<string>())
        {
            if (!sink.Contains(css))
            {
                sink.Add(css);
            }
        }
    }

    private Dictionary<string, ViteChunk>? LoadChunks()
    {
        var path = ManifestPath();
        if (path is null)
        {
            return null;
        }

        var info = new FileInfo(path);

        // در حالت توسعه اگر manifest تغییر کند دوباره خوانده می‌شود؛ در تولید یک بار کافی است.
        lock (_sync)
        {
            var stale = _chunks is null
                || (_env.IsDevelopment() && (info.LastWriteTimeUtc != _loadedAtUtc || info.Length != _loadedLength));

            if (!stale)
            {
                return _chunks;
            }

            try
            {
                using var stream = File.OpenRead(path);
                _chunks = JsonSerializer.Deserialize<Dictionary<string, ViteChunk>>(stream, JsonOptions);
                _loadedAtUtc = info.LastWriteTimeUtc;
                _loadedLength = info.Length;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خواندن manifest ویت از {Path} ناموفق بود.", path);
                _chunks = null;
            }

            return _chunks;
        }
    }

    private string? ManifestPath()
    {
        var root = _env.WebRootPath;
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        var dist = Path.Combine(root, _options.PublicBase.Trim('/'));

        // ویت ۵ به بعد manifest را داخل ‎.vite/‎ می‌گذارد؛ مسیر قدیمی هم پشتیبانی می‌شود.
        var modern = Path.Combine(dist, ".vite", "manifest.json");
        if (File.Exists(modern))
        {
            return modern;
        }

        var legacy = Path.Combine(dist, "manifest.json");
        return File.Exists(legacy) ? legacy : null;
    }
}
