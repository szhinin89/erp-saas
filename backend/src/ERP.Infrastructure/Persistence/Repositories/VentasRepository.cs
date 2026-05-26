using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Infrastructure.Persistence.Mapping;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class SalesRepository : ISalesRepository
{
    private readonly ErpDbContext _context;
    private readonly IUnifiedDocumentSync _documentSync;
    private readonly ICurrentCompany _company;
    private readonly IPlatformQueryAccessor _platform;

    public SalesRepository(
        ErpDbContext context,
        IUnifiedDocumentSync documentSync,
        ICurrentCompany company,
        IPlatformQueryAccessor platform)
    {
        _context = context;
        _documentSync = documentSync;
        _company = company;
        _platform = platform;
    }

    private IQueryable<SalesDocument> ScopedDocuments(Guid subscriberId) =>
        _context.SalesDocuments.ForOperationalScope(subscriberId, _company);

    public Task AddBillAsync(SalesBill factura, CancellationToken ct = default)
        => _context.SalesDocuments.AddAsync(SalesDocumentMapper.ToDocument(factura), ct).AsTask();

    public async Task<SalesBill?> GetBillByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
    {
        var doc = await ScopedDocuments(subscriberId)
            .Include(d => d.Lines)
            .Include(d => d.Electronic)
            .Where(d => d.Id == id
                && (d.DocType == SalesDocumentType.Invoice || d.DocType == SalesDocumentType.Proforma))
            .FirstOrDefaultAsync(ct);
        if (doc is null) return null;
        var bill = SalesDocumentMapper.ToLegacyBill(doc);
        _documentSync.StageSalesBill(bill);
        return bill;
    }

    public async Task<IReadOnlyList<SalesBill>> GetBillsAsync(
        Guid subscriberId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        string? estado,
        CancellationToken ct = default)
    {
        var query = InvoiceDocumentsQuery(subscriberId);
        if (fechaDesde.HasValue)
            query = query.Where(f => f.IssueDate >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(f => f.IssueDate <= fechaHasta.Value);
        if (!string.IsNullOrEmpty(estado))
            query = query.Where(f => f.Status == estado);

        var docs = await query.ToListAsync(ct);
        return docs.Select(SalesDocumentMapper.ToLegacyBill).ToList();
    }

    public async Task<(IReadOnlyList<SalesBill> Items, int TotalCount)> GetBillsPagedAsync(
        Guid subscriberId,
        int pageNumber,
        int pageSize,
        Guid? clienteId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        string? estado,
        string? search,
        CancellationToken ct = default)
    {
        var query = InvoiceDocumentsQuery(subscriberId);

        if (clienteId.HasValue)
            query = query.Where(f => f.BusinessPartnerId == clienteId.Value);
        if (fechaDesde.HasValue)
            query = query.Where(f => f.IssueDate >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(f => f.IssueDate <= fechaHasta.Value);
        if (!string.IsNullOrEmpty(estado))
            query = query.Where(f => f.Status == estado);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(f =>
                (f.Sequential != null && f.Sequential.Contains(search)) ||
                (f.AccessKey != null && f.AccessKey.Contains(search)) ||
                (f.Electronic != null && f.Electronic.AuthNumber != null && f.Electronic.AuthNumber.Contains(search)));

        var totalCount = await query.CountAsync(ct);
        var docs = await query
            .OrderByDescending(f => f.IssueDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (docs.Select(SalesDocumentMapper.ToLegacyBill).ToList(), totalCount);
    }

    private IQueryable<SalesDocument> InvoiceDocumentsQuery(Guid subscriberId) =>
        ScopedDocuments(subscriberId)
            .Include(f => f.Electronic)
            .Where(f => f.DocType == SalesDocumentType.Invoice || f.DocType == SalesDocumentType.Proforma);

    public Task AddNoteAsync(SalesNote nota, CancellationToken ct = default)
        => _context.SalesDocuments.AddAsync(SalesDocumentMapper.ToDocument(nota), ct).AsTask();

    public async Task<SalesNote?> GetNoteByIdWithLinesAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
    {
        var doc = await NoteDocumentsQuery(subscriberId)
            .Include(n => n.Lines)
            .Include(n => n.Electronic)
            .Include(n => n.Reference)
            .FirstOrDefaultAsync(n => n.Id == id, ct);
        if (doc is null) return null;
        var note = SalesDocumentMapper.ToLegacyNote(doc);
        _documentSync.StageSalesNote(note);
        return note;
    }

    public async Task<IReadOnlyList<SalesNote>> GetNotesAsync(
        Guid subscriberId,
        Guid? facturaOriginalId,
        string? estado,
        CancellationToken ct = default)
    {
        IQueryable<SalesDocument> q = ScopedDocuments(subscriberId)
            .Where(n => n.DocType == SalesDocumentType.CreditNote || n.DocType == SalesDocumentType.DebitNote)
            .Include(n => n.Reference);

        if (facturaOriginalId.HasValue)
            q = q.Where(n => n.ReferenceDocumentId == facturaOriginalId.Value);
        if (!string.IsNullOrWhiteSpace(estado))
            q = q.Where(n => n.Status == estado);

        var docs = await q.OrderByDescending(n => n.IssueDate).ToListAsync(ct);
        return docs.Select(SalesDocumentMapper.ToLegacyNote).ToList();
    }

    private IQueryable<SalesDocument> NoteDocumentsQuery(Guid subscriberId) =>
        ScopedDocuments(subscriberId)
            .Where(n => n.DocType == SalesDocumentType.CreditNote || n.DocType == SalesDocumentType.DebitNote);

    public Task AddRetentionAsync(SalesRetention retencion, CancellationToken ct = default)
        => _context.SalesWithholdings.AddAsync(SalesWithholdingMapper.ToWithholding(retencion), ct).AsTask();

    public async Task<IReadOnlyList<SalesRetention>> GetRetentionsAsync(
        Guid subscriberId,
        CancellationToken ct = default)
    {
        var docs = await _context.SalesWithholdings
            .Include(r => r.Lines)
            .Where(r => r.SubscriberId == subscriberId
                && r.Direction == ERP.Domain.Common.WithholdingDirection.Received)
            .OrderByDescending(r => r.IssueDate)
            .ToListAsync(ct);
        return docs.Select(SalesWithholdingMapper.ToLegacyRetention).ToList();
    }

    public Task<bool> ExistsRetentionAccessKeyAsync(Guid subscriberId, string accessKey, CancellationToken ct = default)
        => _context.SalesWithholdings.AnyAsync(
            r => r.SubscriberId == subscriberId && r.AccessKey == accessKey, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _documentSync.FlushAsync(ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SalesBillRetryCandidate>> ListPendingElectronicRetryAsync(CancellationToken ct = default)
    {
        var query = _platform.Unfiltered(_context.SalesDocuments, PlatformQueryReason.BackgroundJob)
            .Where(b => b.Status == "ErrorEnvio" || b.Status == "Rechazado");

        return await query
            .Select(b => new SalesBillRetryCandidate(b.Id, b.SubscriberId))
            .ToListAsync(ct);
    }
}
