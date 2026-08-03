using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>
/// Reversa de una aplicación previa de <see cref="Entities.SupplierCredit"/> — diseño §9.3
/// (hecho contable reverso del de <see cref="SupplierCreditAppliedEvent"/>).
/// </summary>
public sealed class SupplierCreditApplicationReversedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid SupplierCreditId { get; }
    public Guid SupplierCreditMovementId { get; }
    public Guid ReversalOfMovementId { get; }
    public Guid TargetPurchasePayableId { get; }
    public Guid CompanyId { get; }
    public decimal Amount { get; }
    public decimal AvailableAmountAfter { get; }
    public Guid UserId { get; }

    Guid IAuditEvent.EntityId => SupplierCreditId;
    string IAuditEvent.Action => "ApplicationReversed";
    string? IAuditEvent.Reason => null;

    public SupplierCreditApplicationReversedEvent(
        Guid supplierCreditId,
        Guid supplierCreditMovementId,
        Guid reversalOfMovementId,
        Guid targetPurchasePayableId,
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
        TargetPurchasePayableId = targetPurchasePayableId;
        TenantId = tenantId;
        CompanyId = companyId;
        Amount = amount;
        AvailableAmountAfter = availableAmountAfter;
        UserId = userId;
    }
}
