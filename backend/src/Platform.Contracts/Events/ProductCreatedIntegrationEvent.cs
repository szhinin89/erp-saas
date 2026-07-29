namespace Platform.Contracts.Events;

/// <summary>
/// Raised when a new product/item is created in the ERP and exported to Platform.
/// </summary>
public sealed record ProductCreatedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid TenantId,
    Guid ItemId,
    string Sku,
    string ItemType
) : IIntegrationEvent;
