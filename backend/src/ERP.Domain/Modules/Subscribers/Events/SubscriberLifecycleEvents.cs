using ERP.Domain.Common;

namespace ERP.Domain.Subscribers.Events;

public sealed class SubscriptionActivatedEvent : BaseDomainEvent
{
    public Guid TenantId { get; }
    public Guid ActorId  { get; }
    public string? Notes { get; }

    public SubscriptionActivatedEvent(Guid tenantId, Guid actorId, string? notes = null)
    {
        SubscriberId = tenantId;
        TenantId     = tenantId;
        ActorId      = actorId;
        Notes        = notes;
    }
}

public sealed class SubscriptionSuspendedEvent : BaseDomainEvent
{
    public Guid    TenantId { get; }
    public Guid    ActorId  { get; }
    public string? Reason   { get; }

    public SubscriptionSuspendedEvent(Guid tenantId, Guid actorId, string? reason)
    {
        SubscriberId = tenantId;
        TenantId     = tenantId;
        ActorId      = actorId;
        Reason       = reason;
    }
}

public sealed class TrialStartedEvent : BaseDomainEvent
{
    public Guid     TenantId     { get; }
    public Guid     ActorId      { get; }
    public DateTime EndsAtUtc    { get; }

    public TrialStartedEvent(Guid tenantId, Guid actorId, DateTime endsAtUtc)
    {
        SubscriberId = tenantId;
        TenantId     = tenantId;
        ActorId      = actorId;
        EndsAtUtc    = endsAtUtc;
    }
}

public sealed class GracePeriodStartedEvent : BaseDomainEvent
{
    public Guid     TenantId  { get; }
    public Guid     ActorId   { get; }
    public DateTime EndsAtUtc { get; }

    public GracePeriodStartedEvent(Guid tenantId, Guid actorId, DateTime endsAtUtc)
    {
        SubscriberId = tenantId;
        TenantId     = tenantId;
        ActorId      = actorId;
        EndsAtUtc    = endsAtUtc;
    }
}

public sealed class SubscriptionCancelledEvent : BaseDomainEvent
{
    public Guid    TenantId { get; }
    public Guid    ActorId  { get; }
    public string  Reason   { get; }

    public SubscriptionCancelledEvent(Guid tenantId, Guid actorId, string reason)
    {
        SubscriberId = tenantId;
        TenantId     = tenantId;
        ActorId      = actorId;
        Reason       = reason;
    }
}
