using ERP.Domain.Modules.Accounting.Entities;

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

    /// <summary>
    /// ACCOUNTING-CHART-INTEGRITY-01 (I1): true si alguna cuenta, activa o inactiva, tiene a
    /// <paramref name="accountId"/> como padre directo — misma noción de «tiene hijas» que
    /// <see cref="AccountHierarchyDiagnostics"/> (invariante 4).
    /// </summary>
    public static bool HasChildren(Guid accountId, IEnumerable<Account> accounts) =>
        accounts.Any(a => a.ParentAccountId == accountId);

    /// <summary>
    /// ACCOUNTING-CHART-INTEGRITY-01 (I3): true si algún descendiente de cualquier nivel está
    /// activo — recorre el subárbol completo (no solo hijas directas) para no depender de que los
    /// datos existentes ya cumplan I4. Protegido contra ciclos.
    /// </summary>
    public static bool HasActiveDescendant(Guid accountId, IEnumerable<Account> accounts)
    {
        var childrenByParent = accounts.ToLookup(a => a.ParentAccountId);
        var visited = new HashSet<Guid> { accountId };
        var pending = new Stack<Guid>();
        pending.Push(accountId);
        while (pending.Count > 0)
        {
            foreach (var child in childrenByParent[pending.Pop()])
            {
                if (!visited.Add(child.Id))
                    continue;
                if (child.IsActive)
                    return true;
                pending.Push(child.Id);
            }
        }
        return false;
    }

    /// <summary>
    /// ACCOUNTING-CHART-INTEGRITY-01 (I4): true si algún ancestro de la cadena completa de
    /// <see cref="Account.ParentAccountId"/> está inactivo. Un padre ausente corta la cadena (no
    /// se presume inactivo). Protegido contra ciclos.
    /// </summary>
    public static bool HasInactiveAncestor(Account account, IEnumerable<Account> accounts) =>
        HasInactiveInChain(account.ParentAccountId, accounts, account.Id);

    /// <summary>
    /// ACCOUNTING-CHART-INTEGRITY-01 (I4 al crear): misma regla para una cuenta todavía no creada —
    /// true si <paramref name="parentAccountId"/> o cualquiera de sus ancestros está inactivo.
    /// </summary>
    public static bool HasInactiveAncestor(Guid? parentAccountId, IEnumerable<Account> accounts) =>
        HasInactiveInChain(parentAccountId, accounts, selfId: null);

    private static bool HasInactiveInChain(Guid? start, IEnumerable<Account> accounts, Guid? selfId)
    {
        var byId = accounts.ToDictionary(a => a.Id);
        var visited = new HashSet<Guid>();
        if (selfId is { } self)
            visited.Add(self);
        var current = start;
        while (current is { } parentId && visited.Add(parentId) && byId.TryGetValue(parentId, out var parent))
        {
            if (!parent.IsActive)
                return true;
            current = parent.ParentAccountId;
        }
        return false;
    }
}
