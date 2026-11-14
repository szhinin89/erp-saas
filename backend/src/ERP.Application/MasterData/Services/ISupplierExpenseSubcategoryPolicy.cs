using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Models;

namespace ERP.Application.MasterData.Services;

/// <summary>
/// Modo de configuración de subcategorías de gasto de un proveedor en la empresa activa
/// (EXPENSE-SUPPLIER-SUBCATEGORIES-01). Se calcula solo sobre asignaciones ACTIVAS:
/// <list type="bullet">
/// <item><see cref="Unconfigured"/>: cero asignaciones activas → catálogo completo.</item>
/// <item><see cref="Restricted"/>: hay asignaciones activas y al menos una disponible.</item>
/// <item><see cref="RestrictedEmpty"/>: hay asignaciones activas pero ninguna disponible
/// (su subcategoría está inactiva o ya no es subcategoría) → no se permite ninguna; nunca
/// degrada a catálogo completo.</item>
/// </list>
/// </summary>
public enum SupplierExpenseSubcategoryMode
{
    Unconfigured = 0,
    Restricted = 1,
    RestrictedEmpty = 2,
}

/// <summary>
/// Resultado de resolver el modo: <see cref="AvailableIds"/> son las subcategorías asignadas
/// (asignación activa) cuyo nodo está activo y es de nivel Subcategoría. Única implementación de la
/// regla de modos — la usan la política, la lectura (GET) y la validación de Gastos.
/// </summary>
public sealed record SupplierExpenseSubcategoryScope(
    SupplierExpenseSubcategoryMode Mode,
    IReadOnlySet<Guid> AvailableIds
)
{
    /// <summary>¿Puede el proveedor usar esta subcategoría en un gasto nuevo?</summary>
    public bool Allows(Guid subcategoryId) =>
        Mode == SupplierExpenseSubcategoryMode.Unconfigured
        || (Mode == SupplierExpenseSubcategoryMode.Restricted && AvailableIds.Contains(subcategoryId));

    public static SupplierExpenseSubcategoryScope From(
        IEnumerable<SupplierExpenseSubcategory> assignments,
        IEnumerable<ExpenseSubcategoryInfo> infos
    )
    {
        var activeIds = assignments
            .Where(x => x.IsActive)
            .Select(x => x.ExpenseSubcategoryId)
            .ToHashSet();
        if (activeIds.Count == 0)
            return new SupplierExpenseSubcategoryScope(
                SupplierExpenseSubcategoryMode.Unconfigured,
                new HashSet<Guid>()
            );

        var available = infos
            .Where(i => activeIds.Contains(i.Id) && i.IsActive && i.IsSubcategory)
            .Select(i => i.Id)
            .ToHashSet();
        return new SupplierExpenseSubcategoryScope(
            available.Count > 0
                ? SupplierExpenseSubcategoryMode.Restricted
                : SupplierExpenseSubcategoryMode.RestrictedEmpty,
            available
        );
    }
}

/// <summary>
/// Resolución centralizada del modo y de las subcategorías disponibles de un proveedor en la
/// empresa activa. Consumida por la lectura de configuración y por la validación de Gastos.
/// </summary>
public interface ISupplierExpenseSubcategoryPolicy
{
    Task<SupplierExpenseSubcategoryScope> GetScopeAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default
    );
}
