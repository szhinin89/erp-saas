using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Payables;

public sealed class AccountsPayableRepository : IAccountsPayableRepository
{
    private readonly ErpDbContext _db;

    public AccountsPayableRepository(ErpDbContext db) => _db = db;

    public Task<AccountsPayable?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.AccountsPayables
            .Include(x => x.Installments)
            .Where(x => x.TenantId == tenantId)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<AccountsPayable?> GetByOriginAsync(
        Guid tenantId,
        Guid companyId,
        AccountsPayableOriginType originType,
        Guid originId,
        CancellationToken ct = default
    ) =>
        _db.AccountsPayables
            .Include(x => x.Installments)
            .FirstOrDefaultAsync(
                x =>
                    x.TenantId == tenantId
                    && x.CompanyId == companyId
                    && x.OriginType == originType
                    && x.OriginId == originId,
                ct
            );

    public Task AddAsync(AccountsPayable payable, CancellationToken ct = default) =>
        _db.AccountsPayables.AddAsync(payable, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
