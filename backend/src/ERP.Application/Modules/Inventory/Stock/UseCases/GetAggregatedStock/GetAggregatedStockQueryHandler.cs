using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetAggregatedStock;

public sealed class GetAggregatedStockQueryHandler
    : IRequestHandler<GetAggregatedStockQuery, Result<AggregatedStockDto>>
{
    private readonly IStockRepository _repo;
    private readonly ICurrentTenant   _tenant;

    public GetAggregatedStockQueryHandler(IStockRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<AggregatedStockDto>> Handle(
        GetAggregatedStockQuery request, CancellationToken ct)
    {
        var (totalQty, totalVal) = await _repo.GetAggregatedStockAsync(
            _tenant.TenantId, request.ItemId, ct);

        var avgCost = totalQty > 0 ? totalVal / totalQty : 0m;

        return Result<AggregatedStockDto>.Success(
            new AggregatedStockDto(request.ItemId, totalQty, totalVal, avgCost));
    }
}
