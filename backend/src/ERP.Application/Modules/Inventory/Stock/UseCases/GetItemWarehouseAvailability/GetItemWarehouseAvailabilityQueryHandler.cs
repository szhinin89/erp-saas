using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetItemWarehouseAvailability;

public sealed class GetItemWarehouseAvailabilityQueryHandler
    : IRequestHandler<
        GetItemWarehouseAvailabilityQuery,
        Result<IReadOnlyList<ItemWarehouseAvailabilityDto>>
    >
{
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IStockRepository _stockRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ERP.Domain.Modules.Items.Interfaces.IItemRepository _itemRepo;
    private readonly ERP.Domain.Configuration.Interfaces.IOperationalPreferencesResolver? _preferences;

    public GetItemWarehouseAvailabilityQueryHandler(
        IWarehouseRepository warehouseRepo,
        IStockRepository stockRepo,
        ICurrentTenant tenant,
        ERP.Domain.Modules.Items.Interfaces.IItemRepository itemRepo,
        ERP.Domain.Configuration.Interfaces.IOperationalPreferencesResolver? preferences = null
    )
    {
        _warehouseRepo = warehouseRepo;
        _stockRepo = stockRepo;
        _tenant = tenant;
        _itemRepo = itemRepo;
        _preferences = preferences;
    }

    public async Task<Result<IReadOnlyList<ItemWarehouseAvailabilityDto>>> Handle(
        GetItemWarehouseAvailabilityQuery request,
        CancellationToken ct
    )
    {
        var item = await _itemRepo.GetByIdLightAsync(request.ItemId, _tenant.TenantId, ct);
        if (item is null)
            return Result<IReadOnlyList<ItemWarehouseAvailabilityDto>>.NotFound("Ítem no encontrado.");
        if (!item.ParticipatesInInventory)
            return Result<IReadOnlyList<ItemWarehouseAvailabilityDto>>.Success([]);
        var preferences = _preferences is null ? null : await _preferences.ResolveAsync(ct);
        var enforce = ERP.Domain.Modules.Inventory.Policies.SaleStockPolicy.RequiresAvailableStock(true,
            preferences?.Inventory.StockControlEnabled ?? true, item.StockConfig.StockControlEnabled,
            preferences?.SalesPos.AllowSellWithoutStock ?? false);
        var warehouses = await _warehouseRepo.GetAsync(
            _tenant.TenantId,
            activeFilter: true,
            search: null,
            branchId: null,
            ct
        );
        var stocks = await _stockRepo.GetStockByProductAsync(_tenant.TenantId, request.ItemId, ct);
        var byWarehouse = stocks.ToDictionary(s => s.WarehouseId);

        var dtos = warehouses
            .Select(w =>
            {
                byWarehouse.TryGetValue(w.Id, out var stock);
                var available = stock?.AvailableQuantity ?? 0m;
                return new ItemWarehouseAvailabilityDto(
                    w.Id,
                    w.Name,
                    available,
                    stock?.ReservedQuantity ?? 0m,
                    !enforce || available > 0
                );
            })
            .ToList();

        return Result<IReadOnlyList<ItemWarehouseAvailabilityDto>>.Success(dtos);
    }
}
