using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Domain.Modules.Accounting.Interfaces;

public interface IAccountingPeriodRepository
{
    Task<AccountingPeriod?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<AccountingPeriod>> GetByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Períodos de la Company cuyo rango [StartDate, EndDate] se superpone con el rango dado —
    /// soporte de lectura para el invariante "sin períodos solapados" (ADR-026 §6.1), que vive
    /// en Application/Repository, no en el aggregate (ver &lt;remarks&gt; de AccountingPeriod).
    /// </summary>
    Task<IReadOnlyList<AccountingPeriod>> GetOverlappingAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default
    );

    /// <summary>Período de la Company cuyo rango [StartDate, EndDate] contiene la fecha dada — resolución del Posting Engine (ADR-026 §8).</summary>
    Task<AccountingPeriod?> FindContainingDateAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly date,
        CancellationToken ct = default
    );

    Task AddAsync(AccountingPeriod period, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
