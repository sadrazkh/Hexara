using Hexara.Domain.Board;

namespace Hexara.Domain.Game;

/// <summary>پاسخ یک بازیکن به پیشنهاد معامله.</summary>
public enum TradeResponse
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2
}

/// <summary>
/// پیشنهاد معامله‌ی روی میز.
///
/// جریان معامله عمداً داخل دامنه است، نه در لایه‌ی وب: رضایت طرف مقابل باید سمت
/// سرور ثابت شود، وگرنه یک کلاینت دستکاری‌شده می‌تواند منابع بقیه را جابه‌جا کند.
/// </summary>
public sealed class TradeOffer
{
    private readonly Dictionary<int, TradeResponse> _responses;

    public TradeOffer(
        int proposer,
        IReadOnlyDictionary<Resource, int> give,
        IReadOnlyDictionary<Resource, int> take,
        IEnumerable<int> recipients,
        DateTimeOffset? expiresAt = null)
    {
        Proposer = proposer;
        Give = give;
        Take = take;
        ExpiresAt = expiresAt;
        _responses = recipients.ToDictionary(r => r, _ => TradeResponse.Pending);
    }

    public int Proposer { get; }

    /// <summary>چیزی که پیشنهاددهنده می‌دهد.</summary>
    public IReadOnlyDictionary<Resource, int> Give { get; }

    /// <summary>چیزی که پیشنهاددهنده می‌خواهد.</summary>
    public IReadOnlyDictionary<Resource, int> Take { get; }

    /// <summary>
    /// لحظه‌ی پایانِ مهلت؛ تهی یعنی بی‌مهلت.
    ///
    /// زمان از بیرون می‌آید و دامنه هرگز ساعت نمی‌خواند — همان قاعده‌ای که
    /// ‎Rng‎ را هم دست‌ساز نگه داشته: بازپخشِ یک بازی باید همان نتیجه را بدهد.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; }

    public IReadOnlyDictionary<int, TradeResponse> Responses => _responses;

    /// <summary>مهلت گذشته است؟ بی‌زمان یعنی هرگز.</summary>
    public bool HasExpired(DateTimeOffset? now) =>
        ExpiresAt is { } deadline && now is { } moment && moment > deadline;

    public IEnumerable<int> AcceptedBy =>
        _responses.Where(r => r.Value == TradeResponse.Accepted).Select(r => r.Key);

    public bool CanRespond(int playerIndex) => _responses.ContainsKey(playerIndex);

    internal static TradeOffer Restore(TradeOfferSnapshot snapshot)
    {
        var offer = new TradeOffer(
            snapshot.Proposer,
            snapshot.Give,
            snapshot.Take,
            snapshot.Responses.Keys,
            snapshot.ExpiresAt);
        foreach (var (playerIndex, response) in snapshot.Responses)
        {
            offer._responses[playerIndex] = response;
        }

        return offer;
    }

    internal void Respond(int playerIndex, TradeResponse response) => _responses[playerIndex] = response;
}
