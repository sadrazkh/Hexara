using System.Security.Cryptography;
using System.Text;
using Hexara.Domain.Board;

namespace Hexara.Domain.Tests.Board;

/// <summary>
/// اثر انگشت شناسه‌های کانونی برد کلاسیک.
///
/// همین رشته در تست‌های سمت کلاینت (‎ClientApp/src/three/hex.test.ts‎) هم بررسی
/// می‌شود. اگر روزی قاعده‌ی کانونی‌سازی یک طرف عوض شود، هر دو تست با هم قرمز
/// می‌شوند — و این تنها چیزی است که جلوی «کلیک روی گوشه‌ای که سرور جای دیگری
/// می‌شناسد» را می‌گیرد.
/// </summary>
public class CanonicalFingerprintTests
{
    private const int Radius = 2;

    [Fact]
    public void The_canonical_vertex_set_has_not_moved()
    {
        Assert.Equal("54:7f85baa2ae18b258", Fingerprint(Vertices()));
    }

    [Fact]
    public void The_canonical_edge_set_has_not_moved()
    {
        Assert.Equal("72:28cda893d823f973", Fingerprint(Edges()));
    }

    /// <summary>چند مقدار مشخص که در تست کلاینت هم عیناً آمده‌اند.</summary>
    [Fact]
    public void Named_samples_stay_put()
    {
        Assert.Equal("0,0,0", VertexId.Of(new Axial(0, 0), 0).ToString()[1..]);
        Assert.Equal("-1,0,5", VertexId.Of(new Axial(0, 0), 3).ToString()[1..]);
        Assert.Equal("0,0,0", VertexId.Of(new Axial(1, 0), 2).ToString()[1..]);
        Assert.Equal("0,0,0", VertexId.Of(new Axial(1, -1), 4).ToString()[1..]);

        Assert.Equal("0,0,0", EdgeId.Of(new Axial(0, 0), 0).ToString()[1..]);
        Assert.Equal("0,0,0", EdgeId.Of(new Axial(1, 0), 3).ToString()[1..]);
        Assert.Equal("-1,0,0", EdgeId.Of(new Axial(0, 0), 3).ToString()[1..]);
    }

    private static IEnumerable<string> Vertices() =>
        Axial.Disc(Radius)
            .SelectMany(h => Enumerable.Range(0, 6).Select(c => VertexId.Of(h, c)))
            .Distinct()
            .Select(v => $"{v.Hex.Q},{v.Hex.R},{v.Corner}");

    private static IEnumerable<string> Edges() =>
        Axial.Disc(Radius)
            .SelectMany(h => Enumerable.Range(0, 6).Select(s => EdgeId.Of(h, s)))
            .Distinct()
            .Select(e => $"{e.Hex.Q},{e.Hex.R},{e.Side}");

    /// <summary>تعداد به‌علاوه‌ی هش مجموعه‌ی مرتب‌شده — کوتاه و بدون ابهام.</summary>
    private static string Fingerprint(IEnumerable<string> ids)
    {
        var sorted = ids.Order(StringComparer.Ordinal).ToList();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", sorted)));

        return $"{sorted.Count}:{Convert.ToHexString(hash)[..16].ToLowerInvariant()}";
    }
}
