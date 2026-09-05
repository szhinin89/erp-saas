using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetKardexMovementDetail;

/// <summary>
/// Expediente completo de un movimiento de Kardex: compone el hecho de inventario
/// (StockMovement, fuente de verdad) con la información comercial/fiscal del documento
/// origen y el actor — sin duplicar datos entre dominios.
///
/// ZH-AUTH-INVENTORY-BRANCH-READ-SCOPE-06 — <b>company-wide por diseño</b>, coherente con
/// GetKardexByProductQuery/GetKardexByDocumentQuery: el detalle de un movimiento devuelto por
/// esas consultas (que ya listan cruzando sucursales) debe poder abrirse sin importar a qué
/// sucursal pertenezca su bodega. Aislamiento real: <c>IStockRepository.GetMovementByIdAsync</c>
/// filtra por <c>ICurrentCompany</c> (nunca cruza empresas) y el endpoint exige
/// <c>perm:{InventoryPermissions.StockView}</c>.
/// </summary>
public sealed record GetKardexMovementDetailQuery(Guid MovementId)
    : IRequest<Result<KardexMovementDetailDto>>,
        IBranchScopedRequest;
