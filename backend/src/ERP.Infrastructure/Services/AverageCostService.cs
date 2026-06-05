using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class AverageCostService : IAverageCostService
{
    private readonly IStockRepository _stock;

    public AverageCostService(IStockRepository stock) => _stock = stock;

    public async Task<decimal> ObtenerCostoPromedioAsync(
        Guid subscriberId, Guid productoId, Guid WarehouseId, CancellationToken ct = default)
    {
        var stock = await _stock.GetStockAsync(subscriberId, WarehouseId, productoId, ct);
        return stock?.AverageCost ?? 0m;
    }
}
