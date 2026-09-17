using ERP.Application.Common;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Finance;

/// <summary>TREASURY-BANK-ACCOUNTS-01: implementación de <see cref="ICompanyBankAccountRepository"/>.</summary>
public sealed class CompanyBankAccountRepository : ICompanyBankAccountRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public CompanyBankAccountRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    public Task<CompanyBankAccount?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db.CompanyBankAccounts.ForOperationalScope(tenantId, _company)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<CompanyBankAccount>> GetListAsync(
        Guid tenantId,
        bool? isActive,
        CancellationToken ct = default
    )
    {
        var query = _db.CompanyBankAccounts.ForOperationalScope(tenantId, _company);
        if (isActive is not null)
            query = query.Where(x => x.IsActive == isActive.Value);
        return await query.OrderBy(x => x.DisplayName).ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Guid tenantId,
        Guid companyId,
        Guid bankId,
        int accountType,
        string accountNumber,
        Guid? excludeId = null,
        CancellationToken ct = default
    )
    {
        var normalizedAccountNumber = accountNumber.Trim();
        var query = _db.CompanyBankAccounts.Where(x =>
            x.TenantId == tenantId
            && x.CompanyId == companyId
            && x.BankId == bankId
            && (int)x.AccountType == accountType
            && x.AccountNumber == normalizedAccountNumber
        );
        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);
        return await query.AnyAsync(ct);
    }

    public Task AddAsync(CompanyBankAccount account, CancellationToken ct = default) =>
        _db.CompanyBankAccounts.AddAsync(account, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
