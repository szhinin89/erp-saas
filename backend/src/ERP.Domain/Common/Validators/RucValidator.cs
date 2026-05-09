namespace ERP.Domain.Common.Validators;

/// <summary>
/// Valida el RUC ecuatoriano (13 dígitos) según el algoritmo del Servicio de Rentas Internas (SRI).
///
/// Tipos de RUC según el 3.er dígito:
///   0-5 → Persona Natural     → Módulo 10 (misma base que la cédula)
///   6   → Entidad Pública     → Módulo 11 (8 coeficientes)
///   9   → Sociedad Privada    → Módulo 11 (9 coeficientes)
///   7,8 → No asignados        → inválido
/// </summary>
public static class RucValidator
{
    // ── Coeficientes SRI ──────────────────────────────────────────────────
    private static readonly int[] CoefPersonaNatural  = { 2, 1, 2, 1, 2, 1, 2, 1, 2 };
    private static readonly int[] CoefSociedadPrivada = { 4, 3, 2, 7, 6, 5, 4, 3, 2 };
    private static readonly int[] CoefEntidadPublica  = { 3, 2, 7, 6, 5, 4, 3, 2 };

    /// <summary>
    /// Retorna <c>true</c> si el RUC supera todas las validaciones estructurales y de dígito verificador.
    /// </summary>
    public static bool EsRucValido(string ruc)
    {
        if (string.IsNullOrWhiteSpace(ruc)) return false;

        ruc = ruc.Trim();
        if (ruc.Length != 13) return false;
        if (!ruc.All(char.IsDigit)) return false;

        // Provincia: 01-24 o 30 (Gobierno Nacional)
        int provincia = int.Parse(ruc[..2]);
        if (provincia < 1 || (provincia > 24 && provincia != 30)) return false;

        int tercerDigito = ruc[2] - '0';

        return tercerDigito switch
        {
            >= 0 and <= 5 => ValidarPersonaNatural(ruc),
            6             => ValidarEntidadPublica(ruc),
            9             => ValidarSociedadPrivada(ruc),
            _             => false   // 7 y 8 no asignados
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
        return (ruc[9] - '0') == verificador
            && ruc[10..] != "000";
    }

    // ─────────────────────────────────────────────────────────────────────
    // Sociedad Privada — Módulo 11 (9 coeficientes)
    // Dígitos 1-9 → suma ponderada → verificador en posición 10
    // Establecimiento en posiciones 11-13 (≠ "000")
    // ─────────────────────────────────────────────────────────────────────
    private static bool ValidarSociedadPrivada(string ruc)
    {
        int suma = 0;
        for (int i = 0; i < 9; i++)
            suma += (ruc[i] - '0') * CoefSociedadPrivada[i];

        int residuo = suma % 11;
        if (residuo == 10) return false; // dígito verificador inválido en M11
        int verificador = residuo == 0 ? 0 : 11 - residuo;

        return (ruc[9] - '0') == verificador
            && ruc[10..] != "000";
    }

    // ─────────────────────────────────────────────────────────────────────
    // Entidad Pública — Módulo 11 (8 coeficientes)
    // Dígitos 1-8 → suma ponderada → verificador en posición 9
    // Establecimiento en posiciones 10-13 (≠ "0000")
    // ─────────────────────────────────────────────────────────────────────
    private static bool ValidarEntidadPublica(string ruc)
    {
        int suma = 0;
        for (int i = 0; i < 8; i++)
            suma += (ruc[i] - '0') * CoefEntidadPublica[i];

        int residuo = suma % 11;
        if (residuo == 10) return false;
        int verificador = residuo == 0 ? 0 : 11 - residuo;

        return (ruc[8] - '0') == verificador
            && ruc[9..] != "0000";
    }
}
