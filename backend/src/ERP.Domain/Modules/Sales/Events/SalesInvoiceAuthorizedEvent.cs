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

    public Guid CompanyId { get; }
    public DateOnly IssueDate { get; }

    /// <summary>
    /// Montos ya resueltos por Sales (Configuración Tributaria, infraestructura CLOSED) — ADR-026
    /// §4. Accounting los consume tal cual, nunca los recalcula.
    /// </summary>
    public decimal Subtotal { get; }
    public decimal TotalVat { get; }
    public decimal TotalIce { get; }
    public decimal TotalDiscount { get; }

    public SalesInvoiceAuthorizedEvent(
        Guid invoiceId,
        string invoiceNumber,
        decimal grandTotal,
        Guid userId,
        Guid cashSessionId,
        Guid tenantId,
        Guid companyId,
        DateOnly issueDate,
        decimal subtotal,
        decimal totalVat,
        decimal totalIce,
        decimal totalDiscount
    )
    {
        InvoiceId = invoiceId;
        InvoiceNumber = invoiceNumber;
        GrandTotal = grandTotal;
        UserId = userId;
        CashSessionId = cashSessionId;
        TenantId = tenantId;
        CompanyId = companyId;
        IssueDate = issueDate;
        Subtotal = subtotal;
        TotalVat = totalVat;
        TotalIce = totalIce;
        TotalDiscount = totalDiscount;
    }
}
