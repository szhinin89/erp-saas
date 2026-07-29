using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Setup;

/// <summary>
/// Generates and hashes the First-Run setup token.
/// Only the SHA-256 hash is ever persisted — the raw token is shown once in the console.
/// </summary>
public static class SetupTokenCrypto
{
    public const int RawTokenByteLength = 32;

    public static string CreateRawToken()
    {
        var bytes = new byte[RawTokenByteLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    public static string Hash(string raw)
    {
        var inputBytes = Encoding.UTF8.GetBytes(raw);
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes);
    }

    public static bool Matches(string raw, string hash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(raw)),
            Encoding.UTF8.GetBytes(hash)
        );
}
