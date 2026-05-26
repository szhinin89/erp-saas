using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Domain.Modules.Accounting.Interfaces;

public interface IAccountingRepository
{
    Task<Account?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default);
    Task<Account?> GetByCodeAsync(string code, Guid subscriberId, CancellationToken ct = default);
    Task<IReadOnlyList<Account>> GetAllBySubscriberAsync(Guid subscriberId, CancellationToken ct = default);
    Task<(IReadOnlyList<Account> Items, int TotalCount)> GetAccountsPageAsync(Guid subscriberId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<bool> ExistsAsync(string code, Guid subscriberId, CancellationToken ct = default);
    Task AddAsync(Account account, CancellationToken ct = default);
    Task UpdateAsync(Account account, CancellationToken ct = default);
    Task AddJournalEntryAsync(JournalEntry entry, CancellationToken ct = default);
    Task<JournalEntry?> GetJournalEntryByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntry>> GetAllJournalEntriesAsync(Guid subscriberId, CancellationToken ct = default);
    Task<(IReadOnlyList<JournalEntry> Items, int TotalCount)> GetJournalEntriesPageAsync(Guid subscriberId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Asientos contabilizados que tienen al menos una línea en la cuenta indicada (rango de fechas del asiento).</summary>
    Task<IReadOnlyList<JournalEntry>> GetPostedJournalEntriesWithAccountAsync(
        Guid subscriberId,
        Guid accountId,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default);

    /// <summary>Líneas de asientos contabilizados para cuentas de efectivo/banco, agrupables por día.</summary>
    Task<IReadOnlyList<(DateTime EntryDate, Guid AccountId, decimal Debit, decimal Credit)>> GetPostedLineAmountsByAccountsAsync(
        Guid subscriberId,
        IReadOnlyList<Guid> accountIds,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default);

    /// <summary>Movimientos de una cuenta para el Mayor General, ordenados por fecha.</summary>
    Task<IReadOnlyList<(DateTime Date, string Reference, string Description, decimal Debit, decimal Credit)>>
        GetGeneralLedgerLinesAsync(
            Guid subscriberId,
            Guid accountId,
            DateTime desde,
            DateTime hasta,
            CancellationToken ct = default);

    /// <summary>Totales débito/crédito por cuenta para el Balance de Comprobación.</summary>
    Task<IReadOnlyList<(Guid AccountId, decimal TotalDebit, decimal TotalCredit)>>
        GetTrialBalanceAsync(
            Guid subscriberId,
            DateTime desde,
            DateTime hasta,
            CancellationToken ct = default);

    // ── Accounting Periods ────────────────────────────────────────────────
    Task<AccountingPeriod?> GetPeriodAsync(Guid subscriberId, int year, int month, CancellationToken ct = default);
    Task AddPeriodAsync(AccountingPeriod period, CancellationToken ct = default);
    Task<IReadOnlyList<AccountingPeriod>> GetPeriodsAsync(Guid subscriberId, CancellationToken ct = default);
}
