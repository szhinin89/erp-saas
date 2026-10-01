using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Application.Modules.Purchases.Services;

/// <summary>
/// ZH-PURCHASE-RETENTION-CONFIRM-01 — una compra vista como documento origen de retención. Única
/// definición de sus bases retenibles, compartida por la vista previa (<c>CalculateRetentionHandler</c>)
/// y la emisión dentro de la confirmación (<c>ConfirmPurchaseHandler</c>), para que ambas evalúen la
/// elegibilidad sobre exactamente las mismas cifras.
/// </summary>
public static class PurchaseRetentionSource
{
    /// <summary>Base retenible de IVA: el IVA total de la compra.</summary>
    public static decimal VatRetainableBase(PurchaseInvoice invoice) => invoice.TotalVat;

    /// <summary>Base retenible de Renta: suma de la base imponible de las líneas (subtotal - descuento).</summary>
    public static decimal IncomeRetainableBase(PurchaseInvoice invoice) =>
        invoice.Lines.Sum(l => l.TaxableBase);

    /// <summary>Snapshot del documento sustento para <see cref="IRetentionIssuer.IssueAsync"/>.</summary>
    public static RetentionSourceDocumentData From(PurchaseInvoice invoice) =>
        new(
            RetentionSourceDocumentType.PurchaseInvoice,
            invoice.Id,
            invoice.SupplierId,
            VatRetainableBase(invoice),
            IncomeRetainableBase(invoice),
            new RetentionDocument.SourceDocumentSnapshot(
                invoice.DocTypeCode,
                invoice.InvoiceNumber,
                invoice.IssueDate,
                invoice.AuthorizationNumber,
                invoice.TaxSupportCode,
                invoice.Subtotal,
                invoice.GrandTotal
            )
        );
}
