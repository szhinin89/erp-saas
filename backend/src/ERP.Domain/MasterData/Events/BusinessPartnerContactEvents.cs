using ERP.Domain.Common;
using ERP.Domain.MasterData.Enums;

namespace ERP.Domain.MasterData.Events;

public sealed class BusinessPartnerContactCreatedEvent : BaseDomainEvent
{
    public Guid ContactId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public ContactRole ContactRole { get; init; }
    public Guid CreatedBy { get; init; }
}

public sealed class BusinessPartnerContactUpdatedEvent : BaseDomainEvent
{
    public Guid ContactId { get; init; }
    public Guid UpdatedBy { get; init; }
}

public sealed class BusinessPartnerContactDeactivatedEvent : BaseDomainEvent
{
    public Guid ContactId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public Guid DeactivatedBy { get; init; }
}

public sealed class BusinessPartnerPrimaryContactChangedEvent : BaseDomainEvent
{
    public Guid NewPrimaryContactId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public Guid ChangedBy { get; init; }
}
