using System.Security.Cryptography;

namespace Hexara.Application.Rooms;

/// <summary>
/// کد کوتاه دعوت. حروف مبهم (O و 0، I و 1 و L) عمداً حذف شده‌اند چون این کد را
/// آدم‌ها با صدا یا پیامک به هم می‌دهند.
/// </summary>
public static class RoomCode
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public const int Length = 6;

    public static string New()
    {
        return string.Create(Length, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }
        });
    }

    /// <summary>ورودی کاربر را به شکل کانونی می‌آورد تا حروف کوچک و فاصله مهم نباشند.</summary>
    public static string Normalize(string? code) =>
        new((code ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static bool IsWellFormed(string code) =>
        code.Length == Length && code.All(Alphabet.Contains);
}
