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
        IEnumerable<int> recipients)
    {
        Proposer = proposer;
        Give = give;
        Take = take;
        _responses = recipients.ToDictionary(r => r, _ => TradeResponse.Pending);
    }

    public int Proposer { get; }

    /// <summary>چیزی که پیشنهاددهنده می‌دهد.</summary>
    public IReadOnlyDictionary<Resource, int> Give { get; }

    /// <summary>چیزی که پیشنهاددهنده می‌خواهد.</summary>
    public IReadOnlyDictionary<Resource, int> Take { get; }

    public IReadOnlyDictionary<int, TradeResponse> Responses => _responses;

    public IEnumerable<int> AcceptedBy =>
        _responses.Where(r => r.Value == TradeResponse.Accepted).Select(r => r.Key);

    public bool CanRespond(int playerIndex) => _responses.ContainsKey(playerIndex);

    internal void Respond(int playerIndex, TradeResponse response) => _responses[playerIndex] = response;
}
