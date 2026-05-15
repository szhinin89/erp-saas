using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IWarehouseRepository
{
    Task AddAsync(Warehouse warehouse, CancellationToken ct = default);
    Task<Warehouse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> ExistsNameAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken ct = default);
    Task<IReadOnlyList<Warehouse>> GetAsync(
        Guid    tenantId,
        bool?   activeFilter,
        string? search,
        Guid?   branchId,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
