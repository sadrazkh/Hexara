namespace Hexara.Domain.Common;

/// <summary>
/// مولد عدد تصادفی قابل بازتولید.
///
/// عمداً از <c>System.Random</c> استفاده نمی‌کنیم: الگوریتم داخلی آن تضمین‌شده
/// نیست و بین نسخه‌های دات‌نت تغییر کرده است. بازپخش بازی و تولید برد از روی seed
/// وقتی معنا دارد که دنباله‌ی اعداد تا ابد یکسان بماند، پس xorshift را خودمان
/// پیاده می‌کنیم. وضعیت مولد بخشی از وضعیت بازی است و ذخیره می‌شود.
/// </summary>
public sealed class Rng
{
    private ulong _state;

    public Rng(ulong seed)
    {
        // seed صفر برای xorshift مرگبار است (همیشه صفر می‌ماند)؛ با splitmix پخش می‌شود.
        _state = SplitMix(seed == 0 ? 0x9E3779B97F4A7C15UL : seed);
        if (_state == 0)
        {
            _state = 0x9E3779B97F4A7C15UL;
        }
    }

    private Rng(ulong state, bool _) => _state = state;

    /// <summary>وضعیت فعلی مولد — برای ذخیره در دیتابیس و ادامه‌ی دقیق بازی.</summary>
    public ulong State => _state;

    public static Rng FromState(ulong state) => new(state == 0 ? 0x9E3779B97F4A7C15UL : state, true);

    public ulong NextUInt64()
    {
        // xorshift64*
        var x = _state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        _state = x;
        return x * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>عدد تصادفی در بازه‌ی ‎[0, exclusiveMax)‎ بدون سوگیری پیمانه‌ای.</summary>
    public int Next(int exclusiveMax)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMax);

        var bound = (ulong)exclusiveMax;
        var limit = ulong.MaxValue - (ulong.MaxValue % bound) - 1;

        ulong value;
        do
        {
            value = NextUInt64();
        }
        while (value > limit);

        return (int)(value % bound);
    }

    /// <summary>عدد تصادفی در بازه‌ی ‎[min, exclusiveMax)‎.</summary>
    public int Next(int min, int exclusiveMax) => min + Next(exclusiveMax - min);

    /// <summary>یک تاس شش‌وجهی.</summary>
    public int RollDie() => Next(1, 7);

    /// <summary>درهم‌ریزی فیشر–ییتس در جا.</summary>
    public void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>پخش‌کردن seed تا seedهای نزدیک به هم بردهای شبیه هم نسازند.</summary>
    private static ulong SplitMix(ulong seed)
    {
        var z = seed + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
