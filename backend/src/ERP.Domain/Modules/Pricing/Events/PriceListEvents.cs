using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Pricing.Events;

/// <summary>Se levanta cuando <c>PriceList.Create()</c> crea una nueva lista de precios.</summary>
public sealed class PriceListCreatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid CompanyId { get; }
    public Guid PriceListId { get; }

    public PriceListCreatedEvent(Guid tenantId, Guid companyId, Guid priceListId)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PriceListId = priceListId;
    }

    Guid IAuditEvent.EntityId => PriceListId;
    string IAuditEvent.Action => "Created";
    string? IAuditEvent.Reason => null;
}

/// <summary>Se levanta cuando <c>PriceList.Enable()</c> reactiva una lista deshabilitada.</summary>
public sealed class PriceListEnabledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid CompanyId { get; }
    public Guid PriceListId { get; }

    public PriceListEnabledEvent(Guid tenantId, Guid companyId, Guid priceListId)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PriceListId = priceListId;
    }

    Guid IAuditEvent.EntityId => PriceListId;
    string IAuditEvent.Action => "Enabled";
    string? IAuditEvent.Reason => null;
}

/// <summary>Se levanta cuando <c>PriceList.Disable()</c> desactiva una lista activa.</summary>
public sealed class PriceListDisabledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid CompanyId { get; }
    public Guid PriceListId { get; }

    public PriceListDisabledEvent(Guid tenantId, Guid companyId, Guid priceListId)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PriceListId = priceListId;
    }

    Guid IAuditEvent.EntityId => PriceListId;
    string IAuditEvent.Action => "Disabled";
    string? IAuditEvent.Reason => null;
}
