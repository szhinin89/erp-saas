namespace ERP.Application.Inventory.DTOs;

public record StockAdjustmentDto(
    Guid      Id,
    string    NumeroAjuste,
    Guid    WarehouseId,
    string    BodegaNombre,
    Guid    ProductId,
    string    ProductoNombre,
    decimal   CantidadAjuste,
    string    TipoAjuste,
    string  Reason,
    string? Notes,
    DateTime  FechaAjuste,
    string    Status,
    DateTime? FechaEjecucion,
    Guid?     EjecutadoPor,
    DateTime  CreatedAt);

public record AjustesPagedResult(
    IReadOnlyList<StockAdjustmentDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
