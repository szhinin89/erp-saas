using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Sales.UseCases.GetStockDisponibleParaVenta;

public sealed class GetStockDisponibleParaVentaQueryHandler
    : IRequestHandler<GetStockDisponibleParaVentaQuery, Result<StockDisponibleDto>>
{
    private readonly IInventarioStockRepository _stockRepository;
    private readonly ICurrentTenant             _currentTenant;

    public GetStockDisponibleParaVentaQueryHandler(
        IInventarioStockRepository stockRepository,
        ICurrentTenant currentTenant)
    {
        _stockRepository = stockRepository;
        _currentTenant   = currentTenant;
    }

    public async Task<Result<StockDisponibleDto>> Handle(
        GetStockDisponibleParaVentaQuery query, CancellationToken ct)
    {
        var stock = await _stockRepository.GetStockByTenantBodegaProductAsync(
            _currentTenant.TenantId, query.BodegaId, query.ProductoId, ct);

        if (stock is null)
            return Result<StockDisponibleDto>.Success(
                new StockDisponibleDto(query.ProductoId, query.BodegaId, 0, 0, 0));

        return Result<StockDisponibleDto>.Success(new StockDisponibleDto(
            stock.ProductoId,
            stock.BodegaId,
            stock.CantidadDisponible,
            stock.Cantidad,
            stock.CantidadReservada));
    }
}
