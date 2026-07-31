using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hexara.Application.Common.Interfaces;

namespace Hexara.Web.Realtime;

/// <summary>
/// تنظیمات صدا و تصویر.
///
/// **کلید و رمز هرگز در فایلِ کامیت‌شده نمی‌نشینند.** پیش‌فرضشان خالی است و از
/// متغیر محیطی یا user-secrets می‌آیند؛ اگر خالی بمانند، صدا و تصویر خودبه‌خود
/// خاموش می‌ماند و بازی هیچ فرقی نمی‌کند.
/// </summary>
public sealed class LiveKitOptions
{
    public const string Section = "LiveKit";

    public bool Enabled { get; set; }

    /// <summary>نشانی وب‌سوکتِ سرور LiveKit، مثل ‎wss://live.example.com‎.</summary>
    public string Url { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// عمر بلیت. کوتاه است چون بلیت فقط برای *وصل شدن* لازم است و بعدش خودِ
    /// LiveKit نشست را نگه می‌دارد؛ بلیتِ درازعمر یعنی چیزی که لو رفتنش گران است.
    /// </summary>
    public int TokenMinutes { get; set; } = 30;

    /// <summary>بی کلید و رمز و نشانی، «روشن» بودن معنایی ندارد.</summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(Url)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ApiSecret);
}

/// <summary>بلیت ورود به اتاق صوتی: نشانی سرور و خودِ بلیت.</summary>
public sealed record VoiceTicket(string Url, string Token, string Room);

/// <summary>
/// ساختِ بلیت ورود به اتاق LiveKit.
///
/// **چرا دستی و بدون پکیج:** بلیتِ LiveKit یک ‎JWT‎ ساده با امضای ‎HS256‎ است و
/// همه‌ی چیزی که لازم دارد در خودِ ‎.NET‎ هست. آوردن یک SDK فقط برای ساختن سه
/// جفت کلید-مقدار، یک وابستگیِ تازه به ازای هیچ بود.
///
/// دو قاعده‌ی امنیتی که اینجا رعایت شده‌اند:
/// ۱. نامِ اتاق را **سرور** می‌سازد، نه کلاینت — وگرنه هر کسی می‌توانست بلیتِ
///    اتاقِ یک بازیِ دیگر بگیرد.
/// ۲. شناسه‌ی شرکت‌کننده همان شناسه‌ی کاربر است، پس دو تب یک کاربر در LiveKit
///    یکی حساب می‌شوند و کسی نمی‌تواند خودش را چند نفر جا بزند.
/// </summary>
public sealed class LiveKitTokens
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly LiveKitOptions _options;
    private readonly IClock _clock;

    public LiveKitTokens(LiveKitOptions options, IClock clock)
    {
        _options = options;
        _clock = clock;
    }

    public bool IsConfigured => _options.IsConfigured;

    /// <summary>نامِ اتاق هر بازی. از شناسه‌ی بازی ساخته می‌شود و بس.</summary>
    public static string RoomOf(Guid gameId) => $"game-{gameId:N}";

    /// <summary>
    /// بلیت این کاربر برای اتاق این بازی؛ تهی یعنی صدا و تصویر پیکربندی نشده.
    ///
    /// این متد **اجازه نمی‌دهد**، فقط بلیت می‌سازد. بررسی اینکه این کاربر واقعاً
    /// سرِ این بازی نشسته، پیش از صدا زدنِ این انجام می‌شود.
    /// </summary>
    public VoiceTicket? Issue(Guid gameId, Guid userId, string displayName)
    {
        if (!_options.IsConfigured)
        {
            return null;
        }

        var room = RoomOf(gameId);
        var now = _clock.UtcNow;

        var payload = new Dictionary<string, object>
        {
            ["iss"] = _options.ApiKey,
            ["sub"] = userId.ToString(),
            ["name"] = displayName,

            // یک دقیقه عقب‌تر، تا ساعتِ کمی جلوترِ کلاینت بلیت را باطل نکند.
            ["nbf"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(Math.Max(1, _options.TokenMinutes)).ToUnixTimeSeconds(),
            ["video"] = new Dictionary<string, object>
            {
                ["room"] = room,
                ["roomJoin"] = true,
                ["canPublish"] = true,
                ["canSubscribe"] = true,

                // داده از راه LiveKit رد و بدل نمی‌شود؛ هر چیزی که به بازی مربوط
                // است باید از هابِ خودمان برود تا سرور مرجع بماند.
                ["canPublishData"] = false
            }
        };

        return new VoiceTicket(_options.Url, Sign(payload, _options.ApiSecret), room);
    }

    /// <summary>یک ‎JWT‎ با امضای ‎HS256‎.</summary>
    internal static string Sign(IReadOnlyDictionary<string, object> payload, string secret)
    {
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, string> { ["alg"] = "HS256", ["typ"] = "JWT" }, Json));

        var body = Encode(JsonSerializer.SerializeToUtf8Bytes(payload, Json));
        var signed = $"{header}.{body}";

        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signed));

        return $"{signed}.{Encode(signature)}";
    }

    private static string Encode(ReadOnlySpan<byte> bytes) => Base64Url.EncodeToString(bytes);
}
