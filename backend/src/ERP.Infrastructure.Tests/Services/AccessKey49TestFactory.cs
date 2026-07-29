namespace ERP.Infrastructure.Tests.Services;

/// <summary>Genera una clave de acceso de 49 dígitos con dígito verificador válido (misma regla que <see cref="ERP.Infrastructure.Services.SriInvoiceParser"/>).</summary>
internal static class AccessKey49TestFactory
{
    public static string FromPrefix48(string first48Digits)
    {
        if (first48Digits.Length != 48 || !first48Digits.All(char.IsDigit))
            throw new ArgumentException(
                "Se requieren exactamente 48 dígitos.",
                nameof(first48Digits)
            );

        for (var check = 0; check <= 9; check++)
        {
            var candidate = first48Digits + check;
            if (HasValidVerifier(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            "No se pudo calcular dígito verificador para el prefijo dado."
        );
    }

    private static bool HasValidVerifier(string clave)
    {
        var weights = new[] { 2, 3, 4, 5, 6, 7 };
        var sum = 0;
        for (var i = 47; i >= 0; i--)
            sum += (clave[i] - '0') * weights[(47 - i) % 6];

        var residuo = sum % 11;
        var verificador =
            residuo == 0 ? 0
            : residuo == 1 ? 1
            : 11 - residuo;
        var ultimo = clave[48] - '0';
        return verificador == ultimo;
    }
}
