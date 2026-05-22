namespace ERP.Infrastructure.Persistence.Outbox;

/// <summary>
/// Cross-cutting metadata attached to an OutboxMessage.
/// Serialized as JSON into OutboxMessage.MetadataJson.
///
/// Design intent:
///   - Never store business data here — use the event Payload.
///   - All properties are optional; add only what makes sense per event.
///   - Future: extended by AI tags, workflow IDs, integration routing hints.
/// </summary>
public sealed class OutboxMetadata
{
    /// <summary>ID of the originating HTTP request or background job run.</summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>ID of the event that caused this one (causal chain).</summary>
    public Guid? CausationId { get; init; }

    /// <summary>User that triggered the domain action. Null for system-initiated events.</summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Logical module that owns the event (e.g., "Sales", "Inventory", "Purchasing").
    /// Enables module-level analytics and routing without parsing EventName.
    /// </summary>
    public string? SourceModule { get; init; }

    /// <summary>
    /// Distributed trace ID (e.g., W3C traceparent value).
    /// Reserved for future OpenTelemetry integration — do not populate until tracing is wired.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Free-form tags for AI pipeline routing, experiment flags, or feature annotations.
    /// Example: ["ai:eligible", "analytics:daily-rollup"]
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Number of processing attempts. Incremented by the outbox processor on retry.</summary>
    public int RetryCount { get; init; }
}
