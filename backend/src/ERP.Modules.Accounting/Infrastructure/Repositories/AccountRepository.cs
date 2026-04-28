using Microsoft.EntityFrameworkCore;
using Modules.Accounting.Application.Interfaces;
using Modules.Accounting.Domain.Entities;
using Modules.Accounting.Infrastructure.Persistence;

namespace Modules.Accounting.Infrastructure.Repositories;

public class AccountRepository(AccountingDbContext context) : IAccountRepository
{
    public async Task<Account?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, ct);

    public async Task<Account?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct = default)
        => await context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == code && a.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<Account>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await context.Accounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .OrderBy(a => a.Code)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(string code, Guid tenantId, CancellationToken ct = default)
        => await context.Accounts
            .AnyAsync(a => a.Code == code && a.TenantId == tenantId, ct);

    public async Task AddAsync(Account account, CancellationToken ct = default)
        => await context.Accounts.AddAsync(account, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
