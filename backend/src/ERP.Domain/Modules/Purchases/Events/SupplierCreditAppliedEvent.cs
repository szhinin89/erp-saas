using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>
/// Un <see cref="Entities.SupplierCredit"/> se aplicó contra una <c>PurchasePayable</c> destino
/// (<c>SupplierCredit.ApplyToPayable</c>) — diseño §9.3, §19.1 (hecho contable simple: débito CxP
/// destino, crédito "Crédito a favor frente a proveedores").
/// </summary>
public sealed class SupplierCreditAppliedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid SupplierCreditId { get; }
    public Guid SupplierCreditMovementId { get; }
    public Guid TargetPurchasePayableId { get; }
    public Guid CompanyId { get; }
    public decimal Amount { get; }
    public decimal AvailableAmountAfter { get; }
    public Guid UserId { get; }

    Guid IAuditEvent.EntityId => SupplierCreditId;
    string IAuditEvent.Action => "Applied";
    string? IAuditEvent.Reason => null;

    public SupplierCreditAppliedEvent(
        Guid supplierCreditId,
        Guid supplierCreditMovementId,
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
        TargetPurchasePayableId = targetPurchasePayableId;
        TenantId = tenantId;
        CompanyId = companyId;
        Amount = amount;
        AvailableAmountAfter = availableAmountAfter;
        UserId = userId;
    }
}
