using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Purchases;

/// <summary>Implementación de <see cref="IPurchaseCreditNoteRepository"/> — diseño FLOW-READY-02C, fase Application/API (.2).</summary>
public sealed class PurchaseCreditNoteRepository : IPurchaseCreditNoteRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public PurchaseCreditNoteRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    public Task<PurchaseCreditNote?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db
            .PurchaseCreditNotes.ForOperationalScope(tenantId, _company)
            .Include(x => x.Lines)
            .Include(x => x.TaxSummaries)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task AddAsync(PurchaseCreditNote creditNote, CancellationToken ct = default) =>
        _db.PurchaseCreditNotes.AddAsync(creditNote, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public Task<PurchaseCreditNote?> GetByCreateClientRequestIdAsync(
        Guid tenantId,
        Guid createClientRequestId,
        CancellationToken ct = default
    ) =>
        _db
            .PurchaseCreditNotes.Include(x => x.Lines)
            .Include(x => x.TaxSummaries)
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.CreateClientRequestId == createClientRequestId,
                ct
            );

    public Task<Guid?> GetPurchaseInvoiceIdAsync(
        Guid tenantId,
        Guid purchaseCreditNoteId,
        CancellationToken ct = default
    ) =>
        _db
            .PurchaseCreditNotes.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == purchaseCreditNoteId)
            .Select(x => (Guid?)x.PurchaseInvoiceId)
            .FirstOrDefaultAsync(ct);

    public async Task<
        IReadOnlyDictionary<
            Guid,
            (Guid SupplierId, string CreditNoteNumber, string Status, DateOnly IssueDate)
        >
    > GetJournalSourceSummariesByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    )
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, (Guid, string, string, DateOnly)>();

        var rows = await _db
            .PurchaseCreditNotes.ForOperationalScope(tenantId, _company)
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.SupplierId,
                x.CreditNoteNumber,
                x.Status,
                x.IssueDate,
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            x => x.Id,
            x => (x.SupplierId, x.CreditNoteNumber, x.Status.ToString(), x.IssueDate)
        );
    }

    public Task<bool> ExistsByReceptionDocumentIdAsync(
        Guid tenantId,
        Guid receptionDocumentId,
        CancellationToken ct = default
    ) =>
        _db.PurchaseCreditNotes.AnyAsync(
            x => x.TenantId == tenantId && x.ReceptionDocumentId == receptionDocumentId,
            ct
        );

    public Task<bool> ExistsByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    ) =>
        _db.PurchaseCreditNotes.AnyAsync(
            x => x.TenantId == tenantId && x.AccessKey == accessKey,
            ct
        );

    public Task<bool> ExistsBySupplierAndCreditNoteNumberAsync(
        Guid tenantId,
        Guid companyId,
        Guid supplierId,
        string creditNoteNumber,
        CancellationToken ct = default
    ) =>
        _db.PurchaseCreditNotes.AnyAsync(
            x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.SupplierId == supplierId
                && x.CreditNoteNumber == creditNoteNumber,
            ct
        );

    public Task<bool> ExistsByLinkedPurchaseReturnIdAsync(
        Guid tenantId,
        Guid purchaseReturnId,
        Guid? excludePurchaseCreditNoteId = null,
        CancellationToken ct = default
    ) =>
        _db.PurchaseCreditNotes.AnyAsync(
            x =>
                x.TenantId == tenantId
                && x.LinkedPurchaseReturnId == purchaseReturnId
                && (excludePurchaseCreditNoteId == null || x.Id != excludePurchaseCreditNoteId.Value),
            ct
        );

    public async Task<(IReadOnlyList<PurchaseCreditNote> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? status,
        Guid? supplierId,
        Guid? purchaseInvoiceId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var q = _db.PurchaseCreditNotes.ForOperationalScope(tenantId, _company);

        if (
            !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<PurchaseCreditNoteStatus>(status.Trim(), true, out var parsedStatus)
        )
            q = q.Where(x => x.Status == parsedStatus);

        if (supplierId is not null)
            q = q.Where(x => x.SupplierId == supplierId.Value);

        if (purchaseInvoiceId is not null)
            q = q.Where(x => x.PurchaseInvoiceId == purchaseInvoiceId.Value);

        if (dateFrom is not null)
            q = q.Where(x => x.IssueDate >= dateFrom.Value);

        if (dateTo is not null)
            q = q.Where(x => x.IssueDate <= dateTo.Value);

        var total = await q.CountAsync(ct);
        var items = await q.Include(x => x.Lines)
            .Include(x => x.TaxSummaries)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetCreditedTaxableBaseByPurchaseTaxSummaryIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> sourcePurchaseInvoiceTaxSummaryIds,
        Guid? excludePurchaseCreditNoteId = null,
        CancellationToken ct = default
    )
    {
        if (sourcePurchaseInvoiceTaxSummaryIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        var query =
            from s in _db.Set<PurchaseCreditNoteTaxSummary>()
            join cn in _db.PurchaseCreditNotes on s.PurchaseCreditNoteId equals cn.Id
            where
                s.TenantId == tenantId
                && sourcePurchaseInvoiceTaxSummaryIds.Contains(s.SourcePurchaseInvoiceTaxSummaryId)
                && cn.Status != PurchaseCreditNoteStatus.Cancelled
                && (excludePurchaseCreditNoteId == null || cn.Id != excludePurchaseCreditNoteId.Value)
            group s by s.SourcePurchaseInvoiceTaxSummaryId into g
            select new { SourceId = g.Key, Credited = g.Sum(x => x.TaxableBase) };

        return await query.ToDictionaryAsync(x => x.SourceId, x => x.Credited, ct);
    }
}
