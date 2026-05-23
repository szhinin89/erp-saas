using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class ArApRepository : IArApRepository
{
    private readonly ErpDbContext _context;

    public ArApRepository(ErpDbContext context) => _context = context;

    // ── AR ────────────────────────────────────────────────────────────────

    public async Task AddArEntryAsync(AccountsReceivableEntry entry, CancellationToken ct = default)
        => await _context.ArEntries.AddAsync(entry, ct);

    public async Task<AccountsReceivableEntry?> GetArEntryByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
        => await _context.ArEntries
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<AccountsReceivableEntry?> GetArEntryBySalesBillAsync(Guid salesBillId, Guid subscriberId, CancellationToken ct = default)
        => await _context.ArEntries
            .FirstOrDefaultAsync(e => e.SalesBillId == salesBillId, ct);

    public async Task<IReadOnlyList<AccountsReceivableEntry>> GetOpenArEntriesAsync(
        Guid subscriberId, Guid companyId, CancellationToken ct = default)
        => await _context.ArEntries
            .Where(e => e.CompanyId == companyId
                     && e.Status != AccountsReceivableEntry.StatusPaid
                     && e.Status != AccountsReceivableEntry.StatusCancelled)
            .OrderBy(e => e.DueDate)
            .ToListAsync(ct);

    // ── AP ────────────────────────────────────────────────────────────────

    public async Task AddApEntryAsync(AccountsPayableEntry entry, CancellationToken ct = default)
        => await _context.ApEntries.AddAsync(entry, ct);

    public async Task<AccountsPayableEntry?> GetApEntryByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
        => await _context.ApEntries
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<AccountsPayableEntry?> GetApEntryByPurchBillAsync(Guid purchBillId, Guid subscriberId, CancellationToken ct = default)
        => await _context.ApEntries
            .FirstOrDefaultAsync(e => e.PurchBillId == purchBillId, ct);

    public async Task<IReadOnlyList<AccountsPayableEntry>> GetOpenApEntriesAsync(
        Guid subscriberId, Guid companyId, CancellationToken ct = default)
        => await _context.ApEntries
            .Where(e => e.CompanyId == companyId
                     && e.Status != AccountsPayableEntry.StatusPaid
                     && e.Status != AccountsPayableEntry.StatusCancelled)
            .OrderBy(e => e.DueDate)
            .ToListAsync(ct);

    // ── Payments ──────────────────────────────────────────────────────────

    public async Task AddPaymentApplicationAsync(PaymentApplication application, CancellationToken ct = default)
        => await _context.PaymentApplications.AddAsync(application, ct);

    public async Task<IReadOnlyList<PaymentApplication>> GetPaymentsForArEntryAsync(
        Guid arEntryId, Guid subscriberId, CancellationToken ct = default)
        => await _context.PaymentApplications
            .Where(p => p.ArEntryId == arEntryId)
            .OrderBy(p => p.ApplicationDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PaymentApplication>> GetPaymentsForApEntryAsync(
        Guid apEntryId, Guid subscriberId, CancellationToken ct = default)
        => await _context.PaymentApplications
            .Where(p => p.ApEntryId == apEntryId)
            .OrderBy(p => p.ApplicationDate)
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
