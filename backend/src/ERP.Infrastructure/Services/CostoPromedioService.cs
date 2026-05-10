using ERP.Application.Common.Interfaces;
using ERP.Domain.Inventario.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class CostoPromedioService : ICostoPromedioService
{
    private readonly IInventarioStockRepository _stock;

    public CostoPromedioService(IInventarioStockRepository stock) => _stock = stock;

    public async Task<decimal> ObtenerCostoPromedioAsync(
        Guid tenantId, Guid productoId, Guid bodegaId, CancellationToken ct = default)
    {
        var stock = await _stock.GetStockByTenantBodegaProductAsync(tenantId, bodegaId, productoId, ct);
        return stock?.CostoPromedioActual ?? 0m;
    }
}
