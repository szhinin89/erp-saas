using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Payables.Events;

/// <summary>
/// SUPPLIER-PAYMENTS-REVERSE-16 — snapshot de un <c>SupplierPaymentApplicationLine</c> tal como
/// existía al momento de la reversa. El traductor de posting no lo necesita (el asiento inverso
/// solo requiere <see cref="SupplierPaymentReversedEvent.MethodLines"/>, igual que el asiento de
/// confirmación solo usaba los medios) — se transporta de todas formas porque el ticket lo exige
/// explícitamente como parte de la trazabilidad del evento, y porque un futuro consumidor (p. ej.
/// una notificación o un reporte de auditoría) puede necesitar saber exactamente qué cuotas quedaron
/// afectadas sin tener que recargar el agregado completo.
/// </summary>
public sealed record SupplierPaymentReversedApplicationLine(
    Guid AccountsPayableInstallmentId,
    decimal AmountApplied
);

/// <summary>
/// SUPPLIER-PAYMENTS-REVERSE-16 — se levanta cuando <c>SupplierPayment.Reverse()</c> reversa un
/// pago a proveedor ya confirmado. Deliberadamente NO es
/// <c>ERP.Domain.Modules.Finance.Events.SupplierPaymentReversedEvent</c> (Finance) — ese evento
/// pertenece al agregado <c>Payment</c> legacy-AP ya descartado
/// (PAYABLES-PAYMENTS-LEGACY-CLEANUP-14): nada lo raise en la práctica (ningún comando construye un
/// <c>Payment</c> con <c>Direction.Payment</c>), y su traductor
/// (<c>SupplierPaymentReversedPostingTranslator</c> sobre "Finance"/"SupplierPaymentReversed") era
/// código muerto — se eliminó junto con este ticket para reutilizar el mismo nombre de clase sobre
/// el flujo real de <c>SupplierPayment</c> (Payables).
/// </summary>
public sealed class SupplierPaymentReversedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid SupplierPaymentId { get; }
    public Guid CompanyId { get; }
    public Guid SupplierId { get; }
    public decimal TotalAmount { get; }
    public DateOnly PaymentDate { get; }
    public string ReverseReason { get; }
    public IReadOnlyList<SupplierPaymentConfirmedMethodLine> MethodLines { get; }
    public IReadOnlyList<SupplierPaymentReversedApplicationLine> ApplicationLines { get; }

    public SupplierPaymentReversedEvent(
        Guid tenantId,
        Guid supplierPaymentId,
        Guid companyId,
        Guid supplierId,
        decimal totalAmount,
        DateOnly paymentDate,
        string reverseReason,
        IReadOnlyList<SupplierPaymentConfirmedMethodLine> methodLines,
        IReadOnlyList<SupplierPaymentReversedApplicationLine> applicationLines
    )
    {
        TenantId = tenantId;
        SupplierPaymentId = supplierPaymentId;
        CompanyId = companyId;
        SupplierId = supplierId;
        TotalAmount = totalAmount;
        PaymentDate = paymentDate;
        ReverseReason = reverseReason;
        MethodLines = methodLines;
        ApplicationLines = applicationLines;
    }

    Guid IAuditEvent.EntityId => SupplierPaymentId;
    string IAuditEvent.Action => "SupplierPaymentReversed";
    string? IAuditEvent.Reason => ReverseReason;
}
