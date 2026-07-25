using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Accounting.Repositories;

public sealed class AccountingPeriodRepository : IAccountingPeriodRepository
{
    private readonly ErpDbContext _context;

    public AccountingPeriodRepository(ErpDbContext context)
    {
        _context = context;
    }

    private IQueryable<AccountingPeriod> Scoped(Guid tenantId, Guid companyId)
        => _context.AccountingPeriods.Where(x => x.TenantId == tenantId && x.CompanyId == companyId);

    public Task<AccountingPeriod?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default)
        => Scoped(tenantId, companyId).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<AccountingPeriod>> GetByCompanyAsync(Guid tenantId, Guid companyId, CancellationToken ct = default)
        => await Scoped(tenantId, companyId)
            .OrderBy(x => x.FiscalYear).ThenBy(x => x.PeriodNumber)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AccountingPeriod>> GetOverlappingAsync(
        Guid tenantId, Guid companyId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
        => await Scoped(tenantId, companyId)
            .Where(x => x.StartDate <= endDate && x.EndDate >= startDate)
            .ToListAsync(ct);

    public Task<AccountingPeriod?> FindContainingDateAsync(
        Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct = default)
        => Scoped(tenantId, companyId)
            .FirstOrDefaultAsync(x => x.StartDate <= date && x.EndDate >= date, ct);

    public Task AddAsync(AccountingPeriod period, CancellationToken ct = default)
        => _context.AccountingPeriods.AddAsync(period, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
