using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.GetWarehouseById;

public sealed record GetWarehouseByIdQuery(Guid Id)
    : IRequest<Result<WarehouseDetailDto>>,
        ICompanyScopedRequest;
