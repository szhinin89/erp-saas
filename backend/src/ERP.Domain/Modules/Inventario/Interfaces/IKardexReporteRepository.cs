using ERP.Domain.Modules.Inventario.Entities;

namespace ERP.Domain.Modules.Inventario.Interfaces;

public interface IKardexReporteRepository
{
    Task AddAsync(KardexReporte reporte, CancellationToken ct = default);
    Task<KardexReporte?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
