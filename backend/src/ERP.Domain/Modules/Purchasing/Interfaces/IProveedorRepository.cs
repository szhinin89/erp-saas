using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Domain.Modules.Purchasing.Interfaces;

public interface ISupplierRepository
{
    Task AddAsync(Supplier supplier, CancellationToken ct = default);
    Task<Supplier?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<Supplier?> GetByRucAsync(Guid subscriberId, string ruc, CancellationToken ct = default);
    Task<bool> ExistsRucAsync(Guid subscriberId, string ruc, Guid? excludeId, CancellationToken ct = default);
    Task<IReadOnlyList<Supplier>> GetAsync(
        Guid    subscriberId,
        bool?   activeFilter,
        string? search,
        string? personType,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
