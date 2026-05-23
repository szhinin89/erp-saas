using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Domain.Modules.Accounting.Interfaces;

public interface IArApRepository
{
    // ── AR ────────────────────────────────────────────────────────────────
    Task AddArEntryAsync(AccountsReceivableEntry entry, CancellationToken ct = default);
    Task<AccountsReceivableEntry?> GetArEntryByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default);
    Task<AccountsReceivableEntry?> GetArEntryBySalesBillAsync(Guid salesBillId, Guid subscriberId, CancellationToken ct = default);

    Task<IReadOnlyList<AccountsReceivableEntry>> GetOpenArEntriesAsync(
        Guid subscriberId, Guid companyId, CancellationToken ct = default);

    // ── AP ────────────────────────────────────────────────────────────────
    Task AddApEntryAsync(AccountsPayableEntry entry, CancellationToken ct = default);
    Task<AccountsPayableEntry?> GetApEntryByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default);
    Task<AccountsPayableEntry?> GetApEntryByPurchBillAsync(Guid purchBillId, Guid subscriberId, CancellationToken ct = default);

    Task<IReadOnlyList<AccountsPayableEntry>> GetOpenApEntriesAsync(
        Guid subscriberId, Guid companyId, CancellationToken ct = default);

    // ── Payments ──────────────────────────────────────────────────────────
    Task AddPaymentApplicationAsync(PaymentApplication application, CancellationToken ct = default);

    Task<IReadOnlyList<PaymentApplication>> GetPaymentsForArEntryAsync(
        Guid arEntryId, Guid subscriberId, CancellationToken ct = default);

    Task<IReadOnlyList<PaymentApplication>> GetPaymentsForApEntryAsync(
        Guid apEntryId, Guid subscriberId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
