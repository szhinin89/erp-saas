using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

namespace ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft;

/// <summary>
/// Compra lista para editar en el formulario de Nueva Compra — modelo temporal, nunca persistido.
/// Se arma exclusivamente desde el <see cref="PurchaseReceptionDocument"/> ya verificado: cabecera
/// SRI y líneas (incl. Item Matching ya resuelto) fueron persistidas una única vez al descargar el
/// XML (ver <c>DownloadPurchaseReceptionXmlHandler</c>) — este modelo nunca vuelve a tocar el XML.
/// </summary>
public sealed record PurchaseDraft(
    Guid? SupplierId, string SupplierRuc, string SupplierName,
    string? DocTypeCode, string InvoiceNumber, DateOnly IssueDate,
    string? AccessKey, string? AuthorizationNumber, DateTime? AuthorizationDate,
    string? SriPaymentMethodCode,
    IReadOnlyList<PurchaseReceptionLine> Lines,
    PurchaseReceptionProcessingStatus ProcessingStatus, string? ProcessingNotes)
{
    public static PurchaseDraft FromReceptionDocument(PurchaseReceptionDocument document) => new(
        SupplierId: document.SupplierId,
        SupplierRuc: document.SupplierRuc,
        SupplierName: document.SupplierName,
        DocTypeCode: document.DocTypeCode,
        InvoiceNumber: document.InvoiceNumber,
        IssueDate: document.IssueDate,
        AccessKey: document.AccessKey,
        AuthorizationNumber: document.AuthorizationNumber,
        AuthorizationDate: document.AuthorizationDate,
        SriPaymentMethodCode: document.SriPaymentMethodCode,
        Lines: document.Lines,
        ProcessingStatus: document.ProcessingStatus,
        ProcessingNotes: document.ProcessingNotes);
}
