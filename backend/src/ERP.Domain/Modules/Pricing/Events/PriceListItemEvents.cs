using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Pricing.Events;

/// <summary>Se levanta cuando <c>PriceListItem.Create()</c> asigna un ítem a una lista de precios.</summary>
public sealed class PriceListItemAssignedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid AssignmentId { get; init; }
    public Guid PriceListId  { get; init; }
    public Guid ItemId       { get; init; }
    public Guid AssignedBy   { get; init; }

    Guid IAuditEvent.EntityId => AssignmentId;
    string IAuditEvent.Action => "Assigned";
    string? IAuditEvent.Reason => null;
}

/// <summary>Se levanta cuando <c>PriceListItem.Enable()</c> reactiva una asignación deshabilitada.</summary>
public sealed class PriceListItemEnabledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid AssignmentId { get; init; }
    public Guid PriceListId  { get; init; }
    public Guid ItemId       { get; init; }
    public Guid EnabledBy    { get; init; }

    Guid IAuditEvent.EntityId => AssignmentId;
    string IAuditEvent.Action => "Enabled";
    string? IAuditEvent.Reason => null;
}

/// <summary>Se levanta cuando <c>PriceListItem.Disable()</c> desactiva una asignación activa.</summary>
public sealed class PriceListItemDisabledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid AssignmentId { get; init; }
    public Guid PriceListId  { get; init; }
    public Guid ItemId       { get; init; }
    public Guid DisabledBy   { get; init; }

    Guid IAuditEvent.EntityId => AssignmentId;
    string IAuditEvent.Action => "Disabled";
    string? IAuditEvent.Reason => null;
}
