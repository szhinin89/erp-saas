using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class CostoPromedioService : ICostoPromedioService
{
    private readonly IStockRepository _stock;

    public CostoPromedioService(IStockRepository stock) => _stock = stock;

    public async Task<decimal> ObtenerCostoPromedioAsync(
        Guid subscriberId, Guid productoId, Guid WarehouseId, CancellationToken ct = default)
    {
        var stock = await _stock.GetStockAsync(subscriberId, WarehouseId, productoId, ct);
        return stock?.AverageCost ?? 0m;
    }
}
