using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Domain.Modules.Accounting.Interfaces;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Account>> GetByCompanyAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);

    /// <summary>Búsqueda por la clave única (CompanyId, Code) — ver uq_accounts_company_code.</summary>
    Task<Account?> FindByCodeAsync(Guid tenantId, Guid companyId, string code, CancellationToken ct = default);

    Task AddAsync(Account account, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
