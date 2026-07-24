using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;

namespace ERP.Application.Modules.Purchases.PurchaseReception.Mapping;

public static class PurchaseReceptionMapper
{
    public static PurchaseReceptionItemDto ToDto(PurchaseReceptionVerifiedItem item, PurchaseReceptionDocument document) => new(
        item.Record.SupplierRuc,
        item.Record.SupplierName,
        item.Record.InvoiceNumber,
        item.Record.AccessKey,
        item.Record.IssueDate,
        item.Record.AuthorizationDate,
        item.Record.Total,
        item.SupplierExists,
        item.PurchaseExists,
        ToStatusCode(item.Status),
        document.Id,
        ToDocumentStatusCode(document.Status));

    private static string ToStatusCode(PurchaseReceptionStatus status) => status switch
    {
        PurchaseReceptionStatus.Imported => "IMPORTED",
        PurchaseReceptionStatus.Pending => "PENDING",
        PurchaseReceptionStatus.NewSupplier => "NEW_SUPPLIER",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static string ToDocumentStatusCode(PurchaseReceptionDocumentStatus status) => status switch
    {
        PurchaseReceptionDocumentStatus.Imported => "IMPORTED",
        PurchaseReceptionDocumentStatus.Verified => "VERIFIED",
        PurchaseReceptionDocumentStatus.Processed => "PROCESSED",
        PurchaseReceptionDocumentStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
