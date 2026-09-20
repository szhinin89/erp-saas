using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Pricing.Events;

/// <summary>Se levanta cuando <c>PriceListCustomer.Create()</c> asigna una PriceList a un cliente.</summary>
public sealed class PriceListCustomerAssignedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid AssignmentId { get; init; }
    public Guid PriceListId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid AssignedBy { get; init; }

    Guid IAuditEvent.EntityId => AssignmentId;
    string IAuditEvent.Action => "Assigned";
    string? IAuditEvent.Reason => null;
}

/// <summary>Se levanta cuando <c>PriceListCustomer.Enable()</c> reactiva una asignación deshabilitada.</summary>
public sealed class PriceListCustomerEnabledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid AssignmentId { get; init; }
    public Guid PriceListId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid EnabledBy { get; init; }

    Guid IAuditEvent.EntityId => AssignmentId;
    string IAuditEvent.Action => "Enabled";
    string? IAuditEvent.Reason => null;
}

/// <summary>Se levanta cuando <c>PriceListCustomer.Disable()</c> desactiva una asignación activa.</summary>
public sealed class PriceListCustomerDisabledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid AssignmentId { get; init; }
    public Guid PriceListId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid DisabledBy { get; init; }

    Guid IAuditEvent.EntityId => AssignmentId;
    string IAuditEvent.Action => "Disabled";
    string? IAuditEvent.Reason => null;
}
