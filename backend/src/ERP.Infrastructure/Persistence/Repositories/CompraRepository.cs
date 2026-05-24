using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Infrastructure.Options;
using ERP.Infrastructure.Persistence.Mapping;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class PurchBillRepository : IPurchBillRepository
{
    private readonly ErpDbContext _context;
    private readonly DocumentSchemaOptions _schemaOptions;
    private readonly IUnifiedDocumentSync _documentSync;

    public PurchBillRepository(
        ErpDbContext context,
        IOptions<DocumentSchemaOptions> schemaOptions,
        IUnifiedDocumentSync documentSync)
    {
        _context = context;
        _schemaOptions = schemaOptions.Value;
        _documentSync = documentSync;
    }

    private bool UseUnified => _schemaOptions.UseUnifiedSchema;

    public Task AddAsync(PurchBill compra, CancellationToken ct = default)
    {
        if (UseUnified)
            return _context.PurchaseDocuments.AddAsync(PurchaseDocumentMapper.ToDocument(compra), ct).AsTask();
        return _context.PurchBills.AddAsync(compra, ct).AsTask();
    }

    public async Task<PurchBill?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
    {
        if (UseUnified)
        {
            var doc = await InvoiceDocumentsQuery(subscriberId)
                .FirstOrDefaultAsync(c => c.Id == id, ct);
            if (doc is null) return null;
            var bill = PurchaseDocumentMapper.ToLegacyBill(doc);
            if (UseUnified) _documentSync.StagePurchBill(bill);
            return bill;
        }

        return await _context.PurchBills
            .FirstOrDefaultAsync(c => c.SubscriberId == subscriberId && c.Id == id, ct);
    }

    public async Task<PurchBill?> GetByIdWithLinesAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
    {
        if (UseUnified)
        {
            var doc = await InvoiceDocumentsQuery(subscriberId)
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.Id == id, ct);
            if (doc is null) return null;
            var bill = PurchaseDocumentMapper.ToLegacyBill(doc);
            if (UseUnified) _documentSync.StagePurchBill(bill);
            return bill;
        }

        return await _context.PurchBills
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.SubscriberId == subscriberId && c.Id == id, ct);
    }

    public Task<bool> ExistsAccessKeyAsync(Guid subscriberId, string accessKey, CancellationToken ct = default)
    {
        if (UseUnified)
            return _context.PurchaseDocuments.AnyAsync(
                c => c.SubscriberId == subscriberId && c.AccessKey == accessKey, ct);

        return _context.PurchBills
            .AnyAsync(c => c.SubscriberId == subscriberId && c.AccessKey == accessKey, ct);
    }

    public async Task<IReadOnlyList<PurchBill>> GetAsync(
        Guid subscriberId,
        PurchaseStatus? estado,
        Guid? proveedorId,
        DateTime? desde,
        DateTime? hasta,
        string? search,
        CancellationToken ct = default)
    {
        if (UseUnified)
        {
            var q = InvoiceDocumentsQuery(subscriberId);

            if (estado.HasValue)
                q = q.Where(c => c.Status == estado.Value.ToString());
            if (proveedorId.HasValue)
                q = q.Where(c => c.BusinessPartnerId == proveedorId.Value);
            if (desde.HasValue)
                q = q.Where(c => c.IssueDate >= desde.Value.Date);
            if (hasta.HasValue)
                q = q.Where(c => c.IssueDate <= hasta.Value.Date);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                q = q.Where(c =>
                    c.DocNumber.ToLower().Contains(s) ||
                    (c.AccessKey != null && c.AccessKey.Contains(s)));
            }

            var docs = await q.OrderByDescending(c => c.IssueDate).ToListAsync(ct);
            return docs.Select(PurchaseDocumentMapper.ToLegacyBill).ToList();
        }

        var legacy = _context.PurchBills.Where(c => c.SubscriberId == subscriberId);

        if (estado.HasValue)        legacy = legacy.Where(c => c.Status == estado.Value);
        if (proveedorId.HasValue)   legacy = legacy.Where(c => c.BusinessPartnerId == proveedorId.Value);
        if (desde.HasValue)         legacy = legacy.Where(c => c.InvoiceDate >= desde.Value.Date);
        if (hasta.HasValue)         legacy = legacy.Where(c => c.InvoiceDate <= hasta.Value.Date);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            legacy = legacy.Where(c =>
                c.InvoiceNumber.ToLower().Contains(s) ||
                (c.AccessKey != null && c.AccessKey.Contains(s)));
        }

        return await legacy.OrderByDescending(c => c.InvoiceDate).ToListAsync(ct);
    }

    private IQueryable<PurchaseDocument> InvoiceDocumentsQuery(Guid subscriberId) =>
        _context.PurchaseDocuments
            .Where(c => c.SubscriberId == subscriberId && c.DocType == PurchaseDocumentType.Invoice);

    public async Task<IReadOnlyList<PurchWarehouseAlloc>> GetWarehouseAllocsByBillIdAsync(
        Guid subscriberId,
        Guid PurchBillId,
        CancellationToken ct = default)
        => await _context.PurchWarehouseAllocs
            .Where(a => a.SubscriberId == subscriberId && a.PurchBillId == PurchBillId)
            .OrderBy(a => a.PurchBillLineId)
            .ThenBy(a => a.WarehouseId)
            .ToListAsync(ct);

    public Task AddWarehouseAllocAsync(PurchWarehouseAlloc asignacion, CancellationToken ct = default)
        => _context.PurchWarehouseAllocs.AddAsync(asignacion, ct).AsTask();

    public Task AddIssuedRetentionAsync(IssuedRetention retencion, CancellationToken ct = default)
    {
        if (UseUnified)
            return _context.PurchaseWithholdings.AddAsync(PurchaseWithholdingMapper.ToWithholding(retencion), ct).AsTask();
        return _context.IssuedRetentions.AddAsync(retencion, ct).AsTask();
    }

    public async Task<IssuedRetention?> GetIssuedRetentionByIdWithLinesAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
    {
        if (UseUnified)
        {
            var w = await _context.PurchaseWithholdings
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.SubscriberId == subscriberId && r.Id == id, ct);
            if (w is null) return null;
            var legacy = PurchaseWithholdingMapper.ToLegacyRetention(w);
            _documentSync.StageIssuedRetention(legacy);
            return legacy;
        }

        return await _context.IssuedRetentions
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.SubscriberId == subscriberId && r.Id == id, ct);
    }

    public async Task<IReadOnlyList<IssuedRetention>> GetIssuedRetentionsAsync(
        Guid subscriberId,
        Guid? proveedorId,
        CancellationToken ct = default)
    {
        if (UseUnified)
        {
            var q = _context.PurchaseWithholdings
                .Where(r => r.SubscriberId == subscriberId
                    && r.Direction == WithholdingDirection.Issued);
            if (proveedorId.HasValue)
                q = q.Where(r => r.BusinessPartnerId == proveedorId.Value);
            var docs = await q.OrderByDescending(r => r.IssueDate).ToListAsync(ct);
            return docs.Select(PurchaseWithholdingMapper.ToLegacyRetention).ToList();
        }

        var legacy = _context.IssuedRetentions
            .Where(r => r.SubscriberId == subscriberId);
        if (proveedorId.HasValue)
            legacy = legacy.Where(r => r.BusinessPartnerId == proveedorId.Value);
        return await legacy.OrderByDescending(r => r.IssueDate).ToListAsync(ct);
    }

    public Task AddPurchNoteAsync(PurchNote nota, CancellationToken ct = default)
    {
        if (UseUnified)
            return _context.PurchaseDocuments.AddAsync(PurchaseDocumentMapper.ToDocument(nota), ct).AsTask();
        return _context.PurchNotes.AddAsync(nota, ct).AsTask();
    }

    public async Task<PurchNote?> GetPurchNoteByIdWithLinesAsync(
        Guid subscriberId,
        Guid id,
        CancellationToken ct = default)
    {
        if (UseUnified)
        {
            var doc = await NoteDocumentsQuery(subscriberId)
                .Include(n => n.Lines)
                .Include(n => n.Reference)
                .FirstOrDefaultAsync(n => n.SubscriberId == subscriberId && n.Id == id, ct);
            if (doc is null) return null;
            var (billId, expenseId) = await ResolveNoteLinksAsync(doc, ct);
            var note = PurchaseDocumentMapper.ToLegacyNote(doc, billId, expenseId);
            if (UseUnified) _documentSync.StagePurchNote(note);
            return note;
        }

        return await _context.PurchNotes
            .Include(n => n.Lines)
            .FirstOrDefaultAsync(n => n.SubscriberId == subscriberId && n.Id == id, ct);
    }

    public Task<bool> ExistsPurchNoteAccessKeyAsync(
        Guid subscriberId,
        string accessKey,
        CancellationToken ct = default)
    {
        if (UseUnified)
            return _context.PurchaseDocuments.AnyAsync(
                n => n.SubscriberId == subscriberId && n.AccessKey == accessKey, ct);

        return _context.PurchNotes.AnyAsync(
            n => n.SubscriberId == subscriberId && n.AccessKey == accessKey, ct);
    }

    public async Task<IReadOnlyList<PurchNote>> GetPurchNotesAsync(
        Guid subscriberId,
        Guid? proveedorId,
        Guid? PurchBillId,
        Guid? ExpenseInvoiceId,
        string? estado,
        CancellationToken ct = default)
    {
        if (UseUnified)
        {
            var q = NoteDocumentsQuery(subscriberId);
            if (proveedorId.HasValue)
                q = q.Where(n => n.BusinessPartnerId == proveedorId.Value);
            if (PurchBillId.HasValue)
                q = q.Where(n => n.ReferenceDocumentId == PurchBillId.Value);
            if (ExpenseInvoiceId.HasValue)
                q = q.Where(n => n.ReferenceDocumentId == ExpenseInvoiceId.Value);
            if (!string.IsNullOrWhiteSpace(estado))
                q = q.Where(n => n.Status == estado.Trim());

            var docs = await q.OrderByDescending(n => n.IssueDate).ToListAsync(ct);
            var result = new List<PurchNote>(docs.Count);
            foreach (var doc in docs)
            {
                var links = await ResolveNoteLinksAsync(doc, ct);
                result.Add(PurchaseDocumentMapper.ToLegacyNote(doc, links.BillId, links.ExpenseId));
            }
            return result;
        }

        var legacy = _context.PurchNotes.Where(n => n.SubscriberId == subscriberId);
        if (proveedorId.HasValue) legacy = legacy.Where(n => n.BusinessPartnerId == proveedorId.Value);
        if (PurchBillId.HasValue) legacy = legacy.Where(n => n.PurchBillId == PurchBillId.Value);
        if (ExpenseInvoiceId.HasValue) legacy = legacy.Where(n => n.ExpenseInvoiceId == ExpenseInvoiceId.Value);
        if (!string.IsNullOrWhiteSpace(estado))
            legacy = legacy.Where(n => n.Status == estado.Trim());

        return await legacy.OrderByDescending(n => n.IssueDate).ToListAsync(ct);
    }

    private IQueryable<PurchaseDocument> NoteDocumentsQuery(Guid subscriberId) =>
        _context.PurchaseDocuments
            .Where(n => n.SubscriberId == subscriberId
                && (n.DocType == PurchaseDocumentType.CreditNote
                    || n.DocType == PurchaseDocumentType.DebitNote));

    private async Task<(Guid? BillId, Guid? ExpenseId)> ResolveNoteLinksAsync(
        PurchaseDocument doc,
        CancellationToken ct)
    {
        if (!doc.ReferenceDocumentId.HasValue)
            return (null, null);

        var refId = doc.ReferenceDocumentId.Value;
        if (doc.Reference?.DocType == PurchaseDocumentType.Invoice)
            return (refId, null);

        var isBill = await _context.PurchaseDocuments.AnyAsync(
            p => p.Id == refId && p.DocType == PurchaseDocumentType.Invoice, ct);
        if (isBill)
            return (refId, null);

        var isExpense = await _context.ExpenseDocuments.AnyAsync(e => e.Id == refId, ct);
        if (isExpense)
            return (null, refId);

        return (null, null);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _documentSync.FlushAsync(ct);
        await _context.SaveChangesAsync(ct);
    }
}
