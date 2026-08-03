using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>
/// Reversa de un reembolso previamente registrado — diseño §9.3, §13.6, §19.1ter. El hecho
/// contable reverso usa la <em>misma</em> cuenta contable congelada en el reembolso original
/// (<c>SupplierCreditRefundTransaction.AccountingAccountId</c> heredado), nunca la cuenta vigente
/// del destino financiero.
/// </summary>
public sealed class SupplierCreditRefundReversedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid SupplierCreditId { get; }
    public Guid SupplierCreditMovementId { get; }
    public Guid ReversalOfMovementId { get; }
    public Guid CompanyId { get; }
    public decimal Amount { get; }
    public decimal AvailableAmountAfter { get; }
    public Guid UserId { get; }

    Guid IAuditEvent.EntityId => SupplierCreditId;
    string IAuditEvent.Action => "RefundReversed";
    string? IAuditEvent.Reason => null;

    public SupplierCreditRefundReversedEvent(
        Guid supplierCreditId,
        Guid supplierCreditMovementId,
        Guid reversalOfMovementId,
        Guid tenantId,
        Guid companyId,
        decimal amount,
        decimal availableAmountAfter,
        Guid userId
    )
    {
        SupplierCreditId = supplierCreditId;
        SupplierCreditMovementId = supplierCreditMovementId;
        ReversalOfMovementId = reversalOfMovementId;
        TenantId = tenantId;
        CompanyId = companyId;
        Amount = amount;
        AvailableAmountAfter = availableAmountAfter;
        UserId = userId;
    }
}
