using ERP.Domain.Modules.SupplierCatalog.Entities;

namespace ERP.Domain.Modules.SupplierCatalog.Interfaces;

public interface ISupplierItemRepository
{
    Task<SupplierItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierItem>> GetByItemAsync(Guid itemId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid supplierId, Guid itemId, Guid? variantId, CancellationToken ct = default);
    Task AddAsync(SupplierItem supplierItem, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
