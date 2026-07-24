using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.Domain.Common;

namespace ERP.Infrastructure.Persistence.Outbox;

/// <summary>
/// Builds the MetadataJson payload for an OutboxMessage from a domain event.
/// Centralizes metadata extraction so OutboxMessage.From() stays clean.
///
/// Currently produces minimal metadata (CorrelationId, CausationId from BaseDomainEvent).
/// Extend this factory — not the domain — when new cross-cutting concerns are needed.
/// </summary>
public static class OutboxMetadataFactory
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented          = false,
    };

    /// <summary>
    /// Returns a serialized OutboxMetadata JSON string, or null if the event carries
    /// no extractable metadata (e.g., legacy events that implement IDomainEvent directly).
    /// </summary>
    public static string? Build(IDomainEvent domainEvent, string? sourceModule = null)
    {
        if (domainEvent is not BaseDomainEvent baseEvent)
            return null;

        var metadata = new OutboxMetadata
        {
            CorrelationId = baseEvent.CorrelationId,
            CausationId   = baseEvent.CausationId,
            SourceModule  = sourceModule,
        };

        // Skip serialization if nothing meaningful to store
        if (metadata.CorrelationId is null &&
            metadata.CausationId is null &&
            metadata.SourceModule is null)
            return null;

        return JsonSerializer.Serialize(metadata, Options);
    }

    /// <summary>
    /// Deserializes stored metadata JSON back to <see cref="OutboxMetadata"/>.
    /// Returns null if metadataJson is null/empty or cannot be parsed.
    /// </summary>
    public static OutboxMetadata? Parse(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<OutboxMetadata>(metadataJson, Options);
        }
        catch
        {
            return null;
        }
    }
}
