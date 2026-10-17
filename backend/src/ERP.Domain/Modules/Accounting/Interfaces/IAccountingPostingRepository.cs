using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Domain.Modules.Accounting.Interfaces;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — persistencia de la obligación contable durable y de su historial de
/// intentos. Toda lectura filtra explícitamente Tenant + Company además del query filter global.
/// </summary>
public interface IAccountingPostingRepository
{
    Task<AccountingPosting?> FindByKeyAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string factType,
        Guid sourceEventId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Bloquea la fila (<c>FOR UPDATE</c>) dentro de la transacción ambiente y devuelve la
    /// obligación con su estado ya confirmado en BD. Requiere transacción activa.
    /// </summary>
    Task<AccountingPosting?> LockByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    );

    /// <inheritdoc cref="LockByIdAsync"/>
    Task<AccountingPosting?> LockByKeyAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string factType,
        Guid sourceEventId,
        CancellationToken ct = default
    );

    Task<AccountingPosting?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<AccountingPosting>> ListAsync(
        Guid tenantId,
        Guid companyId,
        AccountingPostingStatus? status,
        string? sourceModule,
        Guid? sourceEventId,
        int take,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<AccountingPostingAttempt>> ListAttemptsAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountingPostingId,
        CancellationToken ct = default
    );

    /// <summary>¿Existe una obligación Pending/Failed con EntryDate dentro de [from, to]?</summary>
    Task<bool> HasUnresolvedInRangeAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default
    );

    /// <summary>ACCOUNTING-CONTINUITY-05 — obligaciones agrupadas por documento operativo.</summary>
    Task<IReadOnlyList<AccountingPosting>> ListBySourceDocumentAsync(
        Guid tenantId,
        Guid companyId,
        Guid sourceDocumentId,
        CancellationToken ct = default
    );

    /// <summary>ACCOUNTING-CONTINUITY-05 — existe alguna obligación Pending/Failed en la Company.</summary>
    Task<bool> HasUnresolvedAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);

    /// <summary>
    /// ACCOUNTING-CONTINUITY-05 — mayor fecha contable de las obligaciones automáticas no canceladas
    /// (excluye reversos), o null si no hay ninguna.
    /// </summary>
    Task<DateOnly?> GetLatestAutomaticEntryDateAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    );

    Task AddAsync(AccountingPosting posting, CancellationToken ct = default);

    Task AddAttemptAsync(AccountingPostingAttempt attempt, CancellationToken ct = default);
}
