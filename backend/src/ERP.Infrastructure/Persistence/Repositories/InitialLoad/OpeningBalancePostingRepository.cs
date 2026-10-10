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

    public async Task AddAsync(OpeningBalancePosting posting, CancellationToken cancellationToken = default) =>
        await _context.OpeningBalancePostings.AddAsync(posting, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
