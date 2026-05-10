using ERP.Domain.Inventario.Entities;

namespace ERP.Domain.Inventario.Interfaces;

public interface ITransferenciaRepository
{
    Task AddAsync(Transferencia transferencia, CancellationToken ct = default);

    Task<Transferencia?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<int> GetNextSecuencialAsync(Guid tenantId, CancellationToken ct = default);

    Task<(IReadOnlyList<Transferencia> Items, int TotalCount)> GetPagedAsync(
        Guid      tenantId,
        int       pageNumber,
        int       pageSize,
        Guid?     bodegaOrigenId,
        Guid?     bodegaDestinoId,
        string?   estado,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
