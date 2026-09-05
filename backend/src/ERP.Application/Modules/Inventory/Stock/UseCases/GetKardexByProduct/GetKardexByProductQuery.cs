using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetKardexByProduct;

/// <summary>
/// Historial completo del Kardex de un producto. Sin <see cref="WarehouseId"/>, retorna
/// el historial en todas las bodegas — cada fila conserva su propio saldo/costo corrido
/// (el Kardex es por producto+bodega, nunca se mezclan saldos entre bodegas).
///
/// ZH-AUTH-INVENTORY-BRANCH-READ-SCOPE-06 — este query es <b>company-wide por diseño</b>, no
/// filtrado por sucursal activa: trazar un producto entre bodegas de distintas sucursales (p.ej.
/// tras un StockTransfer) requiere poder ver el historial completo de la empresa, no solo el de la
/// sucursal actual. Aislamiento real: (1) <c>IStockRepository.GetMovementsByProductAsync</c>
/// ya filtra por <c>ICurrentCompany</c> vía <c>ForOperationalScope</c> — nunca cruza empresas; (2)
/// el endpoint exige <c>perm:{InventoryPermissions.StockView}</c>. Mantiene <c>IBranchScopedRequest</c>
/// únicamente para exigir que exista una sucursal operativa válida en el contexto (mismo requisito
/// que el resto del módulo), no para restringir los resultados a esa sucursal.
/// </summary>
public sealed record GetKardexByProductQuery(
    Guid ProductId,
    Guid? WarehouseId = null,
    DateTime? From = null,
    DateTime? To = null
) : IRequest<Result<IReadOnlyList<StockMovementDto>>>, IBranchScopedRequest;
