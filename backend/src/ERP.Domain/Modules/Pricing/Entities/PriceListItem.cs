using ERP.Domain.Common;
using ERP.Domain.Modules.Pricing.Events;

namespace ERP.Domain.Modules.Pricing.Entities;

/// <summary>
/// Asignación puramente administrativa: indica que un Item pertenece a una PriceList.
/// No representa ninguna regla, precio ni ajuste — eso es responsabilidad exclusiva de
/// PricingRule / PriceList.RuleType-RuleValue. Esta entidad no participa en PricingResolver.
/// </summary>
public sealed class PriceListItem : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid PriceListId { get; private set; }
    public Guid ItemId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private PriceListItem() { }

    public static PriceListItem Create(
        Guid tenantId, Guid companyId, Guid priceListId, Guid itemId, Guid createdBy)
    {
        var assignment = new PriceListItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            PriceListId = priceListId,
            ItemId = itemId,
            IsActive = true,
        };
        assignment.SetCreated(createdBy);
        assignment.RaiseDomainEvent(new PriceListItemAssignedEvent
        {
            TenantId = tenantId,
            AssignmentId = assignment.Id,
            PriceListId = priceListId,
            ItemId = itemId,
            AssignedBy = createdBy,
        });
        return assignment;
    }

    public void Enable(Guid updatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("La asignación ya está activa.");
        IsActive = true;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new PriceListItemEnabledEvent
        {
            TenantId = TenantId,
            AssignmentId = Id,
            PriceListId = PriceListId,
            ItemId = ItemId,
            EnabledBy = updatedBy,
        });
    }

    public void Disable(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("La asignación ya está deshabilitada.");
        IsActive = false;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new PriceListItemDisabledEvent
        {
            TenantId = TenantId,
            AssignmentId = Id,
            PriceListId = PriceListId,
            ItemId = ItemId,
            DisabledBy = updatedBy,
        });
    }
}
