using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IKardexReportRepository
{
    Task AddAsync(KardexReport report, CancellationToken ct = default);
    Task<KardexReport?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
