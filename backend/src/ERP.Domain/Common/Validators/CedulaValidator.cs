namespace ERP.Domain.Common.Validators;

/// <summary>
/// Valida la Cédula de Ciudadanía (CI) ecuatoriana (10 dígitos).
/// Algoritmo: Módulo 10 sobre los primeros 9 dígitos; dígito 10 es verificador.
///
/// Coeficientes: 2 1 2 1 2 1 2 1 2 (posiciones 1-9)
/// Si producto ≥ 10 → restar 9.
/// Suma mod 10 = 0 → válida.
/// </summary>
public static class CedulaValidator
{
    private static readonly int[] Coeficientes = { 2, 1, 2, 1, 2, 1, 2, 1, 2 };

    /// <summary>
    /// Retorna <c>true</c> si la cédula supera todas las validaciones estructurales
    /// y de dígito verificador según el algoritmo del Registro Civil ecuatoriano.
    /// </summary>
    public static bool EsCedulaValida(string cedula)
    {
        if (string.IsNullOrWhiteSpace(cedula)) return false;

        cedula = cedula.Trim();
        if (cedula.Length != 10) return false;
        if (!cedula.All(char.IsDigit)) return false;

        // Provincia: 01-24 (no 30, exclusivo de RUC)
        int provincia = int.Parse(cedula[..2]);
        if (provincia < 1 || provincia > 24) return false;

        // 3.er dígito: 0-5 (natural) o 6 (pública) — nunca 7,8,9 en CI
        int tercerDigito = cedula[2] - '0';
        if (tercerDigito > 6) return false;

        // Módulo 10
        int suma = 0;
        for (int i = 0; i < 9; i++)
        {
            int producto = (cedula[i] - '0') * Coeficientes[i];
            suma += producto >= 10 ? producto - 9 : producto;
        }

        int verificador = (10 - suma % 10) % 10;
        return (cedula[9] - '0') == verificador;
    }
}
