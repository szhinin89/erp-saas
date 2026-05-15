namespace ERP.Application.Modules.Purchasing.DTOs;

public record PurchaseOrderLineDto(
    Guid    Id,
    Guid    ProductId,
    string  Description,
    decimal CantidadPedida,
    decimal CantidadFacturada,
    decimal PendienteFacturar,
    decimal UnitPrice,
    decimal Subtotal,
    decimal   VatTotal,
    decimal Total);

public record LinkedPurchaseBillDto(
    Guid     PurchBillId,
    string   NumeroFactura,
    DateTime FechaVinculacion);

public record PurchaseOrderDto(
    Guid      Id,
    string    NumeroOrden,
    Guid    SupplierId,
    string    ProveedorNombre,
    DateTime  IssueDate,
    DateTime  FechaRequerida,
    string    Status,
    string    Moneda,
    decimal   Subtotal,
    decimal   VatTotal,
    decimal   Total,
    string? Notes,
    string?   DireccionEntrega,
    Guid?     TargetWarehouseId,
    DateTime? FechaEnvio,
    DateTime? FechaAprobacion,
    Guid?     AprobadoPor,
    DateTime? FechaCierre,
    DateTime  CreatedAt)
{
    /// <summary>
    /// Advertencias generadas al vincular facturas (ej. discrepancias de precio vs OC).
    /// Null si no hay advertencias o la operación no genera advertencias.
    /// </summary>
    public IReadOnlyList<string>? Advertencias { get; init; }
};

public record PurchaseOrderDetailDto(
    Guid      Id,
    string    NumeroOrden,
    Guid    SupplierId,
    string    ProveedorNombre,
    DateTime  IssueDate,
    DateTime  FechaRequerida,
    string    Status,
    string    Moneda,
    decimal   Subtotal,
    decimal   VatTotal,
    decimal   Total,
    string? Notes,
    string?   DireccionEntrega,
    Guid?     TargetWarehouseId,
    DateTime? FechaEnvio,
    DateTime? FechaAprobacion,
    Guid?     AprobadoPor,
    DateTime? FechaCierre,
    DateTime  CreatedAt,
    IReadOnlyList<PurchaseOrderLineDto>          Lines,
    IReadOnlyList<LinkedPurchaseBillDto>  FacturasVinculadas);

public record PurchaseOrdersPagedResult(
    IReadOnlyList<PurchaseOrderDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public record PurchaseOrderItemRequest(
    Guid    ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatPct = 15m);






