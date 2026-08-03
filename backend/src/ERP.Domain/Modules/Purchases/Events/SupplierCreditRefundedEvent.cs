using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>
/// Reembolso registrado sobre un <see cref="Entities.SupplierCredit"/> — diseño §9.3, §13.6,
/// §19.1ter. El hecho contable débita <c>SupplierCreditRefundTransaction.AccountingAccountId</c>
/// (congelado desde el destino financiero en la misma transacción) y acredita la cuenta de crédito
/// de proveedor. La creación atómica de <c>SupplierCreditMovement</c> +
/// <c>SupplierCreditRefundTransaction</c> es responsabilidad de Application (Fase 8) — este evento
/// solo transporta el resultado ya calculado por el dominio.
/// </summary>
public sealed class SupplierCreditRefundedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid SupplierCreditId { get; }
    public Guid SupplierCreditMovementId { get; }
    public Guid CompanyId { get; }
    public decimal Amount { get; }
    public decimal AvailableAmountAfter { get; }
    public Guid UserId { get; }

    Guid IAuditEvent.EntityId => SupplierCreditId;
    string IAuditEvent.Action => "Refunded";
    string? IAuditEvent.Reason => null;

    public SupplierCreditRefundedEvent(
        Guid supplierCreditId,
        Guid supplierCreditMovementId,
        Guid tenantId,
        Guid companyId,
        decimal amount,
        decimal availableAmountAfter,
        Guid userId
    )
    {
        SupplierCreditId = supplierCreditId;
        SupplierCreditMovementId = supplierCreditMovementId;
        TenantId = tenantId;
        CompanyId = companyId;
        Amount = amount;
        AvailableAmountAfter = availableAmountAfter;
        UserId = userId;
    }
}
