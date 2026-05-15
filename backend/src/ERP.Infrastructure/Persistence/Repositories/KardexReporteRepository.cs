using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class KardexReporteRepository : IKardexReporteRepository
{
    private readonly ErpDbContext _context;

    public KardexReporteRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(KardexReporte reporte, CancellationToken ct = default)
        => _context.KardexReportes.AddAsync(reporte, ct).AsTask();

    public Task<KardexReporte?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.KardexReportes
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
