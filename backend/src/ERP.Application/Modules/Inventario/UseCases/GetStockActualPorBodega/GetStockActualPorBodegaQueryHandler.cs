using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventario.DTOs;
using ERP.Domain.Modules.Inventario.Interfaces;
using ERP.Domain.Modules.Inventario.Interfaces;

namespace ERP.Application.Modules.Inventario.UseCases.GetStockActualPorBodega;

public sealed class GetStockActualPorBodegaQueryHandler
    : IRequestHandler<GetStockActualPorBodegaQuery, Result<IReadOnlyList<StockActualListItemDto>>>
{
    private readonly IInventarioStockRepository _stock;
    private readonly IBodegaRepository          _bodegas;
    private readonly ICurrentTenant             _tenant;

    public GetStockActualPorBodegaQueryHandler(
        IInventarioStockRepository stock,
        IBodegaRepository bodegas,
        ICurrentTenant tenant)
    {
        _stock   = stock;
        _bodegas = bodegas;
        _tenant  = tenant;
    }

    public async Task<Result<IReadOnlyList<StockActualListItemDto>>> Handle(
        GetStockActualPorBodegaQuery query,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var bodega   = await _bodegas.GetByIdAsync(tenantId, query.BodegaId, ct);
        if (bodega is null)
            return Result<IReadOnlyList<StockActualListItemDto>>.Failure("Bodega no encontrada.");

        var rows = await _stock.GetStockByTenantBodegaAsync(tenantId, query.BodegaId, query.ProductoId, ct);
        var dtos = rows.Select(s => new StockActualListItemDto(
            s.Id,
            s.ProductoId,
            s.BodegaId,
            s.Cantidad,
            s.CantidadReservada,
            s.CantidadDisponible,
            s.UltimaActualizacion)).ToList();

        return Result<IReadOnlyList<StockActualListItemDto>>.Success(dtos);
    }
}
