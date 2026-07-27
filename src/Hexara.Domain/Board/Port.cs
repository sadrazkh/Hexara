namespace Hexara.Domain.Board;

/// <summary>
/// بندر روی یک ضلع ساحلی. اگر <see cref="Resource"/> مقدار داشته باشد بندر
/// اختصاصی ۲:۱ است، وگرنه بندر عمومی ۳:۱.
///
/// قوانین معامله در فاز ۲ب پیاده می‌شود؛ اینجا فقط داده‌ی چیدمان است تا برد از
/// همان ابتدا کامل ساخته شود و بعداً نیاز به تغییر تولیدکننده‌ی برد نباشد.
/// </summary>
public sealed record Port(EdgeId Edge, Resource? Resource)
{
    public bool IsGeneric => Resource is null;

    /// <summary>نرخ تبدیل: ۲ برای بندر اختصاصی، ۳ برای عمومی.</summary>
    public int Rate => Resource is null ? 3 : 2;

    /// <summary>دو گوشه‌ای که با ساختن آبادی روی آن‌ها بندر فعال می‌شود.</summary>
    public IEnumerable<VertexId> Vertices() => Edge.Endpoints();
}
