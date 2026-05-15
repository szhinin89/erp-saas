using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IKardexReporteRepository
{
    Task AddAsync(KardexReporte reporte, CancellationToken ct = default);
    Task<KardexReporte?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
