using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetStock;

public sealed class GetStockQueryHandler
    : IRequestHandler<GetStockQuery, Result<IReadOnlyList<CurrentStockDto>>>
{
    private readonly IStockRepository _repo;
    private readonly ICurrentTenant _tenant;

    public GetStockQueryHandler(IStockRepository repo, ICurrentTenant tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<CurrentStockDto>>> Handle(
        GetStockQuery request,
        CancellationToken ct
    )
    {
        if (request.ItemId is null && request.WarehouseId is null)
            return Result<IReadOnlyList<CurrentStockDto>>.Failure(
                "Debe indicar al menos itemId o warehouseId."
            );

        IReadOnlyList<CurrentStock> stocks;

        if (request.WarehouseId.HasValue)
        {
            stocks = await _repo.GetStockByWarehouseAsync(
                _tenant.TenantId,
                request.WarehouseId.Value,
                request.ItemId,
                ct
            );
        }
        else
        {
            stocks = await _repo.GetStockByProductAsync(
                _tenant.TenantId,
                request.ItemId!.Value,
                ct
            );
        }

        var dtos = stocks.Select(ToDto).ToList();
        return Result<IReadOnlyList<CurrentStockDto>>.Success(dtos);
    }

    internal static CurrentStockDto ToDto(CurrentStock s) =>
        new(
            s.Id,
            s.ProductId,
            s.WarehouseId,
            s.Quantity,
            s.ReservedQuantity,
            s.AvailableQuantity,
            s.TotalStockValue,
            s.AverageCost,
            s.LastUpdatedAt
        );
}
