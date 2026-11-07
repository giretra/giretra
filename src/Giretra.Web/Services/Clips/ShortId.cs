using System.Security.Cryptography;

namespace Giretra.Web.Services.Clips;

public static class ShortId
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    public const int Length = 10;

    public static string Generate()
    {
        Span<char> chars = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    public static bool IsValid(string? value)
        => value is { Length: Length } && value.All(c => Alphabet.Contains(c));
}
