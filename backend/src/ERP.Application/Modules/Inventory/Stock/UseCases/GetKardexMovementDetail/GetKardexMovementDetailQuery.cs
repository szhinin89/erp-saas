using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetKardexMovementDetail;

/// <summary>
/// Expediente completo de un movimiento de Kardex: compone el hecho de inventario
/// (StockMovement, fuente de verdad) con la información comercial/fiscal del documento
/// origen y el actor — sin duplicar datos entre dominios.
/// </summary>
public sealed record GetKardexMovementDetailQuery(Guid MovementId)
    : IRequest<Result<KardexMovementDetailDto>>, IBranchScopedRequest;
