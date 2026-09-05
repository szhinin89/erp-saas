using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetStockMovements;

/// <summary>
/// ZH-AUTH-INVENTORY-BRANCH-READ-SCOPE-06 — a diferencia de GetKardexByProduct/GetKardexByDocument/
/// GetKardexMovementDetail (company-wide por diseño), aquí <see cref="WarehouseId"/> es obligatorio:
/// el handler valida que esa bodega pertenezca a la sucursal activa antes de consultar movimientos.
/// </summary>
public sealed record GetStockMovementsQuery(
    Guid ItemId,
    Guid WarehouseId,
    DateTime? From,
    DateTime? To
) : IRequest<Result<IReadOnlyList<StockMovementDto>>>, IBranchScopedRequest;
