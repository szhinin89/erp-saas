using ERP.Domain.Modules.Finance.Entities;

namespace ERP.Domain.Modules.Finance.Interfaces;

/// <summary>TREASURY-BANK-ACCOUNTS-01: contrato de persistencia de <see cref="CompanyBankAccount"/> — company-scoped vía el filtro global (<c>ICompanyOperationalEntity</c>).</summary>
public interface ICompanyBankAccountRepository
{
    Task<CompanyBankAccount?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// FINANCIAL-DESTINATION-TO-BANK-ACCOUNT-MIGRATION-01 — bloqueo <c>SELECT ... FOR SHARE</c>
    /// real sobre la fila, para leer <see cref="CompanyBankAccount.AccountingAccountId"/> con la
    /// misma garantía de concurrencia que antes daba <c>legacy treasury destination</c> FOR SHARE.
    /// </summary>
    Task<CompanyBankAccount?> GetByIdForShareAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<CompanyBankAccount>> GetListAsync(
        Guid tenantId,
        bool? isActive,
        CancellationToken ct = default
    );

    /// <summary>Único por CompanyId + BankId + AccountType + AccountNumber (regla del ticket).</summary>
    Task<bool> ExistsAsync(
        Guid tenantId,
        Guid companyId,
        Guid bankId,
        int accountType,
        string accountNumber,
        Guid? excludeId = null,
        CancellationToken ct = default
    );

    Task AddAsync(CompanyBankAccount account, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
