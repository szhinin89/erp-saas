using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Events;

public sealed class BusinessPartnerCreatedEvent : BaseDomainEvent
{
    public Guid BusinessPartnerId { get; init; }
    public string IdentificationType { get; init; } = null!;
    public string IdentificationNumber { get; init; } = null!;
    public string LegalName { get; init; } = null!;
    public Guid CreatedBy { get; init; }
}

public sealed class BusinessPartnerActivatedEvent : BaseDomainEvent
{
    public Guid BusinessPartnerId { get; init; }
    public Guid ActivatedBy { get; init; }
}

public sealed class BusinessPartnerDeactivatedEvent : BaseDomainEvent
{
    public Guid BusinessPartnerId { get; init; }
    public Guid DeactivatedBy { get; init; }
}

public sealed class BusinessPartnerIdentificationChangedEvent : BaseDomainEvent
{
    public Guid BusinessPartnerId { get; init; }
    public string OldType { get; init; } = null!;
    public string OldNumber { get; init; } = null!;
    public string NewType { get; init; } = null!;
    public string NewNumber { get; init; } = null!;
    public Guid ChangedBy { get; init; }
}

public sealed class BusinessPartnerProfileUpdatedEvent : BaseDomainEvent
{
    public Guid BusinessPartnerId { get; init; }
    public string LegalName { get; init; } = null!;
    public Guid UpdatedBy { get; init; }
}
