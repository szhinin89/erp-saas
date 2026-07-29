using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.DisableWarehouse;

public sealed record DisableWarehouseCommand(Guid Id)
    : IRequest<Result<WarehouseListItemDto>>,
        ICompanyScopedRequest;
