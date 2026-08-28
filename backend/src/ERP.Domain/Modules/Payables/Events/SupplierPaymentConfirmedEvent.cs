using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Payables.Events;

/// <summary>
/// SUPPLIER-PAYMENTS-POSTING-15D — snapshot de un <c>SupplierPaymentMethodLine</c> tal como lo
/// necesita el posting contable: cuenta destino (vía <c>FinancialDestinationId</c>) y monto. No
/// transporta <c>PaymentMethodId</c>/referencia/cheque — el asiento contable no distingue el medio,
/// solo la cuenta de caja/banco que recibió/entregó el efectivo (mismo criterio que
/// <c>CollectionAppliedEvent.FinancialDestinationId</c>, aquí generalizado a N líneas).
/// </summary>
public sealed record SupplierPaymentConfirmedMethodLine(Guid FinancialDestinationId, decimal Amount);

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — se levanta cuando <c>SupplierPayment.Create</c> confirma un
/// pago a proveedor. Deliberadamente no es <c>SupplierPaymentAppliedEvent</c> (Finance) — ese evento
/// pertenece al agregado <c>Payment</c> legacy-AP ya descartado (PAYABLES-PAYMENTS-LEGACY-CLEANUP-14)
/// y su traductor de posting (<c>SupplierPaymentAppliedPostingTranslator</c>) está bloqueado por
/// <c>PaymentsLegacyCleanupTests</c>.
/// </summary>
/// <remarks>
/// SUPPLIER-PAYMENTS-POSTING-15D — <see cref="MethodLines"/> agregado (aditivo, al final del
/// constructor) para que <c>SupplierPaymentConfirmedPostingTranslator</c> pueda generar un crédito
/// por cada medio sin tener que recargar el agregado completo vía <c>ISupplierPaymentRepository</c> —
/// mismo principio que <c>CollectionAppliedEvent</c>: el evento ya transporta los datos resueltos
/// por el módulo de origen, Accounting los consume tal cual.
/// </remarks>
public sealed class SupplierPaymentConfirmedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid SupplierPaymentId { get; }
    public Guid CompanyId { get; }
    public Guid SupplierId { get; }
    public decimal TotalAmount { get; }
    public DateOnly PaymentDate { get; }
    public IReadOnlyList<SupplierPaymentConfirmedMethodLine> MethodLines { get; }

    public SupplierPaymentConfirmedEvent(
        Guid tenantId,
        Guid supplierPaymentId,
        Guid companyId,
        Guid supplierId,
        decimal totalAmount,
        DateOnly paymentDate,
        IReadOnlyList<SupplierPaymentConfirmedMethodLine> methodLines
    )
    {
        TenantId = tenantId;
        SupplierPaymentId = supplierPaymentId;
        CompanyId = companyId;
        SupplierId = supplierId;
        TotalAmount = totalAmount;
        PaymentDate = paymentDate;
        MethodLines = methodLines;
    }

    Guid IAuditEvent.EntityId => SupplierPaymentId;
    string IAuditEvent.Action => "SupplierPaymentConfirmed";
    string? IAuditEvent.Reason => null;
}
