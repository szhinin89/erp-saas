using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;

namespace ERP.Domain.Modules.Payables.Interfaces;

public interface IAccountsPayableRepository
{
    Task<AccountsPayable?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Natural key real (uq_accounts_payables_tenant_company_origin) — usado por
    /// <c>AccountsPayableService.CreateFromOriginAsync</c> para la idempotencia: nunca crear un
    /// segundo <see cref="AccountsPayable"/> para el mismo documento de origen.
    /// </summary>
    Task<AccountsPayable?> GetByOriginAsync(
        Guid tenantId,
        Guid companyId,
        AccountsPayableOriginType originType,
        Guid originId,
        CancellationToken ct = default
    );

    Task AddAsync(AccountsPayable payable, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
