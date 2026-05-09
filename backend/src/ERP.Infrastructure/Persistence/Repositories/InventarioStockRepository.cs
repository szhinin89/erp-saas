using Microsoft.EntityFrameworkCore;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class InventarioStockRepository : IInventarioStockRepository
{
    private readonly ErpDbContext _context;

    public InventarioStockRepository(ErpDbContext context) => _context = context;

    public Task<StockActual?> GetStockByTenantBodegaProductAsync(
        Guid tenantId,
        Guid bodegaId,
        Guid productoId,
        CancellationToken ct = default)
        => _context.StockActual.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.BodegaId == bodegaId && s.ProductoId == productoId,
            ct);

    public async Task<IReadOnlyList<StockActual>> GetStockByTenantBodegaAsync(
        Guid tenantId,
        Guid bodegaId,
        Guid? productoId,
        CancellationToken ct = default)
    {
        var q = _context.StockActual
            .Where(s => s.TenantId == tenantId && s.BodegaId == bodegaId);

        if (productoId.HasValue)
            q = q.Where(s => s.ProductoId == productoId.Value);

        return await q
            .OrderBy(s => s.ProductoId)
            .ToListAsync(ct);
    }

    public Task AddStockActualAsync(StockActual entity, CancellationToken ct = default)
        => _context.StockActual.AddAsync(entity, ct).AsTask();

    public Task AddMovimientoAsync(InventarioMovimiento movimiento, CancellationToken ct = default)
        => _context.InventarioMovimientos.AddAsync(movimiento, ct).AsTask();
}
