namespace ERP.Domain.Products.Entities;

/// <summary>Listado de categorías con datos de la línea padre (solo lectura / consultas).</summary>
public sealed record ProductCategoryListRow(
    Guid Id,
    string Code,
    string Name,
    Guid LineId,
    string LineCode,
    string LineName,
    bool IsActive);

/// <summary>Listado de subcategorías con jerarquía Línea → Categoría (solo lectura).</summary>
public sealed record ProductSubcategoryListRow(
    Guid Id,
    string Code,
    string Name,
    Guid CategoryId,
    Guid LineId,
    string LineCode,
    string LineName,
    string CategoryCode,
    string CategoryName,
    bool IsActive);
