using ERP.Domain.Common;
using ERP.Domain.MasterData.Enums;

namespace ERP.Domain.MasterData.Events;

public sealed class BusinessPartnerRoleAssignedEvent : BaseDomainEvent
{
    public Guid RoleId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public RoleType RoleType { get; init; }
    public Guid AssignedBy { get; init; }
}

public sealed class BusinessPartnerRoleReactivatedEvent : BaseDomainEvent
{
    public Guid RoleId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public RoleType RoleType { get; init; }
    public Guid ReactivatedBy { get; init; }
}

public sealed class BusinessPartnerRoleRevokedEvent : BaseDomainEvent
{
    public Guid RoleId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public RoleType RoleType { get; init; }
    public Guid RevokedBy { get; init; }
}

public sealed class BusinessPartnerRoleConfigUpdatedEvent : BaseDomainEvent
{
    public Guid RoleId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public RoleType RoleType { get; init; }
    public Guid UpdatedBy { get; init; }
}
