using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.GetWarehouseById;

public sealed class GetWarehouseByIdQueryHandler
    : IRequestHandler<GetWarehouseByIdQuery, Result<WarehouseDetailDto>>
{
    private readonly IWarehouseRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetWarehouseByIdQueryHandler(IWarehouseRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<WarehouseDetailDto>> Handle(
        GetWarehouseByIdQuery request,
        CancellationToken cancellationToken
    )
    {
        var w = await _repo.GetByIdAsync(_currentTenant.TenantId, request.Id, cancellationToken);
        if (w is null)
            return Result<WarehouseDetailDto>.NotFound("Bodega no encontrada.");

        return Result<WarehouseDetailDto>.Success(
            new WarehouseDetailDto(
                w.Id,
                w.BranchId,
                w.Name,
                w.Code,
                w.StorageType,
                w.Address,
                w.Phone,
                w.Email,
                w.Manager,
                w.Latitude,
                w.Longitude,
                w.Capacity,
                w.DailyDispatchGoal,
                w.IsActive,
                w.CreatedAt,
                w.UpdatedAt,
                w.CreatedBy,
                w.UpdatedBy
            )
        );
    }
}
