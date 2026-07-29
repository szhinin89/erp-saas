using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetKardexByProduct;

/// <summary>
/// Historial completo del Kardex de un producto. Sin <see cref="WarehouseId"/>, retorna
/// el historial en todas las bodegas — cada fila conserva su propio saldo/costo corrido
/// (el Kardex es por producto+bodega, nunca se mezclan saldos entre bodegas).
/// </summary>
public sealed record GetKardexByProductQuery(
    Guid ProductId,
    Guid? WarehouseId = null,
    DateTime? From = null,
    DateTime? To = null
) : IRequest<Result<IReadOnlyList<StockMovementDto>>>, IBranchScopedRequest;
