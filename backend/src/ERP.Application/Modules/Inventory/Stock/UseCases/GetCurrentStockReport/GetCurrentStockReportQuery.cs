using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetCurrentStockReport;

/// <summary>
/// Reporte básico de stock actual por bodega (piloto Sumak). Branch-scoped por exigencia de
/// contexto (defensa en profundidad vía BranchScopeBehavior/IBranchAccessGuard) — CurrentStock
/// no tiene BranchId propio (solo vía Warehouse.BranchId), así que el resultado es
/// company-scoped, igual que GetStockQuery.
/// </summary>
public sealed record GetCurrentStockReportQuery(Guid? WarehouseId = null, string? Search = null)
    : IRequest<Result<IReadOnlyList<StockReportRowDto>>>,
        IBranchScopedRequest;

/// <summary>Estado calculado exclusivamente desde datos ya existentes (Quantity + ItemStockConfig.MinStockQty) — sin reglas de negocio nuevas.</summary>
public enum StockReportStatus
{
    SinStock,
    Bajo,
    Disponible,
}

public sealed record StockReportRowDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    Guid WarehouseId,
    string WarehouseName,
    decimal Quantity,
    decimal AvailableQuantity,
    decimal AverageCost,
    decimal StockValue,
    StockReportStatus Status
);
