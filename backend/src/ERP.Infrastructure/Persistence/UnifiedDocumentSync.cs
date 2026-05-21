using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Options;
using ERP.Infrastructure.Persistence.Mapping;

namespace ERP.Infrastructure.Persistence;

public sealed class UnifiedDocumentSync : IUnifiedDocumentSync
{
    private readonly ErpDbContext _context;
    private readonly DocumentSchemaOptions _options;

    private SalesBill? _salesBill;
    private SalesNote? _salesNote;
    private PurchBill? _purchBill;
    private PurchNote? _purchNote;
    private ExpenseInvoice? _expenseInvoice;
    private SalesRetention? _salesRetention;
    private IssuedRetention? _issuedRetention;

    public UnifiedDocumentSync(ErpDbContext context, IOptions<DocumentSchemaOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public void StageSalesBill(SalesBill bill) => _salesBill = bill;
    public void StageSalesNote(SalesNote note) => _salesNote = note;
    public void StagePurchBill(PurchBill bill) => _purchBill = bill;
    public void StagePurchNote(PurchNote note) => _purchNote = note;
    public void StageExpenseInvoice(ExpenseInvoice invoice) => _expenseInvoice = invoice;
    public void StageSalesRetention(SalesRetention retention) => _salesRetention = retention;
    public void StageIssuedRetention(IssuedRetention retention) => _issuedRetention = retention;

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (!_options.UseUnifiedSchema)
            return;

        if (_salesBill is not null)
            await UpsertSalesBillAsync(_salesBill, ct);
        if (_salesNote is not null)
            await UpsertSalesNoteAsync(_salesNote, ct);
        if (_purchBill is not null)
            await UpsertPurchBillAsync(_purchBill, ct);
        if (_purchNote is not null)
            await UpsertPurchNoteAsync(_purchNote, ct);
        if (_expenseInvoice is not null)
            await UpsertExpenseInvoiceAsync(_expenseInvoice, ct);
        if (_salesRetention is not null)
            await UpsertSalesWithholdingAsync(_salesRetention, ct);
        if (_issuedRetention is not null)
            await UpsertPurchaseWithholdingAsync(_issuedRetention, ct);

        _salesBill = null;
        _salesNote = null;
        _purchBill = null;
        _purchNote = null;
        _expenseInvoice = null;
        _salesRetention = null;
        _issuedRetention = null;
    }

    private async Task UpsertSalesWithholdingAsync(SalesRetention retention, CancellationToken ct)
    {
        var mapped = SalesWithholdingMapper.ToWithholding(retention);
        var tracked = await _context.SalesWithholdings
            .Include(w => w.Lines)
            .FirstOrDefaultAsync(w => w.Id == retention.Id, ct);

        if (tracked is null)
        {
            await _context.SalesWithholdings.AddAsync(mapped, ct);
            return;
        }

        CopySalesWithholdingScalars(tracked, mapped);
        SyncSalesWithholdingLines(tracked, mapped);
    }

    private async Task UpsertPurchaseWithholdingAsync(IssuedRetention retention, CancellationToken ct)
    {
        var mapped = PurchaseWithholdingMapper.ToWithholding(retention);
        var tracked = await _context.PurchaseWithholdings
            .Include(w => w.Lines)
            .FirstOrDefaultAsync(w => w.Id == retention.Id, ct);

        if (tracked is null)
        {
            await _context.PurchaseWithholdings.AddAsync(mapped, ct);
            return;
        }

        CopyPurchaseWithholdingScalars(tracked, mapped);
        SyncPurchaseWithholdingLines(tracked, mapped);
    }

    private static void CopySalesWithholdingScalars(SalesWithholding target, SalesWithholding source)
    {
        foreach (var name in SalesWithholdingScalarProperties)
            EntityReflection.Set(target, name, EntityReflection.Get(source, name));
    }

    private static void CopyPurchaseWithholdingScalars(PurchaseWithholding target, PurchaseWithholding source)
    {
        foreach (var name in PurchaseWithholdingScalarProperties)
            EntityReflection.Set(target, name, EntityReflection.Get(source, name));
    }

    private void SyncSalesWithholdingLines(SalesWithholding tracked, SalesWithholding mapped)
    {
        var mappedIds = mapped.Lines.Select(l => l.Id).ToHashSet();
        foreach (var existing in tracked.Lines.ToList())
        {
            if (!mappedIds.Contains(existing.Id))
                _context.SalesWithholdingLines.Remove(existing);
        }

        foreach (var src in mapped.Lines)
        {
            var existing = tracked.Lines.FirstOrDefault(l => l.Id == src.Id);
            if (existing is null)
            {
                if (src.SalesWithholdingId == Guid.Empty)
                    src.AssignWithholdingId(tracked.Id);
                tracked.AddLine(src);
                _context.SalesWithholdingLines.Add(src);
            }
            else
            {
                EntityReflection.Set(existing, nameof(SalesWithholdingLine.TaxType), src.TaxType);
                EntityReflection.Set(existing, nameof(SalesWithholdingLine.RetentionCode), src.RetentionCode);
                EntityReflection.Set(existing, nameof(SalesWithholdingLine.TaxableBase), src.TaxableBase);
                EntityReflection.Set(existing, nameof(SalesWithholdingLine.RetentionPct), src.RetentionPct);
                EntityReflection.Set(existing, nameof(SalesWithholdingLine.AmountRetained), src.AmountRetained);
            }
        }
    }

    private void SyncPurchaseWithholdingLines(PurchaseWithholding tracked, PurchaseWithholding mapped)
    {
        var mappedIds = mapped.Lines.Select(l => l.Id).ToHashSet();
        foreach (var existing in tracked.Lines.ToList())
        {
            if (!mappedIds.Contains(existing.Id))
                _context.PurchaseWithholdingLines.Remove(existing);
        }

        foreach (var src in mapped.Lines)
        {
            var existing = tracked.Lines.FirstOrDefault(l => l.Id == src.Id);
            if (existing is null)
            {
                if (src.PurchaseWithholdingId == Guid.Empty)
                    src.AssignWithholdingId(tracked.Id);
                tracked.AddLine(src);
                _context.PurchaseWithholdingLines.Add(src);
            }
            else
            {
                EntityReflection.Set(existing, nameof(PurchaseWithholdingLine.TaxType), src.TaxType);
                EntityReflection.Set(existing, nameof(PurchaseWithholdingLine.RetentionCode), src.RetentionCode);
                EntityReflection.Set(existing, nameof(PurchaseWithholdingLine.TaxableBase), src.TaxableBase);
                EntityReflection.Set(existing, nameof(PurchaseWithholdingLine.RetentionPct), src.RetentionPct);
                EntityReflection.Set(existing, nameof(PurchaseWithholdingLine.AmountRetained), src.AmountRetained);
            }
        }
    }

    private async Task UpsertSalesBillAsync(SalesBill bill, CancellationToken ct)
    {
        var mapped = SalesDocumentMapper.ToDocument(bill);
        var tracked = await _context.SalesDocuments
            .Include(d => d.Lines)
            .Include(d => d.Electronic)
            .FirstOrDefaultAsync(d => d.Id == bill.Id, ct);

        if (tracked is null)
        {
            await _context.SalesDocuments.AddAsync(mapped, ct);
            return;
        }

        CopySalesDocumentScalars(tracked, mapped);
        SyncSalesLines(tracked, mapped);
        SyncSalesElectronic(tracked, mapped.Electronic);
    }

    private async Task UpsertSalesNoteAsync(SalesNote note, CancellationToken ct)
    {
        var mapped = SalesDocumentMapper.ToDocument(note);
        var tracked = await _context.SalesDocuments
            .Include(d => d.Lines)
            .Include(d => d.Electronic)
            .FirstOrDefaultAsync(d => d.Id == note.Id, ct);

        if (tracked is null)
        {
            await _context.SalesDocuments.AddAsync(mapped, ct);
            return;
        }

        CopySalesDocumentScalars(tracked, mapped);
        SyncSalesLines(tracked, mapped);
        SyncSalesElectronic(tracked, mapped.Electronic);
    }

    private async Task UpsertPurchBillAsync(PurchBill bill, CancellationToken ct)
    {
        var mapped = PurchaseDocumentMapper.ToDocument(bill);
        var tracked = await _context.PurchaseDocuments
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == bill.Id, ct);

        if (tracked is null)
        {
            await _context.PurchaseDocuments.AddAsync(mapped, ct);
            return;
        }

        CopyPurchaseDocumentScalars(tracked, mapped);
        SyncPurchaseLines(tracked, mapped);
    }

    private async Task UpsertPurchNoteAsync(PurchNote note, CancellationToken ct)
    {
        var mapped = PurchaseDocumentMapper.ToDocument(note);
        var tracked = await _context.PurchaseDocuments
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == note.Id, ct);

        if (tracked is null)
        {
            await _context.PurchaseDocuments.AddAsync(mapped, ct);
            return;
        }

        CopyPurchaseDocumentScalars(tracked, mapped);
        SyncPurchaseLines(tracked, mapped);
    }

    private async Task UpsertExpenseInvoiceAsync(ExpenseInvoice invoice, CancellationToken ct)
    {
        var mapped = ExpenseDocumentMapper.ToDocument(invoice);
        var tracked = await _context.ExpenseDocuments
            .Include(d => d.Details)
            .FirstOrDefaultAsync(d => d.Id == invoice.Id, ct);

        if (tracked is null)
        {
            await _context.ExpenseDocuments.AddAsync(mapped, ct);
            return;
        }

        CopyExpenseDocumentScalars(tracked, mapped);
        SyncExpenseDetails(tracked, mapped);
    }

    private static void CopySalesDocumentScalars(SalesDocument target, SalesDocument source)
    {
        foreach (var name in SalesDocumentScalarProperties)
            EntityReflection.Set(target, name, EntityReflection.Get(source, name));
    }

    private static void CopyPurchaseDocumentScalars(PurchaseDocument target, PurchaseDocument source)
    {
        foreach (var name in PurchaseDocumentScalarProperties)
            EntityReflection.Set(target, name, EntityReflection.Get(source, name));
    }

    private static void CopyExpenseDocumentScalars(ExpenseDocument target, ExpenseDocument source)
    {
        foreach (var name in ExpenseDocumentScalarProperties)
            EntityReflection.Set(target, name, EntityReflection.Get(source, name));
    }

    private void SyncSalesLines(SalesDocument tracked, SalesDocument mapped)
    {
        var mappedIds = mapped.Lines.Select(l => l.Id).ToHashSet();
        foreach (var existing in tracked.Lines.ToList())
        {
            if (!mappedIds.Contains(existing.Id))
                _context.SalesDetails.Remove(existing);
        }

        foreach (var src in mapped.Lines)
        {
            var existing = tracked.Lines.FirstOrDefault(l => l.Id == src.Id);
            if (existing is null)
            {
                if (src.SalesDocumentId == Guid.Empty)
                    src.AssignDocumentId(tracked.Id);
                tracked.AddLine(src);
                _context.SalesDetails.Add(src);
            }
            else
            {
                CopySalesDetailScalars(existing, src);
            }
        }
    }

    private void SyncPurchaseLines(PurchaseDocument tracked, PurchaseDocument mapped)
    {
        var mappedIds = mapped.Lines.Select(l => l.Id).ToHashSet();
        foreach (var existing in tracked.Lines.ToList())
        {
            if (!mappedIds.Contains(existing.Id))
                _context.PurchaseDetails.Remove(existing);
        }

        foreach (var src in mapped.Lines)
        {
            var existing = tracked.Lines.FirstOrDefault(l => l.Id == src.Id);
            if (existing is null)
            {
                tracked.AddLine(src);
                _context.PurchaseDetails.Add(src);
            }
            else
            {
                CopyPurchaseDetailScalars(existing, src);
            }
        }
    }

    private void SyncExpenseDetails(ExpenseDocument tracked, ExpenseDocument mapped)
    {
        var mappedIds = mapped.Details.Select(d => d.Id).ToHashSet();
        foreach (var existing in tracked.Details.ToList())
        {
            if (!mappedIds.Contains(existing.Id))
                _context.ExpenseDetails.Remove(existing);
        }

        foreach (var src in mapped.Details)
        {
            var existing = tracked.Details.FirstOrDefault(d => d.Id == src.Id);
            if (existing is null)
            {
                src.ExpenseId = tracked.Id;
                src.SubscriberId = tracked.SubscriberId;
                tracked.Details.Add(src);
                _context.ExpenseDetails.Add(src);
            }
            else
            {
                existing.ExpenseId = tracked.Id;
                existing.SubscriberId = tracked.SubscriberId;
                existing.ProductId = src.ProductId;
                existing.Description = src.Description;
                existing.Quantity = src.Quantity;
                existing.UnitPrice = src.UnitPrice;
                existing.TaxAmount = src.TaxAmount;
                existing.LineTotal = src.LineTotal;
                existing.SortOrder = src.SortOrder;
            }
        }
    }

    private void SyncSalesElectronic(SalesDocument tracked, SalesElectronicDoc? mapped)
    {
        if (mapped is null)
            return;

        if (tracked.Electronic is null)
        {
            tracked.AttachElectronic(mapped);
            _context.SalesElectronicDocs.Add(mapped);
            return;
        }

        var e = tracked.Electronic;
        if (!string.IsNullOrWhiteSpace(mapped.AuthNumber))
            e.SetAuthorization(
                mapped.AuthNumber,
                mapped.AuthDate ?? DateTime.UtcNow,
                mapped.XmlSignedPath,
                mapped.XmlAuthPath);

        if (!string.IsNullOrWhiteSpace(mapped.ErrorMessage))
            e.SetError(mapped.ErrorMessage);
        else if (mapped.AuthNumber is not null)
            e.ClearError();
    }

    private static void CopySalesDetailScalars(SalesDetail target, SalesDetail source)
    {
        foreach (var name in SalesDetailScalarProperties)
            EntityReflection.Set(target, name, EntityReflection.Get(source, name));
    }

    private static void CopyPurchaseDetailScalars(PurchaseDetail target, PurchaseDetail source)
    {
        foreach (var name in PurchaseDetailScalarProperties)
            EntityReflection.Set(target, name, EntityReflection.Get(source, name));
    }

    private static readonly string[] SalesDocumentScalarProperties =
    [
        nameof(SalesDocument.CompanyId), nameof(SalesDocument.BranchId), nameof(SalesDocument.CustomerId),
        nameof(SalesDocument.WarehouseId), nameof(SalesDocument.SalespersonId), nameof(SalesDocument.DocType),
        nameof(SalesDocument.DocNumber), nameof(SalesDocument.EstabCode), nameof(SalesDocument.EmPointCode),
        nameof(SalesDocument.Sequential), nameof(SalesDocument.AccessKey), nameof(SalesDocument.IssueDate),
        nameof(SalesDocument.DueDate), nameof(SalesDocument.DeliveryDate), nameof(SalesDocument.ReferenceDocumentId),
        nameof(SalesDocument.Reason), nameof(SalesDocument.Subtotal), nameof(SalesDocument.VatTotal),
        nameof(SalesDocument.TotalDiscount), nameof(SalesDocument.Total), nameof(SalesDocument.TotalNotesApplied),
        nameof(SalesDocument.PaymentMethodCode), nameof(SalesDocument.PaymentDays), nameof(SalesDocument.Currency),
        nameof(SalesDocument.Status), nameof(SalesDocument.Notes), nameof(SalesDocument.RemittanceKey),
        nameof(SalesDocument.GuideDocNum), nameof(SalesDocument.JournalEntryId),
        nameof(SalesDocument.CreatedAt), nameof(SalesDocument.UpdatedAt),
        nameof(SalesDocument.CreatedBy), nameof(SalesDocument.UpdatedBy),
    ];

    private static readonly string[] SalesDetailScalarProperties =
    [
        nameof(SalesDetail.SubscriberId), nameof(SalesDetail.SalesDocumentId), nameof(SalesDetail.ProductId),
        nameof(SalesDetail.ProductCode), nameof(SalesDetail.Description), nameof(SalesDetail.UnitCode),
        nameof(SalesDetail.Quantity), nameof(SalesDetail.UnitPrice), nameof(SalesDetail.DiscountPct),
        nameof(SalesDetail.DiscountAmount), nameof(SalesDetail.VatCode), nameof(SalesDetail.VatPercentage),
        nameof(SalesDetail.Subtotal), nameof(SalesDetail.VatAmount), nameof(SalesDetail.IceAmount),
        nameof(SalesDetail.Total), nameof(SalesDetail.SortOrder),
        nameof(SalesDetail.CreatedAt), nameof(SalesDetail.UpdatedAt),
        nameof(SalesDetail.CreatedBy), nameof(SalesDetail.UpdatedBy),
    ];

    private static readonly string[] PurchaseDocumentScalarProperties =
    [
        nameof(PurchaseDocument.CompanyId), nameof(PurchaseDocument.SupplierId), nameof(PurchaseDocument.DocType),
        nameof(PurchaseDocument.DocNumber), nameof(PurchaseDocument.AccessKey), nameof(PurchaseDocument.IssueDate),
        nameof(PurchaseDocument.RequiredDate), nameof(PurchaseDocument.DueDate), nameof(PurchaseDocument.Subtotal),
        nameof(PurchaseDocument.VatTotal), nameof(PurchaseDocument.Total), nameof(PurchaseDocument.TotalNotesApplied),
        nameof(PurchaseDocument.Currency), nameof(PurchaseDocument.Status), nameof(PurchaseDocument.PaymentTerms),
        nameof(PurchaseDocument.Notes), nameof(PurchaseDocument.XmlPath), nameof(PurchaseDocument.ReferenceDocumentId),
        nameof(PurchaseDocument.Reason), nameof(PurchaseDocument.JournalEntryId),
        nameof(PurchaseDocument.ValidatedBy), nameof(PurchaseDocument.ValidatedAt),
        nameof(PurchaseDocument.ApprovedBy), nameof(PurchaseDocument.ApprovedAt),
        nameof(PurchaseDocument.RejectedBy), nameof(PurchaseDocument.RejectedAt), nameof(PurchaseDocument.RejectionReason),
        nameof(PurchaseDocument.CreatedAt), nameof(PurchaseDocument.UpdatedAt),
        nameof(PurchaseDocument.CreatedBy), nameof(PurchaseDocument.UpdatedBy),
    ];

    private static readonly string[] PurchaseDetailScalarProperties =
    [
        nameof(PurchaseDetail.SubscriberId), nameof(PurchaseDetail.PurchaseDocumentId), nameof(PurchaseDetail.ProductId),
        nameof(PurchaseDetail.Description), nameof(PurchaseDetail.Quantity), nameof(PurchaseDetail.QtyReceived),
        nameof(PurchaseDetail.UnitPrice), nameof(PurchaseDetail.DiscountPct), nameof(PurchaseDetail.VatCode),
        nameof(PurchaseDetail.Subtotal), nameof(PurchaseDetail.VatPercentage),
        nameof(PurchaseDetail.VatAmount), nameof(PurchaseDetail.Total),
        nameof(PurchaseDetail.CreatedAt), nameof(PurchaseDetail.UpdatedAt),
        nameof(PurchaseDetail.CreatedBy), nameof(PurchaseDetail.UpdatedBy),
    ];

    private static readonly string[] SalesWithholdingScalarProperties =
    [
        nameof(SalesWithholding.CompanyId), nameof(SalesWithholding.CustomerId), nameof(SalesWithholding.Direction),
        nameof(SalesWithholding.SalesDocumentId), nameof(SalesWithholding.IssuerRuc), nameof(SalesWithholding.IssuerName),
        nameof(SalesWithholding.VoucherType), nameof(SalesWithholding.AccessKey), nameof(SalesWithholding.IssueDate),
        nameof(SalesWithholding.EstabCode), nameof(SalesWithholding.EmPointCode), nameof(SalesWithholding.Sequential),
        nameof(SalesWithholding.TotalRetained), nameof(SalesWithholding.Status),
        nameof(SalesWithholding.XmlPath), nameof(SalesWithholding.XmlSignedPath), nameof(SalesWithholding.XmlAuthPath),
        nameof(SalesWithholding.AuthNumber), nameof(SalesWithholding.AuthDate), nameof(SalesWithholding.ErrorMessage),
        nameof(SalesWithholding.JournalEntryId),
        nameof(SalesWithholding.CreatedAt), nameof(SalesWithholding.UpdatedAt),
        nameof(SalesWithholding.CreatedBy), nameof(SalesWithholding.UpdatedBy),
    ];

    private static readonly string[] PurchaseWithholdingScalarProperties =
    [
        nameof(PurchaseWithholding.CompanyId), nameof(PurchaseWithholding.SupplierId), nameof(PurchaseWithholding.Direction),
        nameof(PurchaseWithholding.PurchaseDocumentId), nameof(PurchaseWithholding.VoucherType),
        nameof(PurchaseWithholding.AccessKey), nameof(PurchaseWithholding.IssueDate),
        nameof(PurchaseWithholding.EstabCode), nameof(PurchaseWithholding.EmPointCode), nameof(PurchaseWithholding.Sequential),
        nameof(PurchaseWithholding.TotalRetained), nameof(PurchaseWithholding.Status),
        nameof(PurchaseWithholding.XmlPath), nameof(PurchaseWithholding.XmlSignedPath), nameof(PurchaseWithholding.XmlAuthPath),
        nameof(PurchaseWithholding.AuthNumber), nameof(PurchaseWithholding.AuthDate), nameof(PurchaseWithholding.ErrorMessage),
        nameof(PurchaseWithholding.JournalEntryId),
        nameof(PurchaseWithholding.CreatedAt), nameof(PurchaseWithholding.UpdatedAt),
        nameof(PurchaseWithholding.CreatedBy), nameof(PurchaseWithholding.UpdatedBy),
    ];

    private static readonly string[] ExpenseDocumentScalarProperties =
    [
        nameof(ExpenseDocument.SupplierId), nameof(ExpenseDocument.DocType),
        nameof(ExpenseDocument.DocNumber), nameof(ExpenseDocument.AccessKey), nameof(ExpenseDocument.IssueDate),
        nameof(ExpenseDocument.Concept), nameof(ExpenseDocument.Category), nameof(ExpenseDocument.Subtotal),
        nameof(ExpenseDocument.TaxTotal), nameof(ExpenseDocument.Total), nameof(ExpenseDocument.TotalNotesApplied),
        nameof(ExpenseDocument.Status), nameof(ExpenseDocument.Notes), nameof(ExpenseDocument.XmlPath),
        nameof(ExpenseDocument.JournalEntryId), nameof(ExpenseDocument.ValidatedBy), nameof(ExpenseDocument.ValidatedAt),
        nameof(ExpenseDocument.ApprovedBy), nameof(ExpenseDocument.ApprovedAt), nameof(ExpenseDocument.RejectedBy),
        nameof(ExpenseDocument.RejectedAt), nameof(ExpenseDocument.RejectionReason), nameof(ExpenseDocument.IsActive),
        nameof(ExpenseDocument.CreatedAt), nameof(ExpenseDocument.UpdatedAt),
        nameof(ExpenseDocument.CreatedBy), nameof(ExpenseDocument.UpdatedBy),
    ];

    private static class EntityReflection
    {
        public static object? Get<T>(T target, string property) =>
            typeof(T).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.GetValue(target);

        public static void Set<T>(T target, string property, object? value) =>
            typeof(T).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.SetValue(target, value);
    }
}
