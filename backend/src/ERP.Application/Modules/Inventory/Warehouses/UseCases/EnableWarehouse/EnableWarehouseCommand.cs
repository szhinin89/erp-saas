using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.EnableWarehouse;

public sealed record EnableWarehouseCommand(Guid Id)
    : IRequest<Result<WarehouseListItemDto>>, ICompanyScopedRequest;
