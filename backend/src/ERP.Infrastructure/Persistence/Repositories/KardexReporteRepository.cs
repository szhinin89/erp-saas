using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class KardexReportRepository : IKardexReportRepository
{
    private readonly ErpDbContext _context;

    public KardexReportRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(KardexReport reporte, CancellationToken ct = default)
        => _context.KardexReports.AddAsync(reporte, ct).AsTask();

    public Task<KardexReport?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.KardexReports
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
