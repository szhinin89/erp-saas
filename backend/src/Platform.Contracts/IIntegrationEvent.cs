namespace Platform.Contracts;

/// <summary>
/// Marker for a domain event exported across the ERP/Platform integration boundary.
/// This is the Platform-side mirror of ERP.Domain.Common.IIntegrationEvent —
/// Platform.Contracts does not reference ERP.Domain (ADR-ERP-002).
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
    Guid TenantId { get; }
}
