using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Models;

namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Repositorio del agregado SupplierExpenseSubcategory — EXPENSE-SUPPLIER-SUBCATEGORIES-01.
/// Scope: ITenantScopedEntity + ICompanyScopedEntity; todas las consultas quedan filtradas por
/// (tenant_id, company_id) por el query filter global fail-closed.
/// </summary>
public interface ISupplierExpenseSubcategoryRepository
{
    /// <summary>Todas las asignaciones (activas e inactivas) del proveedor en la empresa activa.</summary>
    Task<IReadOnlyList<SupplierExpenseSubcategory>> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Nodos del catálogo de gastos visibles en la empresa activa (los de otra empresa no se
    /// devuelven), con el nombre de su categoría y tipo. Un id no visible simplemente no aparece.
    /// </summary>
    Task<IReadOnlyList<ExpenseSubcategoryInfo>> GetSubcategoryInfosAsync(
        IReadOnlyCollection<Guid> nodeIds,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Subcategorías activas (nivel Subcategoría) del catálogo de gastos de la empresa activa, con el
    /// nombre de su categoría y tipo y sin cuenta contable. Candidatas para configurar el proveedor.
    /// </summary>
    Task<IReadOnlyList<ExpenseSubcategoryInfo>> GetActiveSubcategoryCatalogAsync(
        CancellationToken cancellationToken = default
    );

    Task AddAsync(
        SupplierExpenseSubcategory assignment,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
