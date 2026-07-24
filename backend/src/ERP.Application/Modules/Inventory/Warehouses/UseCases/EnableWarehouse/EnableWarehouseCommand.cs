using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.EnableWarehouse;

public sealed record EnableWarehouseCommand(Guid Id)
    : IRequest<Result<WarehouseListItemDto>>, ICompanyScopedRequest;
