using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.PostInitialBalance;

/// <summary>
/// IL-4B — documento de apertura de inventario de UNA bodega (varias líneas), posteado con
/// <c>StockMovementType.InitialBalance</c> y la Fecha de Corte como fecha efectiva del Kardex.
///
/// Camino restringido a apertura — no es un backdating general de ajustes: cada Item+Bodega debe
/// estar sin historia (sin CurrentStock ni movimientos), así el movimiento de apertura es siempre el
/// primero de su Kardex y no reordena ni recalcula historia existente. Los ajustes normales
/// (<c>ExecuteStockAdjustmentCommand</c>) siguen posteando con el día operativo de la empresa.
/// </summary>
public sealed record PostInitialBalanceCommand(
    Guid WarehouseId,
    string WarehouseName,
    Guid ReasonId,
    DateOnly CutoffDate,
    string? Notes,
    IReadOnlyList<CreateStockAdjustmentLineInput> Lines
) : IRequest<Result<StockAdjustmentDto>>, IBranchScopedRequest;
