using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.GetWarehouses;

public sealed class GetWarehousesQueryHandler
    : IRequestHandler<GetWarehousesQuery, Result<IReadOnlyList<WarehouseListItemDto>>>
{
    private readonly IWarehouseRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetWarehousesQueryHandler(IWarehouseRepository repo, ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<WarehouseListItemDto>>> Handle(
        GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetAsync(
            _currentTenant.TenantId,
            request.ActiveFilter,
            request.Search,
            request.BranchId,
            cancellationToken);

        var dtos = items.Select(ToDto).ToList();
        return Result<IReadOnlyList<WarehouseListItemDto>>.Success(dtos);
    }

    internal static WarehouseListItemDto ToDto(Warehouse w) => new(
        w.Id, w.BranchId, w.Name, w.Code, w.StorageType,
        w.Address, w.Phone, w.Email, w.Manager,
        w.Latitude, w.Longitude, w.Capacity, w.DailyDispatchGoal, w.IsActive);
}
