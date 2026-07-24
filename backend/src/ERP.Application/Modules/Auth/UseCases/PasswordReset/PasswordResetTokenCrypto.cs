using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Auth.UseCases.PasswordReset;

internal static class PasswordResetTokenCrypto
{
    public static string CreateRawToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string raw)
    {
        var inputBytes = Encoding.UTF8.GetBytes(raw);
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToBase64String(hashBytes);
    }
}
