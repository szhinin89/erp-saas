namespace Platform.Contracts.Events;

/// <summary>
/// FUTURE contract: ERP does not currently raise a corresponding domain event for stock adjustments.
/// </summary>
public sealed record StockAdjustedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid TenantId,
    Guid WarehouseId,
    Guid ItemId,
    decimal QuantityDelta,
    string Reason) : IIntegrationEvent;
