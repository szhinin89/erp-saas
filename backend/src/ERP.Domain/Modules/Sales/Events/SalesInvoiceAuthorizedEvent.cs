using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Events;

public sealed class SalesInvoiceAuthorizedEvent : BaseDomainEvent
{
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }
    public decimal GrandTotal { get; }
    public Guid UserId { get; }

    /// <summary>
    /// Caja que originó la venta (<c>SalesInvoice.CashSessionId</c>) — permite registrar el
    /// movimiento de caja de forma determinística, sin buscar la sesión abierta del usuario.
    /// </summary>
    public Guid CashSessionId { get; }

    public SalesInvoiceAuthorizedEvent(
        Guid invoiceId, string invoiceNumber, decimal grandTotal, Guid userId, Guid cashSessionId)
    {
        InvoiceId = invoiceId;
        InvoiceNumber = invoiceNumber;
        GrandTotal = grandTotal;
        UserId = userId;
        CashSessionId = cashSessionId;
    }
}
