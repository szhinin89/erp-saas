using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.ListWarehouses;

public sealed class GetWarehousesQueryHandler
    : IRequestHandler<GetWarehousesQuery, Result<IReadOnlyList<WarehouseDto>>>
{
    private readonly IWarehouseRepository _repo;
    private readonly ICurrentSubscriber    _subscriber;

    public GetWarehousesQueryHandler(IWarehouseRepository repo, ICurrentSubscriber subscriber)
    {
        _repo   = repo;
        _subscriber = subscriber;
    }

    public async Task<Result<IReadOnlyList<WarehouseDto>>> Handle(
        GetWarehousesQuery query, CancellationToken ct)
    {
        var list = await _repo.GetAsync(
            _subscriber.SubscriberId, query.ActiveFilter, query.Search, query.BranchId, ct);

        var dtos = list.Select(b => new WarehouseDto(
            b.Id, b.BranchId, b.Name, b.Code, b.StorageType,
            b.Address, b.Phone, b.Email, b.Manager,
            b.Latitude, b.Longitude, b.Capacity, b.DailyDispatchGoal, b.IsActive))
            .ToList();

        return Result<IReadOnlyList<WarehouseDto>>.Success(dtos);
    }
}
