namespace ERP.Application.Modules.Inventory.DTOs;

public sealed record CurrentStockListItemDto(
    Guid     Id,
    Guid    ProductId,
    Guid    WarehouseId,
    decimal Quantity,
    decimal ReservedQty,
    decimal AvailableQuantity,
    DateTime UltimaActualizacion);

