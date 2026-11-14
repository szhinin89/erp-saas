namespace ERP.Domain.MasterData.Models;

/// <summary>
/// Lectura de un nodo del catálogo de gastos (sin cuenta contable) para la configuración
/// proveedor↔subcategoría. <see cref="IsSubcategory"/> distingue el nivel Subcategoría de Tipo y
/// Categoría; <see cref="IsActive"/> es el estado del propio nodo.
/// </summary>
public sealed record ExpenseSubcategoryInfo(
    Guid Id,
    string Code,
    string Name,
    string CategoryName,
    string TypeName,
    bool IsActive,
    bool IsSubcategory
);
