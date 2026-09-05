using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.EnableWarehouse;

/// <summary>
/// ZH-AUTH-INVENTORY-BRANCH-READ-SCOPE-06 — a diferencia de Create/UpdateWarehouseCommand
/// (configuración de bodega, administrada company-wide: un admin puede asignar/editar bodegas de
/// cualquier sucursal desde un único panel de settings), habilitar/deshabilitar es una decisión
/// operativa del día a día ("¿esta bodega está abierta hoy?") que debe tomarse desde la sucursal
/// dueña de la bodega — branch-scoped explícitamente, con validación en el handler.
/// </summary>
public sealed record EnableWarehouseCommand(Guid Id)
    : IRequest<Result<WarehouseListItemDto>>,
        IBranchScopedRequest;
