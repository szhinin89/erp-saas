using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Sales.UseCases.GetStockDisponibleParaVenta;

public sealed class GetAvailableStockForSaleQueryHandler
    : IRequestHandler<GetAvailableStockForSaleQuery, Result<StockDisponibleDto>>
{
    private readonly IStockRepository _stockRepository;
    private readonly ICurrentTenant             _currentTenant;

    public GetAvailableStockForSaleQueryHandler(
        IStockRepository stockRepository,
        ICurrentTenant currentTenant)
    {
        _stockRepository = stockRepository;
        _currentTenant   = currentTenant;
    }

    public async Task<Result<StockDisponibleDto>> Handle(
        GetAvailableStockForSaleQuery query, CancellationToken ct)
    {
        var stock = await _stockRepository.GetStockAsync(
            _currentTenant.TenantId, query.WarehouseId, query.ProductId, ct);

        if (stock is null)
            return Result<StockDisponibleDto>.Success(
                new StockDisponibleDto(query.ProductId, query.WarehouseId, 0, 0, 0));

        return Result<StockDisponibleDto>.Success(new StockDisponibleDto(
            stock.ProductId,
            stock.WarehouseId,
            stock.AvailableQuantity,
            stock.Quantity,
            stock.ReservedQuantity));
    }
}
