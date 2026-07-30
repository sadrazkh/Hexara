using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Application.Games;

/// <summary>
/// قالب JSON مشترک برای ذخیره‌ی عکس وضعیت، حرکت‌ها و رویدادها.
///
/// تنظیمات عمداً اینجاست و نه در دامنه: <c>Hexara.Domain</c> نباید بداند با چه
/// قالبی ذخیره می‌شود. سلسله‌مراتب <see cref="GameAction"/> و <see cref="GameEvent"/>
/// هم با یک اصلاح‌کننده‌ی زمان اجرا چندریختی می‌شود، تا اضافه شدن هر حرکت جدید در
/// فازهای بعد نیاز به دست زدن به دامنه نداشته باشد.
/// </summary>
public static class GameJson
{
    public static readonly JsonSerializerOptions Options = Build();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException($"خواندن JSON به {typeof(T).Name} ناموفق بود.");

    private static JsonSerializerOptions Build()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(AddPolymorphism);

        return new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                // enumها با نام نوشته می‌شوند نه عدد: هم ستون jsonb با چشم خواندنی
                // می‌ماند، هم اضافه شدن یک عضو وسط enum داده‌های قدیمی را خراب نمی‌کند.
                new JsonStringEnumConverter(),
                new AxialConverter(),
                new VertexIdConverter(),
                new EdgeIdConverter()
            }
        };
    }

    private static void AddPolymorphism(JsonTypeInfo info)
    {
        if (info.Type != typeof(GameAction) && info.Type != typeof(GameEvent))
        {
            return;
        }

        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "$kind",
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
        };

        // نام نوع به عنوان شناسه استفاده می‌شود، پس تغییر نام یک حرکت شکستن داده است.
        foreach (var derived in info.Type.Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && info.Type.IsAssignableFrom(t) && t != info.Type)
            .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            options.DerivedTypes.Add(new JsonDerivedType(derived, derived.Name));
        }

        info.PolymorphismOptions = options;
    }
}

/// <summary>
/// مختصات و شناسه‌های هندسی به صورت رشته‌ی فشرده ذخیره می‌شوند. گوشه و ضلع سازنده‌ی
/// عمومی ندارند (چون باید کانونی ساخته شوند)، پس بدون این مبدل‌ها اصلاً قابل
/// بازخوانی نیستند.
/// </summary>
internal sealed class AxialConverter : JsonConverter<Axial>
{
    public override Axial Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var parts = ReadParts(ref reader, 2);
        return new Axial(parts[0], parts[1]);
    }

    public override void Write(Utf8JsonWriter writer, Axial value, JsonSerializerOptions options) =>
        writer.WriteStringValue(Join(value.Q, value.R));

    /// <summary>
    /// شناسه‌ی هندسی یک مقدار ماشینی است، پس همیشه با فرهنگ ناوابسته ساخته می‌شود.
    ///
    /// چرا صریح: علامت منفی در فرهنگ فارسی ‎ASCII‎ نیست — ‎U+200E U+2212‎ است، نه
    /// ‎U+002D‎. با فرهنگ جاری، ‎q = -1‎ رشته‌ی ‎«‎−1»‎ می‌شد و همان رشته در دیتابیس و
    /// در کلاینت می‌نشست، و خواندنش هم استثنا می‌داد. مختصات محوری منفی فراوان است،
    /// پس این نشتی در زبان فارسی هر ساخت‌وساز روی نیمه‌ی منفی برد را می‌شکست.
    /// </summary>
    internal static string Join(params int[] parts) =>
        string.Join(',', parts.Select(p => p.ToString(CultureInfo.InvariantCulture)));

    internal static int[] ReadParts(ref Utf8JsonReader reader, int expected)
    {
        var raw = reader.GetString() ?? throw new JsonException("مقدار هندسی تهی است.");
        var parts = raw.Split(',');
        if (parts.Length != expected)
        {
            throw new JsonException($"مقدار هندسی «{raw}» باید {expected} جزء داشته باشد.");
        }

        return [.. parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture))];
    }
}

internal sealed class VertexIdConverter : JsonConverter<VertexId>
{
    public override VertexId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var parts = AxialConverter.ReadParts(ref reader, 3);
        return VertexId.Of(new Axial(parts[0], parts[1]), parts[2]);
    }

    public override void Write(Utf8JsonWriter writer, VertexId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(AxialConverter.Join(value.Hex.Q, value.Hex.R, value.Corner));
}

internal sealed class EdgeIdConverter : JsonConverter<EdgeId>
{
    public override EdgeId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var parts = AxialConverter.ReadParts(ref reader, 3);
        return EdgeId.Of(new Axial(parts[0], parts[1]), parts[2]);
    }

    public override void Write(Utf8JsonWriter writer, EdgeId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(AxialConverter.Join(value.Hex.Q, value.Hex.R, value.Side));
}
