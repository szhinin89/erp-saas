using ERP.Domain.Bodegas.Entities;

namespace ERP.Domain.Bodegas.Interfaces;

public interface IBodegaRepository
{
    Task AddAsync(Bodega bodega, CancellationToken ct = default);
    Task<Bodega?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> ExistsNombreAsync(Guid tenantId, string nombre, Guid? excludeId, CancellationToken ct = default);
    Task<IReadOnlyList<Bodega>> GetAsync(
        Guid tenantId,
        bool? activeFilter,
        string? search,
        Guid? sucursalId,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
