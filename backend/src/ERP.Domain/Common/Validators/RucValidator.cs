using System.Globalization;

namespace ERP.Domain.Common.Validators;

/// <summary>
/// Valida el RUC ecuatoriano (13 dígitos) según las reglas vigentes del Servicio de Rentas Internas (SRI).
///
/// Tipos de RUC según el 3.er dígito:
///   0-5 → Persona Natural     → Módulo 10 sobre la cédula (dígitos 1-10) + establecimiento ≠ "000"
///   6   → Entidad Pública     → solo estructura: establecimiento (dígitos 10-13) ≠ "0000"
///   9   → Sociedad Privada    → solo estructura: establecimiento (dígitos 11-13) ≠ "000"
///   7,8 → No asignados        → inválido
///
/// RUC-SOCIETIES-VALIDATION-01: el SRI indica que los RUC de sociedades privadas, sociedades
/// públicas y extranjeros sin cédula NO tienen un algoritmo de validación específico; el antiguo
/// dígito verificador módulo 11 rechazaba RUC reales vigentes (p. ej. 0391034777001). Para tipos 6 y 9
/// solo se validan longitud, formato numérico, provincia, tipo y establecimiento.
/// </summary>
public static class RucValidator
{
    // ── Coefficients SRI ──────────────────────────────────────────────────
    private static readonly int[] CoefPersonaNatural = { 2, 1, 2, 1, 2, 1, 2, 1, 2 };

    /// <summary>
    /// Retorna <c>true</c> si el RUC supera las validaciones estructurales y, para persona natural,
    /// el dígito verificador de la cédula.
    /// </summary>
    public static bool EsRucValido(string ruc)
    {
        if (string.IsNullOrWhiteSpace(ruc))
            return false;

        ruc = ruc.Trim();
        if (ruc.Length != 13)
            return false;
        if (!ruc.All(char.IsDigit))
            return false;

        // Provincia: 01-24 o 30 (Gobierno Nacional)
        int provincia = int.Parse(ruc[..2], CultureInfo.InvariantCulture);
        if (provincia < 1 || (provincia > 24 && provincia != 30))
            return false;

        int tercerDigito = ruc[2] - '0';

        return tercerDigito switch
        {
            >= 0 and <= 5 => ValidarPersonaNatural(ruc),
            6 => ValidarEntidadPublica(ruc),
            9 => ValidarSociedadPrivada(ruc),
            _ => false, // 7 y 8 no asignados
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // Persona Natural — Módulo 10
    // Dígitos 1-9 → suma ponderada → verificador en posición 10
    // Establecimiento en posiciones 11-13 (≠ "000")
    // ─────────────────────────────────────────────────────────────────────
    private static bool ValidarPersonaNatural(string ruc)
    {
        int suma = 0;
        for (int i = 0; i < 9; i++)
        {
            int producto = (ruc[i] - '0') * CoefPersonaNatural[i];
            suma += producto >= 10 ? producto - 9 : producto;
        }

        int verificador = (10 - suma % 10) % 10;
        return (ruc[9] - '0') == verificador && ruc[10..] != "000";
    }

    // ─────────────────────────────────────────────────────────────────────
    // Sociedad Privada — sin dígito verificador (regla SRI vigente)
    // Establecimiento en posiciones 11-13 (≠ "000")
    // ─────────────────────────────────────────────────────────────────────
    private static bool ValidarSociedadPrivada(string ruc) => ruc[10..] != "000";

    // ─────────────────────────────────────────────────────────────────────
    // Entidad Pública — sin dígito verificador (regla SRI vigente)
    // Establecimiento en posiciones 10-13 (≠ "0000")
    // ─────────────────────────────────────────────────────────────────────
    private static bool ValidarEntidadPublica(string ruc) => ruc[9..] != "0000";
}
