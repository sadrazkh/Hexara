using System.Globalization;
using System.Text.Json;

namespace Hexara.Web.Infrastructure;

/// <summary>
/// ترجمه‌های رابط کاربری از فایل‌های JSON مشترک بین سرور (Razor) و کلاینت (Vue)
/// خوانده می‌شود تا یک منبع حقیقت واحد داشته باشیم و متن‌ها دوتکه نشوند.
/// کلیدها تودرتو نوشته می‌شوند و با نقطه صدا زده می‌شوند: <c>t("nav.play")</c>.
/// </summary>
public sealed class UiTranslator
{
    public const string DefaultCulture = "fa";

    public static readonly string[] SupportedCultures = ["fa", "en"];

    private readonly Dictionary<string, Dictionary<string, string>> _catalogs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<UiTranslator> _logger;

    public UiTranslator(IWebHostEnvironment env, ILogger<UiTranslator> logger)
    {
        _logger = logger;

        var dir = Path.Combine(env.ContentRootPath, "Locales");
        foreach (var culture in SupportedCultures)
        {
            var path = Path.Combine(dir, $"{culture}.json");
            _catalogs[culture] = Load(path);
        }
    }

    /// <summary>کل دیکشنری یک زبان — برای تزریق به کلاینت یا نمایش در دیباگ.</summary>
    public IReadOnlyDictionary<string, string> Catalog(string culture) =>
        _catalogs.TryGetValue(Normalize(culture), out var c) ? c : _catalogs[DefaultCulture];

    public string this[string key] => Translate(CurrentCulture(), key);

    public string T(string key) => Translate(CurrentCulture(), key);

    public string T(string key, params object?[] args)
    {
        var format = Translate(CurrentCulture(), key);
        try
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    public static string CurrentCulture() => Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public static bool IsRtl(string culture) => Normalize(culture) == "fa";

    private string Translate(string culture, string key)
    {
        if (_catalogs.TryGetValue(culture, out var catalog) && catalog.TryGetValue(key, out var value))
        {
            return value;
        }

        // بازگشت به زبان پیش‌فرض، و در نهایت خودِ کلید تا کمبود ترجمه در UI دیده شود.
        if (culture != DefaultCulture
            && _catalogs.TryGetValue(DefaultCulture, out var fallback)
            && fallback.TryGetValue(key, out var fallbackValue))
        {
            return fallbackValue;
        }

        return key;
    }

    private static string Normalize(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return DefaultCulture;
        }

        var two = culture.Split('-')[0].ToLowerInvariant();
        return SupportedCultures.Contains(two) ? two : DefaultCulture;
    }

    private Dictionary<string, string> Load(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            _logger.LogWarning("فایل ترجمه {Path} پیدا نشد.", path);
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Flatten(doc.RootElement, prefix: null, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خواندن فایل ترجمه {Path} ناموفق بود.", path);
        }

        return result;
    }

    private static void Flatten(JsonElement element, string? prefix, Dictionary<string, string> sink)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    Flatten(prop.Value, prefix is null ? prop.Name : $"{prefix}.{prop.Name}", sink);
                }
                break;

            case JsonValueKind.String when prefix is not null:
                sink[prefix] = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False when prefix is not null:
                sink[prefix] = element.ToString();
                break;
        }
    }
}
