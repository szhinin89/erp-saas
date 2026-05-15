using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IAjusteInventarioRepository
{
    Task AddAsync(AjusteInventario ajuste, CancellationToken ct = default);

    Task<AjusteInventario?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<int> GetNextSecuencialAsync(Guid tenantId, CancellationToken ct = default);

    Task<(IReadOnlyList<AjusteInventario> Items, int TotalCount)> GetPagedAsync(
        Guid      tenantId,
        int       pageNumber,
        int       pageSize,
        Guid?     bodegaId,
        Guid?     productoId,
        string?   estado,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
