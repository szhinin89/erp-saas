using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Events;

/// <summary>
/// SALES-CASH-VS-RECEIVABLE-POSTING-SPLIT-AND-CANCEL-REVERSAL-01 Lote 3 — se levanta cuando
/// <c>SalesInvoice.Cancel()</c> anula una venta autorizada. Mismo criterio/forma que
/// <c>PurchaseInvoiceCancelledEvent</c> (Compras) — antes de este lote, <c>SalesInvoice.Cancel()</c>
/// no levantaba ningún domain event, así que la anulación de una venta nunca reversaba los asientos
/// contables ya generados (<c>Sales/InvoiceIssued</c>/<c>Sales/CostOfGoodsSold</c>), dejándolos
/// activos indefinidamente aunque la venta ya no existiera comercialmente.
/// </summary>
public sealed class SalesInvoiceCancelledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid InvoiceId { get; }
    public Guid CustomerId { get; }
    public string InvoiceNumber { get; }
    public decimal GrandTotal { get; }
    public string CancelReason { get; }
    public Guid CompanyId { get; }

    public SalesInvoiceCancelledEvent(
        Guid tenantId,
        Guid invoiceId,
        Guid customerId,
        string invoiceNumber,
        decimal grandTotal,
        string cancelReason,
        Guid companyId
    )
    {
        TenantId = tenantId;
        InvoiceId = invoiceId;
        CustomerId = customerId;
        InvoiceNumber = invoiceNumber;
        GrandTotal = grandTotal;
        CancelReason = cancelReason;
        CompanyId = companyId;
    }

    Guid IAuditEvent.EntityId => InvoiceId;
    string IAuditEvent.Action => "Cancelled";
    string? IAuditEvent.Reason => CancelReason;
}
