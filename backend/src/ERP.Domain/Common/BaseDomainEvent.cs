namespace ERP.Domain.Common;

/// <summary>
/// Convenience base class for new domain events.
/// Adds CorrelationId and optional SubscriberId for AI/analytics traceability.
/// Existing events that implement IDomainEvent directly continue working unchanged.
/// </summary>
public abstract class BaseDomainEvent : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;

    /// <summary>Links this event to the originating request (e.g. HTTP X-Correlation-ID).</summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>Subscriber that owns this event. Null for system-level events.</summary>
    public Guid? SubscriberId { get; init; }

    /// <summary>
    /// The event that caused this one — enables causal chains for analytics and automation.
    /// </summary>
    public Guid? CausationId { get; init; }
}
