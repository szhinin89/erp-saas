using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.DisableWarehouse;

public sealed record DisableWarehouseCommand(Guid Id)
    : IRequest<Result<WarehouseListItemDto>>, ICompanyScopedRequest;
