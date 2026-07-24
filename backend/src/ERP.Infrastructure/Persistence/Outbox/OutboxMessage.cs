using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.Domain.Common;

namespace ERP.Infrastructure.Persistence.Outbox;

/// <summary>
/// Durable record of a domain event, written atomically with the business transaction.
/// Enterprise-ready: EventName + EventVersion enable analytics, versioning, and AI pipelines
/// without coupling the domain to any message bus or external system.
/// </summary>
public sealed class OutboxMessage
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented               = false,
    };

    public Guid Id { get; private set; }

    /// <summary>
    /// Assembly-qualified type name — used internally to deserialize the payload.
    /// Consumers should use <see cref="EventName"/> for routing/filtering, not this field.
    /// </summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>
    /// Clean, stable event name for routing, analytics, and event catalogs.
    /// Example: "InvoiceCreatedEvent". Does NOT include assembly/namespace.
    /// </summary>
    public string EventName { get; private set; } = string.Empty;

    /// <summary>
    /// Schema version of the event. Starts at 1; increment when the payload shape changes
    /// in a way that is not purely additive (see AI-RULES/EVENT-VERSIONING.md).
    /// </summary>
    public int EventVersion { get; private set; } = 1;

    /// <summary>JSON payload of the original domain event.</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>
    /// Optional JSON bag for cross-cutting metadata: trace IDs, AI tags, workflow context,
    /// retry info, integration routing hints. Populated via OutboxMetadataFactory.
    /// Do NOT store business data here — use the Payload.
    /// </summary>
    public string? MetadataJson { get; private set; }

    public DateTime OccurredOnUtc { get; private set; }

    public DateTime? ProcessedOnUtc { get; private set; }

    /// <summary>Last processing error message. Null while pending or after success.</summary>
    public string? Error { get; private set; }

    /// <summary>Tenant that raised the event — denormalized for efficient cross-tenant outbox queries.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Links this event to the originating HTTP request or workflow.</summary>
    public Guid? CorrelationId { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage From(IDomainEvent domainEvent, string? metadataJson = null)
    {
        var eventType  = domainEvent.GetType();
        var tenantId = domainEvent is BaseDomainEvent b ? b.TenantId : null;
        var correlation = domainEvent is BaseDomainEvent b2 ? b2.CorrelationId : null;

        return new OutboxMessage
        {
            Id            = Guid.NewGuid(),
            Type          = eventType.AssemblyQualifiedName!,
            EventName     = eventType.Name,
            EventVersion  = 1,
            Payload       = JsonSerializer.Serialize(domainEvent, eventType, SerializerOptions),
            MetadataJson  = metadataJson,
            OccurredOnUtc = domainEvent.OccurredOn,
            TenantId  = tenantId,
            CorrelationId = correlation,
        };
    }

    public void MarkProcessed()
    {
        ProcessedOnUtc = DateTime.UtcNow;
        Error          = null;
    }

    public void MarkFailed(string error)
    {
        Error = error;
    }
}
