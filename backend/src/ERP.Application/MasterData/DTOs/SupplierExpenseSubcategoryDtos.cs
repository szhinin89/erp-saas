using ERP.Application.MasterData.Services;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Models;

namespace ERP.Application.MasterData.DTOs;

/// <summary>
/// EXPENSE-SUPPLIER-SUBCATEGORIES-01. <see cref="IsActive"/> es el estado de la asignación;
/// <see cref="IsSubcategoryActive"/> el del nodo del catálogo (false si ya no es una subcategoría
/// visible). Nombres resueltos en servidor, sin cuentas contables.
/// </summary>
public sealed record SupplierExpenseSubcategoryItemDto(
    Guid ExpenseSubcategoryId,
    string Code,
    string Name,
    string CategoryName,
    string TypeName,
    bool IsActive,
    bool IsSubcategoryActive
);

/// <summary>
/// Subcategoría activa del catálogo de gastos candidata a asignarse (sin cuenta contable).
/// </summary>
public sealed record SupplierExpenseCatalogItemDto(
    Guid ExpenseSubcategoryId,
    string Code,
    string Name,
    string CategoryName,
    string TypeName
);

/// <summary>
/// <see cref="Mode"/> ∈ {Unconfigured, Restricted, RestrictedEmpty} y
/// <see cref="AvailableSubcategoryIds"/> los calcula el servidor (regla única); el frontend no los
/// recalcula. <see cref="Items"/> incluye asignaciones activas e inactivas.
/// </summary>
public sealed record SupplierExpenseSubcategoriesDto(
    string Mode,
    IReadOnlyList<SupplierExpenseSubcategoryItemDto> Items,
    IReadOnlyList<Guid> AvailableSubcategoryIds,
    IReadOnlyList<SupplierExpenseCatalogItemDto>? Catalog = null
)
{
    /// <summary>
    /// Candidatas para la configuración: solo el GET las devuelve y únicamente a quien tiene
    /// <c>masterdata.businesspartners.configure-company</c>; <c>null</c> en cualquier otro caso (incluido el PUT).
    /// </summary>
    public SupplierExpenseSubcategoriesDto WithCatalog(IReadOnlyList<ExpenseSubcategoryInfo> catalog) =>
        this with
        {
            Catalog = catalog
                .Where(c => c.IsActive && c.IsSubcategory)
                .OrderBy(c => c.TypeName, StringComparer.Ordinal)
                .ThenBy(c => c.CategoryName, StringComparer.Ordinal)
                .ThenBy(c => c.Code, StringComparer.Ordinal)
                .Select(c => new SupplierExpenseCatalogItemDto(c.Id, c.Code, c.Name, c.CategoryName, c.TypeName))
                .ToList(),
        };

    public static SupplierExpenseSubcategoriesDto Build(
        IReadOnlyList<SupplierExpenseSubcategory> assignments,
        IReadOnlyList<ExpenseSubcategoryInfo> infos
    )
    {
        var scope = SupplierExpenseSubcategoryScope.From(assignments, infos);
        var byId = infos.ToDictionary(i => i.Id);
        var items = assignments
            .Select(a =>
            {
                byId.TryGetValue(a.ExpenseSubcategoryId, out var info);
                return new SupplierExpenseSubcategoryItemDto(
                    a.ExpenseSubcategoryId,
                    info?.Code ?? "?",
                    info?.Name ?? "Subcategoría no encontrada en el catálogo",
                    info?.CategoryName ?? string.Empty,
                    info?.TypeName ?? string.Empty,
                    a.IsActive,
                    info is { IsActive: true, IsSubcategory: true }
                );
            })
            .OrderBy(i => i.TypeName, StringComparer.Ordinal)
            .ThenBy(i => i.CategoryName, StringComparer.Ordinal)
            .ThenBy(i => i.Code, StringComparer.Ordinal)
            .ToList();

        return new SupplierExpenseSubcategoriesDto(
            scope.Mode.ToString(),
            items,
            scope.AvailableIds.OrderBy(x => x).ToList()
        );
    }
}
