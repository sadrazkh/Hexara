using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Hexara.Web.Realtime;

/// <summary>تنظیمات چت؛ از ‎appsettings‎ خوانده می‌شود.</summary>
public sealed class ChatOptions
{
    public const string Section = "Chat";

    /// <summary>
    /// خاموشش که کنی، چت کاملاً از رابط و از هاب می‌رود و بازی دست نمی‌خورد.
    ///
    /// همان قاعده‌ای که برای صدا و تصویر هم گذاشته‌ایم: هیچ‌کدامِ این‌ها نباید
    /// شرطِ ادامه‌ی بازی باشند.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>سقف طول یک پیام؛ بلندتر از این بریده می‌شود.</summary>
    public int MaxLength { get; set; } = 300;

    /// <summary>چند پیام آخر برای تازه‌رسیده‌ها نگه داشته شود.</summary>
    public int HistorySize { get; set; } = 60;

    /// <summary>سقف پیام در هر پنجره‌ی زمانی، برای هر بازیکن در هر بازی.</summary>
    public int BurstLimit { get; set; } = 8;

    /// <summary>طول پنجره‌ی محدودیت به ثانیه.</summary>
    public int BurstSeconds { get; set; } = 10;
}

/// <summary>
/// تنظیمات تماشا.
///
/// جدا از چت است چون سؤالِ متفاوتی جواب می‌دهد: چت درباره‌ی حرف‌زدنِ بازیکن‌هاست
/// و این درباره‌ی اینکه چه کسی اجازه‌ی *دیدن* دارد. خاموشش که کنی، کسی که سرِ
/// بازی نیست نه صفحه را می‌گیرد نه به هاب راه دارد.
/// </summary>
public sealed class SpectatorOptions
{
    public const string Section = "Spectators";

    public bool Enabled { get; set; } = true;
}

/// <summary>یک پیام چت، همان‌طور که روی سیم می‌رود.</summary>
/// <param name="Seat">
/// صندلیِ فرستنده، نه نامش. نام از روی همان نمای بازی درمی‌آید که کلاینت دارد،
/// پس کسی نمی‌تواند با یک نامِ ساختگی خودش را جای دیگری جا بزند.
/// </param>
public sealed record ChatMessage(long Id, int Seat, string Text, DateTimeOffset SentAt);

/// <summary>
/// چتِ داخل بازی.
///
/// **در حافظه است و عمداً.** پیام‌ها در دیتابیس ذخیره نمی‌شوند: تاریخچه‌ی چتِ یک
/// دستِ بازی بعد از تمام شدنش ارزشی ندارد، و یک جدولِ تازه یعنی مهاجرت، سیاست
/// نگه‌داری و یک جای دیگر که باید پاک شود. در عوض چند ده پیامِ آخر نگه داشته
/// می‌شود تا کسی که صفحه را تازه می‌کند یا اتصالش قطع و وصل می‌شود گفت‌وگو را از
/// دست ندهد. با ری‌استارت سرور تاریخچه می‌رود — که برای چتِ یک بازی قابل قبول است.
///
/// مثل <see cref="GamePresence"/> با بیش از یک نمونه‌ی سرور کار نمی‌کند و برای
/// افقی شدن به همان backplane نیاز دارد.
/// </summary>
public sealed partial class GameChat
{
    private readonly ChatOptions _options;
    private readonly ConcurrentDictionary<Guid, Room> _rooms = new();
    private long _lastId;

    public GameChat(ChatOptions options) => _options = options;

    public bool Enabled => _options.Enabled;

    /// <summary>
    /// پیام را می‌پذیرد یا رد می‌کند.
    ///
    /// تهی یعنی نپذیرفت — یا متن بعد از تمیز شدن چیزی نماند، یا این بازیکن تندتر
    /// از حدِ مجاز پیام می‌فرستد. رد شدن هرگز استثنا نمی‌دهد، چون خرابیِ چت نباید
    /// به بازی سرایت کند.
    /// </summary>
    public ChatMessage? Post(Guid gameId, int seat, string? text, DateTimeOffset now)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        var clean = Clean(text, _options.MaxLength);
        if (clean.Length == 0)
        {
            return null;
        }

        var room = _rooms.GetOrAdd(gameId, _ => new Room());

        lock (room.Gate)
        {
            if (!room.Allows(seat, now, _options.BurstLimit, TimeSpan.FromSeconds(_options.BurstSeconds)))
            {
                return null;
            }

            var message = new ChatMessage(Interlocked.Increment(ref _lastId), seat, clean, now);
            room.History.Enqueue(message);

            while (room.History.Count > Math.Max(1, _options.HistorySize))
            {
                room.History.TryDequeue(out _);
            }

            return message;
        }
    }

    /// <summary>پیام‌های اخیر همین بازی، از قدیم به جدید.</summary>
    public IReadOnlyList<ChatMessage> History(Guid gameId)
    {
        if (!_options.Enabled || !_rooms.TryGetValue(gameId, out var room))
        {
            return [];
        }

        lock (room.Gate)
        {
            return [.. room.History];
        }
    }

    /// <summary>بازی که تمام شد، حافظه‌اش هم آزاد می‌شود.</summary>
    public void Forget(Guid gameId) => _rooms.TryRemove(gameId, out _);

    /// <summary>
    /// گفت‌وگو را از یک شناسه به شناسه‌ی دیگر می‌برد.
    ///
    /// برای لحظه‌ی شروع بازی: تا یک ثانیه پیش در اتاق انتظار داشتند هماهنگ
    /// می‌کردند و اگر تاریخچه جا می‌ماند، صفحه‌ی بازی با یک چتِ خالی باز می‌شد و
    /// نصفِ حرف‌ها گم می‌شد.
    ///
    /// اگر مقصد از قبل گفت‌وگویی داشته باشد دست نمی‌خورد — بازی همیشه از اتاق
    /// می‌آید و تازه است، ولی جای بازنویسیِ بی‌صدا اینجا نیست.
    /// </summary>
    public void Move(Guid from, Guid to)
    {
        if (!_options.Enabled || from == to || !_rooms.TryRemove(from, out var room))
        {
            return;
        }

        _rooms.TryAdd(to, room);
    }

    /// <summary>
    /// متن را به چیزی تبدیل می‌کند که امن و خواندنی باشد.
    ///
    /// نویسه‌های کنترلی برداشته می‌شوند و هر رشته فاصله (از جمله خط تازه) به یک
    /// فاصله تبدیل می‌شود: بی این کار یک پیامِ سیصد خطِ خالی می‌توانست کلِ پنل را
    /// از دستِ بقیه دربیاورد. متن **بدون** فرار دادنِ HTML ذخیره می‌شود، چون
    /// کلاینت آن را به‌عنوان متن می‌گذارد نه HTML؛ فرار دادنِ دوباره فقط باعث
    /// می‌شد کاربر ‎&amp;amp;‎ ببیند.
    ///
    /// **نیم‌فاصله و اتصالِ اجباری استثنا هستند.** نسخه‌ی اول همه‌ی نویسه‌های
    /// دسته‌ی ‎Cf‎ را می‌برد و «می‌کنم» را «می کنم» می‌کرد — در برنامه‌ای که زبان
    /// اولش فارسی است، این خرابیِ متن است نه پاک‌سازی. آنچه واقعاً خطرناک است
    /// نویسه‌های وارونه‌کننده‌ی جهت‌اند (‎U+202E‎ و خویشانش) و همان‌ها می‌روند.
    /// </summary>
    internal static string Clean(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var stripped = ControlCharacters().Replace(text, " ");
        var collapsed = Whitespace().Replace(stripped, " ").Trim();

        return collapsed.Length <= maxLength ? collapsed : collapsed[..maxLength].TrimEnd();
    }

    // ‎‌‎ نیم‌فاصله و ‎‍‎ اتصالِ اجباری‌اند و از دسته بیرون گذاشته می‌شوند.
    // با گریزِ صریح نوشته شده‌اند نه با خودِ نویسه: نویسه‌شان نامرئی است و کسی که
    // بعداً این خط را می‌خواند باید *ببیند* چه چیزی استثنا شده.
    [GeneratedRegex("[\\p{Cc}\\p{Cf}-[\\u200C\\u200D]]")]
    private static partial Regex ControlCharacters();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>تاریخچه و ضربان‌شمارِ یک بازی.</summary>
    private sealed class Room
    {
        public object Gate { get; } = new();

        public ConcurrentQueue<ChatMessage> History { get; } = new();

        private readonly Dictionary<int, Queue<DateTimeOffset>> _recent = [];

        /// <summary>
        /// پنجره‌ی لغزان: زمانِ پیام‌های اخیرِ همین صندلی نگه داشته می‌شود و
        /// هرچه از پنجره بیرون افتاد دور ریخته می‌شود.
        /// </summary>
        public bool Allows(int seat, DateTimeOffset now, int limit, TimeSpan window)
        {
            if (!_recent.TryGetValue(seat, out var stamps))
            {
                stamps = new Queue<DateTimeOffset>();
                _recent[seat] = stamps;
            }

            while (stamps.Count > 0 && now - stamps.Peek() > window)
            {
                stamps.Dequeue();
            }

            if (stamps.Count >= Math.Max(1, limit))
            {
                return false;
            }

            stamps.Enqueue(now);
            return true;
        }
    }
}
