using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.InitialLoad;

public sealed class OpeningBalancePostingRepository : IOpeningBalancePostingRepository
{
    private readonly ErpDbContext _context;

    public OpeningBalancePostingRepository(ErpDbContext context) => _context = context;

    // Filtro explícito tenant + empresa además del global query filter (mismo criterio que
    // ImportBatchRepository.Scoped).
    public Task<OpeningBalancePosting?> FindByBatchAsync(
        Guid tenantId,
        Guid companyId,
        Guid importBatchId,
        CancellationToken cancellationToken = default
    ) =>
        _context
            .OpeningBalancePostings.Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            .FirstOrDefaultAsync(x => x.ImportBatchId == importBatchId, cancellationToken);

    public async Task LockBatchAsync(
        Guid tenantId,
        Guid companyId,
        Guid importBatchId,
        CancellationToken cancellationToken = default
    )
    {
        // Proveedor en memoria (tests unitarios): sin transacciones reales ni FOR UPDATE.
        if (!_context.Database.IsRelational())
            return;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("El bloqueo del lote requiere una transacción.");
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM import_batches WHERE id = {importBatchId} AND tenant_id = {tenantId} AND company_id = {companyId} FOR UPDATE",
            cancellationToken
        );
    }

    public async Task AddAsync(OpeningBalancePosting posting, CancellationToken cancellationToken = default) =>
        await _context.OpeningBalancePostings.AddAsync(posting, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
