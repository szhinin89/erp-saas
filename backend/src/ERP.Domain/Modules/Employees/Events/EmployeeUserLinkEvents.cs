using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Employees.Events;

/// <summary>
/// Vinculación (<c>UserLinked</c>) o desvinculación (<c>UserUnlinked</c>) de un usuario ERP a la ficha
/// (EMP-BR-13/14). Solo identificadores: sin nombre, correo ni otros datos personales.
/// </summary>
public sealed class EmployeeUserLinkChangedEvent : BaseDomainEvent, IAuditEvent
{
    public const string LinkedAction = "UserLinked";
    public const string UnlinkedAction = "UserUnlinked";
    public const string IdentityUserField = "IdentityUser";

    public Guid CompanyId { get; }
    public Guid EmployeeId { get; }
    public Guid BusinessPartnerId { get; }
    public string Action { get; }
    public Guid? OldIdentityUserId { get; }
    public Guid? NewIdentityUserId { get; }

    public EmployeeUserLinkChangedEvent(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        Guid businessPartnerId,
        string action,
        Guid? oldIdentityUserId,
        Guid? newIdentityUserId
    )
    {
        TenantId = tenantId;
        CompanyId = companyId;
        EmployeeId = employeeId;
        BusinessPartnerId = businessPartnerId;
        Action = action;
        OldIdentityUserId = oldIdentityUserId;
        NewIdentityUserId = newIdentityUserId;
    }

    Guid IAuditEvent.EntityId => EmployeeId;
    string IAuditEvent.Action => Action;
    string? IAuditEvent.Reason => null;
}
