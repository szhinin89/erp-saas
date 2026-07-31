namespace ERP.Application.Modules.Sales.DTOs;

/// <summary>
/// Línea de factura candidata a devolución, con el remanente ya calculado
/// (cantidad original − cantidades ya devueltas en <c>SalesReturn</c> <c>Authorized</c>).
/// </summary>
public sealed record ReturnableLineDto(
    Guid InvoiceDetailId,
    Guid? ItemId,
    string Description,
    string? SnapshotSku,
    Guid? WarehouseId,
    string UomCode,
    decimal OriginalQuantity,
    decimal ReturnedQuantity,
    decimal RemainingQuantity,
    decimal UnitPrice,
    decimal DiscountPct,
    string VatCode,
    decimal VatRate,
    string? IceCode,
    decimal IceRate
);
