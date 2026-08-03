using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Purchases;

public sealed class PurchaseInvoiceRepository : IPurchaseInvoiceRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public PurchaseInvoiceRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<PurchaseInvoice> Scoped(Guid tenantId) =>
        _db.PurchaseInvoices.ForOperationalScope(tenantId, _company);

    public Task<PurchaseInvoice?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Include(x => x.Lines.OrderBy(l => l.SortOrder))
            .Include(x => x.PaymentSchedules.OrderBy(s => s.InstallmentNumber))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<PurchaseInvoice?> GetByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    ) => Scoped(tenantId).FirstOrDefaultAsync(x => x.AccessKey == accessKey, ct);

    public async Task<(
        IReadOnlyList<PurchaseInvoice> Items,
        IReadOnlyDictionary<Guid, int> LineCounts,
        int Total
    )> GetPagedAsync(
        Guid tenantId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var q = Scoped(tenantId);

        if (
            !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<PurchaseStatus>(status.Trim(), true, out var ps)
        )
            q = q.Where(x => x.Status == ps);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.InvoiceNumber.Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = items.Select(i => i.Id).ToList();
        var lineCounts = await _db.Set<PurchaseInvoiceDetail>()
            .Where(d => ids.Contains(d.InvoiceId))
            .GroupBy(d => d.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Count, ct);

        return (items, lineCounts, total);
    }

    public Task AddAsync(PurchaseInvoice invoice, CancellationToken ct = default) =>
        _db.PurchaseInvoices.AddAsync(invoice, ct).AsTask();

    public async Task RemoveLinesByInvoiceAsync(
        Guid invoiceId,
        IEnumerable<PurchaseInvoiceDetail> newLines,
        CancellationToken ct = default
    )
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM purchase_invoice_details WHERE invoice_id = {invoiceId}",
            ct
        );

        foreach (var entry in _db.ChangeTracker.Entries<PurchaseInvoiceDetail>().ToList())
            entry.State = EntityState.Detached;

        foreach (var line in newLines)
            _db.Set<PurchaseInvoiceDetail>().Add(line);
    }

    public async Task ClearScheduleTrackingAsync(Guid invoiceId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM purchase_payment_schedules WHERE purchase_invoice_id = {invoiceId}",
            ct
        );

        foreach (var entry in _db.ChangeTracker.Entries<PurchasePaymentSchedule>().ToList())
            entry.State = EntityState.Detached;
    }

    public void ReattachSchedulesAsAdded(PurchaseInvoice invoice)
    {
        foreach (var entry in _db.ChangeTracker.Entries<PurchasePaymentSchedule>().ToList())
            entry.State = EntityState.Detached;

        foreach (var schedule in invoice.PaymentSchedules)
            _db.Set<PurchasePaymentSchedule>().Add(schedule);
    }

    public Task<PurchasePayable?> GetPayableByPurchaseIdAsync(
        Guid tenantId,
        Guid purchaseId,
        CancellationToken ct = default
    ) =>
        _db
            .PurchasePayables.Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PurchaseId == purchaseId, ct);

    public Task<IssuedWithholding?> GetWithholdingByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db
            .IssuedWithholdings.Include(w => w.Details)
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == id, ct);

    public Task<IssuedWithholding?> GetWithholdingByPurchaseIdAsync(
        Guid tenantId,
        Guid purchaseId,
        CancellationToken ct = default
    ) =>
        _db
            .IssuedWithholdings.Include(w => w.Details)
            .FirstOrDefaultAsync(
                w => w.TenantId == tenantId && w.PurchaseInvoiceId == purchaseId,
                ct
            );

    /// <inheritdoc/>
    public Task<Guid?> GetWithholdingPurchaseInvoiceIdAsync(
        Guid tenantId,
        Guid withholdingId,
        CancellationToken ct = default
    ) =>
        _db
            .IssuedWithholdings.AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.Id == withholdingId)
            .Select(w => (Guid?)w.PurchaseInvoiceId)
            .FirstOrDefaultAsync(ct);

    public void TrackPayable(PurchasePayable payable) => _db.PurchasePayables.Add(payable);

    public void TrackCommunication(PurchaseCommunication communication) =>
        _db.PurchaseCommunications.Add(communication);

    public void TrackWithholding(IssuedWithholding withholding) =>
        _db.IssuedWithholdings.Add(withholding);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
