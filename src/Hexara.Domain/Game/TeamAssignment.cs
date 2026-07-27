namespace Hexara.Domain.Game;

/// <summary>
/// کدام صندلی در کدام تیم است. اندیس = صندلی، مقدار = شناسه‌ی تیم.
///
/// عمداً یک نوع جداست و نه یک ‎IReadOnlyList&lt;int&gt;‎ خام: <see cref="GameOptions"/>
/// یک record است و برابری‌اش عضو‌به‌عضو حساب می‌شود؛ یک فهرست خام برابری مرجعی
/// دارد و رفت‌وبرگشت عکس وضعیت را بی‌صدا نابرابر می‌کرد.
/// </summary>
public sealed class TeamAssignment : IEquatable<TeamAssignment>
{
    public TeamAssignment(IReadOnlyList<int> bySeat) => BySeat = [.. bySeat];

    /// <summary>تیم هر صندلی، به ترتیب صندلی.</summary>
    public IReadOnlyList<int> BySeat { get; }

    public int TeamOf(int seat) => BySeat[seat];

    /// <summary>شناسه‌ی تیم‌ها، مرتب‌شده.</summary>
    public IEnumerable<int> Teams => BySeat.Distinct().Order();

    /// <summary>صندلی‌های یک تیم.</summary>
    public IEnumerable<int> SeatsOf(int team) =>
        BySeat.Select((t, seat) => (t, seat)).Where(x => x.t == team).Select(x => x.seat);

    /// <summary>هم‌تیمی‌های یک صندلی، بدون خودش.</summary>
    public IEnumerable<int> Teammates(int seat) => SeatsOf(BySeat[seat]).Where(s => s != seat);

    public bool AreTeammates(int a, int b) => a != b && BySeat[a] == BySeat[b];

    /// <summary>هر صندلی باید تیمی داشته باشد و حداقل دو تیم لازم است.</summary>
    public bool IsValidFor(int playerCount) =>
        BySeat.Count == playerCount
        && BySeat.All(t => t >= 0)
        && BySeat.Distinct().Count() >= 2;

    /// <summary>تقسیم ساده‌ی یک‌درمیان — دو تیم که صندلی‌هایشان درهم است.</summary>
    public static TeamAssignment Alternating(int playerCount) =>
        new([.. Enumerable.Range(0, playerCount).Select(seat => seat % 2)]);

    public bool Equals(TeamAssignment? other) =>
        other is not null && BySeat.SequenceEqual(other.BySeat);

    public override bool Equals(object? obj) => Equals(obj as TeamAssignment);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var team in BySeat)
        {
            hash.Add(team);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => string.Join(",", BySeat);
}
