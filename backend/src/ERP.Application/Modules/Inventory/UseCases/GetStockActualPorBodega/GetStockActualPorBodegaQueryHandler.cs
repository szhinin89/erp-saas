using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.GetCurrentStockPorBodega;

public sealed class GetCurrentStockPorWarehouseQueryHandler
    : IRequestHandler<GetCurrentStockPorWarehouseQuery, Result<IReadOnlyList<CurrentStockListItemDto>>>
{
    private readonly IStockRepository _stock;
    private readonly IWarehouseRepository          _bodegas;
    private readonly ICurrentTenant             _tenant;

    public GetCurrentStockPorWarehouseQueryHandler(
        IStockRepository stock,
        IWarehouseRepository bodegas,
        ICurrentTenant tenant)
    {
        _stock   = stock;
        _bodegas = bodegas;
        _tenant  = tenant;
    }

    public async Task<Result<IReadOnlyList<CurrentStockListItemDto>>> Handle(
        GetCurrentStockPorWarehouseQuery query,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var Warehouse   = await _bodegas.GetByIdAsync(tenantId, query.WarehouseId, ct);
        if (Warehouse is null)
            return Result<IReadOnlyList<CurrentStockListItemDto>>.Failure("Warehouse no encontrada.");

        var rows = await _stock.GetStockByWarehouseAsync(tenantId, query.WarehouseId, query.ProductId, ct);
        var dtos = rows.Select(s => new CurrentStockListItemDto(
            s.Id,
            s.ProductId,
            s.WarehouseId,
            s.Quantity,
            s.ReservedQuantity,
            s.AvailableQuantity,
            s.LastUpdatedAt)).ToList();

        return Result<IReadOnlyList<CurrentStockListItemDto>>.Success(dtos);
    }
}
