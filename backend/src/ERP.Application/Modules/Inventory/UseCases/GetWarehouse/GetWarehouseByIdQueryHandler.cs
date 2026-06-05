using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.GetWarehouse;

public sealed class GetWarehouseByIdQueryHandler
    : IRequestHandler<GetWarehouseByIdQuery, Result<WarehouseDetailDto?>>
{
    private readonly IWarehouseRepository _repo;
    private readonly ICurrentSubscriber    _subscriber;

    public GetWarehouseByIdQueryHandler(IWarehouseRepository repo, ICurrentSubscriber subscriber)
    {
        _repo   = repo;
        _subscriber = subscriber;
    }

    public async Task<Result<WarehouseDetailDto?>> Handle(
        GetWarehouseByIdQuery query, CancellationToken ct)
    {
        var b = await _repo.GetByIdAsync(_subscriber.SubscriberId, query.Id, ct);
        if (b is null) return Result<WarehouseDetailDto?>.Success(null);

        return Result<WarehouseDetailDto?>.Success(new WarehouseDetailDto(
            b.Id, b.BranchId, b.Name, b.Code, b.StorageType,
            b.Address, b.Phone, b.Email, b.Manager,
            b.Latitude, b.Longitude, b.Capacity, b.DailyDispatchGoal,
            b.IsActive, b.CreatedAt, b.UpdatedAt, b.CreatedBy, b.UpdatedBy));
    }
}
