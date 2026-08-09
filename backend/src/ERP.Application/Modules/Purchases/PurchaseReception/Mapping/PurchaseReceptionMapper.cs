using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;

namespace ERP.Application.Modules.Purchases.PurchaseReception.Mapping;

public static class PurchaseReceptionMapper
{
    public static PurchaseReceptionItemDto ToDto(
        PurchaseReceptionVerifiedItem item,
        PurchaseReceptionDocument document
    ) =>
        new(
            item.Record.SupplierRuc,
            item.Record.SupplierName,
            ToSourceDocTypeCode(item.Record.SourceDocType),
            item.Record.InvoiceNumber,
            item.Record.ModifiedDocumentNumber,
            item.Record.AccessKey,
            item.Record.IssueDate,
            item.Record.AuthorizationDate,
            item.Record.Subtotal,
            item.Record.VatAmount,
            item.Record.Total,
            item.SupplierExists,
            item.PurchaseExists,
            item.AffectedPurchaseExists,
            item.AffectedPurchaseId,
            ToStatusCode(item.Status),
            document.Id,
            ToDocumentStatusCode(document.Status),
            ToProcessingStatusCode(document.ProcessingStatus),
            document.ProcessingNotes
        );

    private static string ToSourceDocTypeCode(PurchaseReceptionSourceDocType sourceDocType) =>
        sourceDocType switch
        {
            PurchaseReceptionSourceDocType.Invoice => "INVOICE",
            PurchaseReceptionSourceDocType.CreditNote => "CREDIT_NOTE",
            PurchaseReceptionSourceDocType.DebitNote => "DEBIT_NOTE",
            PurchaseReceptionSourceDocType.Retention => "RETENTION",
            PurchaseReceptionSourceDocType.Unknown => "UNKNOWN",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceDocType), sourceDocType, null),
        };

    private static string ToStatusCode(PurchaseReceptionStatus status) =>
        status switch
        {
            PurchaseReceptionStatus.Imported => "IMPORTED",
            PurchaseReceptionStatus.Pending => "PENDING",
            PurchaseReceptionStatus.NewSupplier => "NEW_SUPPLIER",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    public static string ToDocumentStatusCode(PurchaseReceptionDocumentStatus status) =>
        status switch
        {
            PurchaseReceptionDocumentStatus.Imported => "IMPORTED",
            PurchaseReceptionDocumentStatus.Verified => "VERIFIED",
            PurchaseReceptionDocumentStatus.Processed => "PROCESSED",
            PurchaseReceptionDocumentStatus.Cancelled => "CANCELLED",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    public static string ToProcessingStatusCode(PurchaseReceptionProcessingStatus status) =>
        status switch
        {
            PurchaseReceptionProcessingStatus.Pending => "PENDING",
            PurchaseReceptionProcessingStatus.Processed => "PROCESSED",
            PurchaseReceptionProcessingStatus.ProcessedWithWarnings => "PROCESSED_WITH_WARNINGS",
            PurchaseReceptionProcessingStatus.Failed => "FAILED",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
}
