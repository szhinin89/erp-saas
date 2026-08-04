using ERP.Application.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetCurrentStockReport;

public sealed class GetCurrentStockReportQueryHandler
    : IRequestHandler<GetCurrentStockReportQuery, Result<IReadOnlyList<StockReportRowDto>>>
{
    private readonly IStockRepository _stockRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IItemRepository _itemRepo;
    private readonly ICurrentTenant _t;

    public GetCurrentStockReportQueryHandler(
        IStockRepository stockRepo,
        IWarehouseRepository warehouseRepo,
        IItemRepository itemRepo,
        ICurrentTenant t
    )
    {
        _stockRepo = stockRepo;
        _warehouseRepo = warehouseRepo;
        _itemRepo = itemRepo;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<StockReportRowDto>>> Handle(
        GetCurrentStockReportQuery q,
        CancellationToken ct
    )
    {
        if (q.WarehouseId.HasValue)
        {
            var warehouse = await _warehouseRepo.GetByIdAsync(_t.TenantId, q.WarehouseId.Value, ct);
            if (warehouse is null)
                return Result<IReadOnlyList<StockReportRowDto>>.NotFound(
                    "Bodega no encontrada."
                );
        }

        var stocks = await _stockRepo.GetForReportAsync(_t.TenantId, q.WarehouseId, ct);
        if (stocks.Count == 0)
            return Result<IReadOnlyList<StockReportRowDto>>.Success(
                Array.Empty<StockReportRowDto>()
            );

        var warehouses = await _warehouseRepo.GetAsync(
            _t.TenantId,
            activeFilter: null,
            search: null,
            branchId: null,
            ct
        );
        var warehouseNames = warehouses.ToDictionary(w => w.Id, w => w.Name);

        var productIds = stocks.Select(s => s.ProductId).Distinct().ToList();
        var items = await _itemRepo.GetByIdsLightAsync(productIds, _t.TenantId, ct);
        var itemsById = items.ToDictionary(i => i.Id);

        var rows = stocks
            .Select(s => ToRow(s, itemsById, warehouseNames))
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            rows = rows
                .Where(r =>
                    r.Sku.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || r.ProductName.Contains(s, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
        }

        rows = rows
            .OrderBy(r => r.WarehouseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result<IReadOnlyList<StockReportRowDto>>.Success(rows);
    }

    private static StockReportRowDto? ToRow(
        CurrentStock s,
        IReadOnlyDictionary<Guid, Item> itemsById,
        IReadOnlyDictionary<Guid, string> warehouseNames
    )
    {
        if (!itemsById.TryGetValue(s.ProductId, out var item))
            return null;

        var warehouseName = warehouseNames.GetValueOrDefault(s.WarehouseId, "—");
        var minStock = item.StockConfig.MinStockQty;
        var status =
            s.Quantity <= 0
                ? StockReportStatus.SinStock
                : minStock.HasValue && s.Quantity <= minStock.Value
                    ? StockReportStatus.Bajo
                    : StockReportStatus.Disponible;

        return new StockReportRowDto(
            s.ProductId,
            item.Code.SKU,
            item.Code.ShortName,
            s.WarehouseId,
            warehouseName,
            s.Quantity,
            s.AvailableQuantity,
            s.AverageCost,
            s.TotalStockValue,
            status
        );
    }
}
