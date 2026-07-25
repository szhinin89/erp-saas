using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Accounting.Repositories;

public sealed class AccountRepository : IAccountRepository
{
    private readonly ErpDbContext _context;

    public AccountRepository(ErpDbContext context)
    {
        _context = context;
    }

    private IQueryable<Account> Scoped(Guid tenantId, Guid companyId)
        => _context.Accounts.Where(x => x.TenantId == tenantId && x.CompanyId == companyId);

    public Task<Account?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default)
        => Scoped(tenantId, companyId).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Account>> GetByCompanyAsync(Guid tenantId, Guid companyId, CancellationToken ct = default)
        => await Scoped(tenantId, companyId)
            .OrderBy(x => x.Code)
            .ToListAsync(ct);

    public Task<Account?> FindByCodeAsync(Guid tenantId, Guid companyId, string code, CancellationToken ct = default)
        => Scoped(tenantId, companyId).FirstOrDefaultAsync(x => x.Code == AccountCode.Create(code), ct);

    public Task AddAsync(Account account, CancellationToken ct = default)
        => _context.Accounts.AddAsync(account, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
