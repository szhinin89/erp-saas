using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Sales.UseCases.GetAvailableStockForSale;

public sealed class GetAvailableStockForSaleQueryHandler
    : IRequestHandler<GetAvailableStockForSaleQuery, Result<AvailableStockDto>>
{
    private readonly IStockRepository _stockRepository;
    private readonly ICurrentSubscriber             _currentSubscriber;

    public GetAvailableStockForSaleQueryHandler(
        IStockRepository stockRepository,
        ICurrentSubscriber currentSubscriber)
    {
        _stockRepository = stockRepository;
        _currentSubscriber   = currentSubscriber;
    }

    public async Task<Result<AvailableStockDto>> Handle(
        GetAvailableStockForSaleQuery query, CancellationToken ct)
    {
        var stock = await _stockRepository.GetStockAsync(
            _currentSubscriber.SubscriberId, query.WarehouseId, query.ProductId, ct);

        if (stock is null)
            return Result<AvailableStockDto>.Success(
                new AvailableStockDto(query.ProductId, query.WarehouseId, 0, 0, 0));

        return Result<AvailableStockDto>.Success(new AvailableStockDto(
            stock.ProductId,
            stock.WarehouseId,
            stock.AvailableQuantity,
            stock.Quantity,
            stock.ReservedQuantity));
    }
}
