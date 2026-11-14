using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Events;

/// <summary>Se levanta cuando <c>SupplierExpenseSubcategory.Create()</c> asigna una subcategoría a un proveedor.</summary>
public sealed class SupplierExpenseSubcategoryAssignedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid AssignmentId { get; init; }
    public Guid CompanyId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public Guid ExpenseSubcategoryId { get; init; }
    public Guid AssignedBy { get; init; }

    Guid IAuditEvent.EntityId => AssignmentId;
    string IAuditEvent.Action => "Assigned";
    string? IAuditEvent.Reason => null;
}

/// <summary>Se levanta cuando <c>SupplierExpenseSubcategory.Enable()</c> reactiva una asignación retirada.</summary>
public sealed class SupplierExpenseSubcategoryEnabledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid AssignmentId { get; init; }
    public Guid CompanyId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public Guid ExpenseSubcategoryId { get; init; }
    public Guid EnabledBy { get; init; }

    Guid IAuditEvent.EntityId => AssignmentId;
    string IAuditEvent.Action => "Enabled";
    string? IAuditEvent.Reason => null;
}

/// <summary>Se levanta cuando <c>SupplierExpenseSubcategory.Disable()</c> retira una asignación activa.</summary>
public sealed class SupplierExpenseSubcategoryDisabledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid AssignmentId { get; init; }
    public Guid CompanyId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public Guid ExpenseSubcategoryId { get; init; }
    public Guid DisabledBy { get; init; }

    Guid IAuditEvent.EntityId => AssignmentId;
    string IAuditEvent.Action => "Disabled";
    string? IAuditEvent.Reason => null;
}
