using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.DisableWarehouse;

/// <summary>
/// ZH-AUTH-INVENTORY-BRANCH-READ-SCOPE-06 — mismo criterio que EnableWarehouseCommand: acción
/// operativa branch-scoped, no de configuración company-wide.
/// </summary>
public sealed record DisableWarehouseCommand(Guid Id)
    : IRequest<Result<WarehouseListItemDto>>,
        IBranchScopedRequest;
