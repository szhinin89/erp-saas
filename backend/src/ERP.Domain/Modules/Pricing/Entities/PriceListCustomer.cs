using ERP.Domain.Common;
using ERP.Domain.Modules.Pricing.Events;

namespace ERP.Domain.Modules.Pricing.Entities;

/// <summary>
/// Asignación puramente administrativa: indica que una PriceList corresponde a un cliente
/// concreto (BusinessPartner) dentro de una empresa. No representa ninguna regla, precio ni
/// ajuste — eso sigue siendo responsabilidad exclusiva de PricingRule / PriceList.RuleType-
/// RuleValue, resuelto después por PricingResolver contra la lista ya seleccionada. Esta
/// entidad tampoco valida asignación de ítems (PriceListItem) — ambas responsabilidades son
/// ortogonales y se combinan en capas separadas (IPriceListSelectionResolver → PricingResolver).
///
/// CustomerId es un Guid suelto sin FK física a BusinessPartner (mismo criterio que
/// PriceListItem.ItemId no tiene FK física a Item): Pricing no debe depender del tipo
/// BusinessPartner (módulo MasterData) — ver PRICING-CUSTOMER-PRICE-LIST-FOUNDATION-05A.
///
/// Regla de negocio: como máximo UNA relación ACTIVA por (TenantId, CompanyId, CustomerId) —
/// garantizado por índice único parcial en BD (mismo patrón que PriceList.IsDefault), no solo
/// en memoria. Un mismo cliente puede sí tener relaciones activas distintas en Companies
/// distintas del mismo tenant (BusinessPartner es tenant-scoped, PriceList es company-scoped).
/// </summary>
public sealed class PriceListCustomer : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid PriceListId { get; private set; }
    public Guid CustomerId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private PriceListCustomer() { }

    public static PriceListCustomer Create(
        Guid tenantId,
        Guid companyId,
        Guid priceListId,
        Guid customerId,
        Guid createdBy
    )
    {
        var assignment = new PriceListCustomer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            PriceListId = priceListId,
            CustomerId = customerId,
            IsActive = true,
        };
        assignment.SetCreated(createdBy);
        assignment.RaiseDomainEvent(
            new PriceListCustomerAssignedEvent
            {
                TenantId = tenantId,
                AssignmentId = assignment.Id,
                PriceListId = priceListId,
                CustomerId = customerId,
                AssignedBy = createdBy,
            }
        );
        return assignment;
    }

    public void Enable(Guid updatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("La asignación ya está activa.");
        IsActive = true;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new PriceListCustomerEnabledEvent
            {
                TenantId = TenantId,
                AssignmentId = Id,
                PriceListId = PriceListId,
                CustomerId = CustomerId,
                EnabledBy = updatedBy,
            }
        );
    }

    public void Disable(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("La asignación ya está deshabilitada.");
        IsActive = false;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new PriceListCustomerDisabledEvent
            {
                TenantId = TenantId,
                AssignmentId = Id,
                PriceListId = PriceListId,
                CustomerId = CustomerId,
                DisabledBy = updatedBy,
            }
        );
    }
}
