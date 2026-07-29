using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.GetWarehouses;

public sealed record GetWarehousesQuery(bool? ActiveFilter, string? Search, Guid? BranchId)
    : IRequest<Result<IReadOnlyList<WarehouseListItemDto>>>,
        ICompanyScopedRequest;
