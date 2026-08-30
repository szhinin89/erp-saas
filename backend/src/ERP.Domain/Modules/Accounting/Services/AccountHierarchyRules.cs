namespace ERP.Domain.Modules.Accounting.Services;

/// <summary>
/// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01: regla canónica del Plan de Cuentas — el código
/// contable manda la jerarquía. "1" es raíz (Level 0); "1.1" implica padre "1" (Level 1);
/// "1.1.01" implica padre "1.1" (Level 2); "1.1.01.001" implica padre "1.1.01" (Level 3). Helpers
/// puros y reutilizables por bootstrap/backfill (Infrastructure) y diagnóstico/reportes
/// (Application) sin duplicar la regla de parseo de segmentos en cada capa.
/// </summary>
public static class AccountHierarchyRules
{
    /// <summary>
    /// Código del padre inmediato implicado por <paramref name="code"/> (prefijo sin el último
    /// segmento), o null si <paramref name="code"/> es de un solo segmento (cuenta raíz).
    /// </summary>
    public static string? GetExpectedParentCode(string code)
    {
        var lastDot = code.LastIndexOf('.');
        return lastDot < 0 ? null : code[..lastDot];
    }

    /// <summary>Profundidad implicada por el código: cantidad de segmentos menos 1 (raíz = 0).</summary>
    public static int GetCodeDepth(string code) => code.Count(c => c == '.');
}
