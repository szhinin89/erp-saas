using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Domain.Modules.Purchasing.Interfaces;

public interface IProveedorRepository
{
    Task AddAsync(Proveedor proveedor, CancellationToken ct = default);
    Task<Proveedor?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<Proveedor?> GetByRucAsync(Guid tenantId, string ruc, CancellationToken ct = default);
    Task<bool> ExistsRucAsync(Guid tenantId, string ruc, Guid? excludeId, CancellationToken ct = default);
    Task<IReadOnlyList<Proveedor>> GetAsync(
        Guid tenantId,
        bool? activeFilter,
        string? search,
        string? tipoPersona,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
