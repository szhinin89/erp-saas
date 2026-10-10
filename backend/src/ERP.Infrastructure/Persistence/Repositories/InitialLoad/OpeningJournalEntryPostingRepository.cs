using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.InitialLoad;

public sealed class OpeningJournalEntryPostingRepository : IOpeningJournalEntryPostingRepository
{
    private static readonly int[] BalanceImportTypes =
    [
        (int)ImportType.InitialStock,
        (int)ImportType.InitialReceivables,
        (int)ImportType.InitialPayables,
    ];

    private readonly ErpDbContext _context;

    public OpeningJournalEntryPostingRepository(ErpDbContext context) => _context = context;

    // Filtro explícito tenant + empresa además del global query filter (mismo criterio que
    // OpeningBalancePostingRepository).
    public Task<OpeningJournalEntryPosting?> FindCurrentAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    ) =>
        _context.OpeningJournalEntryPostings.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.CompanyId == companyId && x.IsCurrent,
            cancellationToken
        );

    public Task<OpeningJournalEntryPosting?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        _context.OpeningJournalEntryPostings.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && x.CompanyId == companyId,
            cancellationToken
        );

    public async Task<int> GetLastVersionAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    ) =>
        await _context
            .OpeningJournalEntryPostings.Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            .MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0;

    public async Task LockCompanyOpeningAsync(
        Guid tenantId,
        Guid companyId,
        bool includeBalanceBatches,
        CancellationToken cancellationToken = default
    )
    {
        // Proveedor en memoria (tests unitarios): sin transacciones reales ni FOR UPDATE.
        if (!_context.Database.IsRelational())
            return;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("El bloqueo de la apertura requiere una transacción.");
        // Orden fijo empresa → lotes: IL-7B solo bloquea su lote, el cambio de fecha solo la empresa.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM company WHERE id = {companyId} AND tenant_id = {tenantId} FOR UPDATE",
            cancellationToken
        );
        if (includeBalanceBatches)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM import_batches WHERE tenant_id = {tenantId} AND company_id = {companyId} AND import_type = ANY({BalanceImportTypes}) ORDER BY id FOR UPDATE",
                cancellationToken
            );
    }

    public async Task AddAsync(OpeningJournalEntryPosting posting, CancellationToken cancellationToken = default) =>
        await _context.OpeningJournalEntryPostings.AddAsync(posting, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
