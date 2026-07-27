using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Finance.Events;

/// <summary>
/// Se levanta cuando <c>Payment.Apply()</c> aplica un cobro (dirección Collection) contra una o
/// más <c>SalesReceivable</c>. Traductor de Accounting (fase futura) se suscribe a este evento
/// para generar el asiento de cobro — mismo patrón que <c>SalesInvoiceAuthorizedEvent</c>.
/// </summary>
public sealed class CollectionAppliedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid PaymentId { get; }
    public Guid CompanyId { get; }
    public Guid CustomerId { get; }
    public decimal Amount { get; }
    public DateOnly PaymentDate { get; }

    public CollectionAppliedEvent(
        Guid tenantId, Guid paymentId, Guid companyId, Guid customerId, decimal amount, DateOnly paymentDate)
    {
        TenantId = tenantId;
        PaymentId = paymentId;
        CompanyId = companyId;
        CustomerId = customerId;
        Amount = amount;
        PaymentDate = paymentDate;
    }

    Guid IAuditEvent.EntityId => PaymentId;
    string IAuditEvent.Action => "CollectionApplied";
    string? IAuditEvent.Reason => null;
}

/// <summary>
/// Se levanta cuando <c>Payment.Apply()</c> aplica un pago (dirección Payment) contra una o más
/// <c>PurchasePayable</c>. Traductor de Accounting (fase futura) se suscribe a este evento para
/// generar el asiento de pago — mismo patrón que <c>PurchaseInvoiceConfirmedEvent</c>.
/// </summary>
public sealed class SupplierPaymentAppliedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid PaymentId { get; }
    public Guid CompanyId { get; }
    public Guid SupplierId { get; }
    public decimal Amount { get; }
    public DateOnly PaymentDate { get; }

    public SupplierPaymentAppliedEvent(
        Guid tenantId, Guid paymentId, Guid companyId, Guid supplierId, decimal amount, DateOnly paymentDate)
    {
        TenantId = tenantId;
        PaymentId = paymentId;
        CompanyId = companyId;
        SupplierId = supplierId;
        Amount = amount;
        PaymentDate = paymentDate;
    }

    Guid IAuditEvent.EntityId => PaymentId;
    string IAuditEvent.Action => "SupplierPaymentApplied";
    string? IAuditEvent.Reason => null;
}

/// <summary>Se levanta cuando <c>Payment.Reverse()</c> reversa un cobro ya aplicado (dirección Collection).</summary>
public sealed class CollectionReversedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid PaymentId { get; }
    public Guid CompanyId { get; }
    public Guid CustomerId { get; }
    public decimal Amount { get; }
    public string Reason { get; }

    public CollectionReversedEvent(
        Guid tenantId, Guid paymentId, Guid companyId, Guid customerId, decimal amount, string reason)
    {
        TenantId = tenantId;
        PaymentId = paymentId;
        CompanyId = companyId;
        CustomerId = customerId;
        Amount = amount;
        Reason = reason;
    }

    Guid IAuditEvent.EntityId => PaymentId;
    string IAuditEvent.Action => "CollectionReversed";
    string? IAuditEvent.Reason => Reason;
}

/// <summary>Se levanta cuando <c>Payment.Reverse()</c> reversa un pago ya aplicado (dirección Payment).</summary>
public sealed class SupplierPaymentReversedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid PaymentId { get; }
    public Guid CompanyId { get; }
    public Guid SupplierId { get; }
    public decimal Amount { get; }
    public string Reason { get; }

    public SupplierPaymentReversedEvent(
        Guid tenantId, Guid paymentId, Guid companyId, Guid supplierId, decimal amount, string reason)
    {
        TenantId = tenantId;
        PaymentId = paymentId;
        CompanyId = companyId;
        SupplierId = supplierId;
        Amount = amount;
        Reason = reason;
    }

    Guid IAuditEvent.EntityId => PaymentId;
    string IAuditEvent.Action => "SupplierPaymentReversed";
    string? IAuditEvent.Reason => Reason;
}
