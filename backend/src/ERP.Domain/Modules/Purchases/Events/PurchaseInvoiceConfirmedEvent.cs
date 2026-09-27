using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>Se levanta cuando <c>PurchaseInvoice.Confirm()</c> pasa la compra de Draft a Confirmed.</summary>
public sealed class PurchaseInvoiceConfirmedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid InvoiceId { get; }
    public Guid SupplierId { get; }
    public string InvoiceNumber { get; }
    public decimal GrandTotal { get; }
    public Guid CompanyId { get; }
    public DateOnly IssueDate { get; }

    /// <summary>
    /// Montos ya resueltos por Purchases (Configuración Tributaria, infraestructura CLOSED) —
    /// ADR-026 §4. Accounting los consume tal cual, nunca los recalcula.
    /// </summary>
    public decimal Subtotal { get; }
    /// <summary>Frozen costing SSOT: net purchase base including reviewed freight and additional costs.</summary>
    public decimal CostSubtotal { get; }
    public decimal TotalVat { get; }
    public decimal TotalIce { get; }
    public decimal TotalDiscount { get; }

    /// <summary>
    /// FLOW-READY-02F.2 — IRBPNR (impuesto SRI código "5"). Opcional, agregado al final del
    /// constructor para no romper ningún call site existente (aditivo, mismo criterio que el resto
    /// de campos de este evento).
    /// </summary>
    public decimal TotalIrbpnr { get; }

    public PurchaseInvoiceConfirmedEvent(
        Guid tenantId,
        Guid invoiceId,
        Guid supplierId,
        string invoiceNumber,
        decimal grandTotal,
        Guid companyId,
        DateOnly issueDate,
        decimal subtotal,
        decimal costSubtotal,
        decimal totalVat,
        decimal totalIce,
        decimal totalDiscount,
        decimal totalIrbpnr = 0m
    )
    {
        TenantId = tenantId;
        InvoiceId = invoiceId;
        SupplierId = supplierId;
        InvoiceNumber = invoiceNumber;
        GrandTotal = grandTotal;
        CompanyId = companyId;
        IssueDate = issueDate;
        Subtotal = subtotal;
        CostSubtotal = costSubtotal;
        TotalVat = totalVat;
        TotalIce = totalIce;
        TotalDiscount = totalDiscount;
        TotalIrbpnr = totalIrbpnr;
    }

    Guid IAuditEvent.EntityId => InvoiceId;
    string IAuditEvent.Action => "Confirmed";
    string? IAuditEvent.Reason => null;
}
