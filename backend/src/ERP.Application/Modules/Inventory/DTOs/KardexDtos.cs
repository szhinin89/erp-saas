namespace ERP.Application.Inventory.DTOs;

public record KardexResponse(
    KardexProductDto                  Producto,
    KardexWarehouseDto                    Warehouse,
    IReadOnlyList<KardexMovementDto> Rows,
    KardexSummaryDto                   Resumen);

public record KardexProductDto(Guid Id, string  Name, string Codigo);

public record KardexWarehouseDto(Guid Id, string  Name);

public record KardexMovementDto(
    DateTime Fecha,
    string   MovementType,
    string?  Referencia,
    decimal  InboundQuantity,
    decimal  InboundValue,
    decimal  OutboundQuantity,
    decimal  OutboundValue,
    decimal  BalanceQuantity,
    decimal  BalanceValue,
    decimal  AverageUnitCost);

public record KardexSummaryDto(
    decimal OpeningQuantity,
    decimal OpeningValue,
    decimal TotalInboundQuantity,
    decimal TotalInboundValue,
    decimal TotalOutboundQuantity,
    decimal TotalOutboundValue,
    decimal ClosingQuantity,
    decimal ClosingValue,
    decimal FinalAverageCost);

