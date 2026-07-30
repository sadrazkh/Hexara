using System.Globalization;

namespace Hexara.Domain.Board;

/// <summary>
/// مختصات محوری یک هگز با چیدمان «نوک‌تیز به بالا». این نوع آینه‌ی دقیق
/// <c>ClientApp/src/three/hex.ts</c> است؛ هر تغییری در ترتیب جهت‌ها باید در هر دو
/// طرف اعمال شود وگرنه شناسه‌ی گره و یال بین سرور و کلاینت یکی نمی‌ماند.
/// </summary>
public readonly record struct Axial(int Q, int R)
{
    /// <summary>مؤلفه‌ی سوم مختصات مکعبی که همیشه ‎-(q+r)‎ است.</summary>
    public int S => -Q - R;

    /// <summary>شش جهت همسایگی: شرق، شمال‌شرق، شمال‌غرب، غرب، جنوب‌غرب، جنوب‌شرق.</summary>
    public static readonly Axial[] Directions =
    [
        new(1, 0),
        new(1, -1),
        new(0, -1),
        new(-1, 0),
        new(-1, 1),
        new(0, 1)
    ];

    /// <summary>جهت‌ها را به بازه‌ی ۰ تا ۵ می‌آورد تا محاسبات با اندیس منفی هم درست بماند.</summary>
    public static int NormalizeDirection(int direction) => ((direction % 6) + 6) % 6;

    public Axial Neighbor(int direction) => this + Directions[NormalizeDirection(direction)];

    public IEnumerable<Axial> Neighbors()
    {
        for (var d = 0; d < 6; d++)
        {
            yield return Neighbor(d);
        }
    }

    /// <summary>فاصله‌ی محوری بین دو هگز — تعداد گام تا رسیدن.</summary>
    public static int Distance(Axial a, Axial b)
    {
        var dq = a.Q - b.Q;
        var dr = a.R - b.R;
        return (Math.Abs(dq) + Math.Abs(dq + dr) + Math.Abs(dr)) / 2;
    }

    /// <summary>تمام هگزهای یک صفحه‌ی شش‌ضلعی با شعاع داده‌شده (شعاع ۲ ⇒ ۱۹ هگز).</summary>
    public static IEnumerable<Axial> Disc(int radius)
    {
        for (var q = -radius; q <= radius; q++)
        {
            var from = Math.Max(-radius, -q - radius);
            var to = Math.Min(radius, -q + radius);
            for (var r = from; r <= to; r++)
            {
                yield return new Axial(q, r);
            }
        }
    }

    public static Axial operator +(Axial a, Axial b) => new(a.Q + b.Q, a.R + b.R);

    public static Axial operator -(Axial a, Axial b) => new(a.Q - b.Q, a.R - b.R);

    /// <summary>
    /// شناسه است نه متنِ نمایشی، پس با فرهنگ ناوابسته — در فارسی علامت منفی
    /// ‎U+2212‎ می‌شد و همین رشته‌ها به لاگ و پیام خطا و کلید می‌روند.
    /// </summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"({Q},{R})");
}
