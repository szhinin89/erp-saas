using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX02 (P1-4) — punto único sancionado para bypasses de los filtros
/// globales de EF (<see cref="EnterpriseQueryFilterConfigurator"/>). backend/CLAUDE.md § Seguridad
/// prohíbe <c>.IgnoreQueryFilters()</c> sin justificación salvo a través de este accessor;
/// <c>IgnoreQueryFiltersAuditTests</c> hace cumplir esta regla vía allowlist de archivos — este
/// archivo ya estaba pre-registrado en esa allowlist como el destino esperado del wrapper. Cada
/// caller sigue siendo responsable de re-aplicar cualquier filtro explícito que necesite (p. ej.
/// TenantId) — este método NO reemplaza esa responsabilidad, solo documenta el bypass.
/// </summary>
internal static class PlatformQueryAccessor
{
    public static IQueryable<T> AsPlatformQuery<T>(this DbSet<T> set)
        where T : class => set.IgnoreQueryFilters();
}
