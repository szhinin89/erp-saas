using System.Security.Cryptography;

namespace ERP.Application.Auth;

/// <summary>
/// Genera contraseñas temporales con <see cref="RandomNumberGenerator"/> (CSPRNG). Cumple la
/// política única de <see cref="PasswordComplexityRules"/> (mínimo 8, una mayúscula, un dígito) y
/// usa un alfabeto sin caracteres ambiguos (0/O, 1/l/I) ni símbolos, para que se pueda copiar o
/// dictar sin errores. La contraseña nunca se persiste ni se registra: solo viaja al hash.
/// </summary>
public static class TemporaryPasswordGenerator
{
    public const int Length = 16;

    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string All = Upper + Lower + Digits;

    public static string Generate()
    {
        var chars = new char[Length];
        // Garantiza al menos una mayúscula, una minúscula y un dígito.
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        for (var i = 3; i < Length; i++)
            chars[i] = Pick(All);

        // Fisher–Yates con CSPRNG para que las posiciones garantizadas no sean predecibles.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static char Pick(string alphabet) =>
        alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
