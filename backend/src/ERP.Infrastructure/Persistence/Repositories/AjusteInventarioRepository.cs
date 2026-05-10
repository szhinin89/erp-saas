using Microsoft.EntityFrameworkCore;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class AjusteInventarioRepository : IAjusteInventarioRepository
{
    private readonly ErpDbContext _context;

    public AjusteInventarioRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(AjusteInventario ajuste, CancellationToken ct = default)
        => _context.AjustesInventario.AddAsync(ajuste, ct).AsTask();

    public Task<AjusteInventario?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.AjustesInventario
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);

    public async Task<int> GetNextSecuencialAsync(Guid tenantId, CancellationToken ct = default)
    {
        // MaxAsync nullable — compatible con PostgreSQL e InMemory
        var max = await _context.AjustesInventario
            .Where(a => a.TenantId == tenantId)
            .MaxAsync(a => (int?)a.Secuencial, ct);
        return (max ?? 0) + 1;
    }

    public async Task<(IReadOnlyList<AjusteInventario> Items, int TotalCount)> GetPagedAsync(
        Guid      tenantId,
        int       pageNumber,
        int       pageSize,
        Guid?     bodegaId,
        Guid?     productoId,
        string?   estado,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        CancellationToken ct = default)
    {
        var query = _context.AjustesInventario
            .Where(a => a.TenantId == tenantId);

        if (bodegaId.HasValue)
            query = query.Where(a => a.BodegaId == bodegaId.Value);
        if (productoId.HasValue)
            query = query.Where(a => a.ProductoId == productoId.Value);
        if (!string.IsNullOrEmpty(estado))
            query = query.Where(a => a.Estado == estado);
        if (fechaDesde.HasValue)
            query = query.Where(a => a.FechaAjuste >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(a => a.FechaAjuste <= fechaHasta.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.FechaAjuste)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
