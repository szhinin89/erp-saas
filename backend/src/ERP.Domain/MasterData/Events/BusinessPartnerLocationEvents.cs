using ERP.Domain.Common;
using ERP.Domain.MasterData.Enums;

namespace ERP.Domain.MasterData.Events;

public sealed class BusinessPartnerLocationCreatedEvent : BaseDomainEvent
{
    public Guid LocationId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public LocationType LocationType { get; init; }
    public Guid CreatedBy { get; init; }
}

public sealed class BusinessPartnerLocationUpdatedEvent : BaseDomainEvent
{
    public Guid LocationId { get; init; }
    public Guid UpdatedBy { get; init; }
}

public sealed class BusinessPartnerLocationDeactivatedEvent : BaseDomainEvent
{
    public Guid LocationId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public Guid DeactivatedBy { get; init; }
}

public sealed class BusinessPartnerPrimaryLocationChangedEvent : BaseDomainEvent
{
    public Guid NewPrimaryLocationId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public Guid ChangedBy { get; init; }
}
