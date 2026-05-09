using ERP.Domain.Proveedores.Entities;

namespace ERP.Domain.Proveedores.Interfaces;

public interface IProveedorRepository
{
    Task AddAsync(Proveedor proveedor, CancellationToken ct = default);
    Task<Proveedor?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> ExistsRucAsync(Guid tenantId, string ruc, Guid? excludeId, CancellationToken ct = default);
    Task<IReadOnlyList<Proveedor>> GetAsync(
        Guid tenantId,
        bool? activeFilter,
        string? search,
        string? tipoPersona,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
