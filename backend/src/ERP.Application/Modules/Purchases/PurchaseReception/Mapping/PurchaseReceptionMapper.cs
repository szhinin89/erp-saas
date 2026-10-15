using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;

namespace ERP.Application.Modules.Purchases.PurchaseReception.Mapping;

public static class PurchaseReceptionMapper
{
    public static PurchaseReceptionItemDto ToDto(
        PurchaseReceptionVerifiedItem item,
        PurchaseReceptionDocument document,
        DateTime authorizationDateUtc,
        bool creditNoteExists = false,
        Guid? creditNoteId = null,
        Guid? cancelledCreditNoteId = null
    ) =>
        new(
            item.Record.SupplierRuc,
            item.Record.SupplierName,
            ToSourceDocTypeCode(item.Record.SourceDocType),
            item.Record.InvoiceNumber,
            item.Record.ModifiedDocumentNumber,
            item.Record.AccessKey,
            item.Record.IssueDate,
            authorizationDateUtc,
            item.Record.Subtotal,
            item.Record.VatAmount,
            item.Record.Total,
            item.SupplierExists,
            item.SupplierId,
            item.SupplierIsActive,
            item.PurchaseId,
            item.AffectedPurchaseExists,
            item.AffectedPurchaseId,
            ToStatusCode(item.Status),
            document.Id,
            ToDocumentStatusCode(document.Status),
            ToProcessingStatusCode(document.ProcessingStatus),
            document.ProcessingNotes,
            item.ExpenseExists,
            creditNoteExists,
            creditNoteId,
            cancelledCreditNoteId,
            item.CancelledPurchaseId,
            item.CancelledExpenseId,
            ToPurchaseStateCode(item.PurchaseState)
        );

    /// <summary>
    /// FASE 5 PURCHASE-UX — mismo registro que produce el TXT, armado desde la recepción ya guardada
    /// (todos esos datos se persistieron al importar). Permite re-verificar sin volver a importar.
    /// <c>AuthorizationLocalDateTime</c> no participa en la verificación (solo se usa al crear el
    /// documento); la fecha de autorización del DTO sale de <c>document.AuthorizationDate</c>.
    /// </summary>
    public static PurchaseReceptionRecord ToRecord(PurchaseReceptionDocument document) =>
        new(
            0,
            document.SourceDocType,
            document.SupplierRuc,
            document.SupplierName,
            document.InvoiceNumber,
            document.AccessKey,
            document.IssueDate,
            document.AuthorizationDate ?? default,
            null,
            document.Subtotal,
            document.VatAmount,
            document.TotalAmount,
            document.ModifiedDocumentNumber
        );

    /// <summary>FASE 5 PURCHASE-UX — código del estado de la compra relacionada para el contrato.</summary>
    public static string ToPurchaseStateCode(PurchaseReceptionPurchaseState state) =>
        state switch
        {
            PurchaseReceptionPurchaseState.None => "NONE",
            PurchaseReceptionPurchaseState.Draft => "DRAFT",
            PurchaseReceptionPurchaseState.Confirmed => "CONFIRMED",
            PurchaseReceptionPurchaseState.Cancelled => "CANCELLED",
            PurchaseReceptionPurchaseState.Discarded => "DISCARDED",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };

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
