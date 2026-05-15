using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class OrdenCompraRepository : IOrdenCompraRepository
{
    private readonly ErpDbContext _context;

    public OrdenCompraRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(OrdenCompra orden, CancellationToken ct = default)
        => _context.OrdenesCompra.AddAsync(orden, ct).AsTask();

    public Task<OrdenCompra?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.OrdenesCompra
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == id, ct);

    public Task<OrdenCompra?> GetByIdWithDetallesAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.OrdenesCompra
            .Include(o => o.Detalles)
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == id, ct);

    public async Task<int> GetNextSecuencialAsync(Guid tenantId, CancellationToken ct = default)
    {
        var max = await _context.OrdenesCompra
            .Where(o => o.TenantId == tenantId)
            .MaxAsync(o => (int?)o.Secuencial, ct);
        return (max ?? 0) + 1;
    }

    public async Task<(IReadOnlyList<OrdenCompra> Items, int TotalCount)> GetPagedAsync(
        Guid      tenantId,
        int       pageNumber,
        int       pageSize,
        Guid?     proveedorId,
        string?   estado,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        CancellationToken ct = default)
    {
        var query = _context.OrdenesCompra
            .Where(o => o.TenantId == tenantId);

        if (proveedorId.HasValue)
            query = query.Where(o => o.ProveedorId == proveedorId.Value);
        if (!string.IsNullOrEmpty(estado))
            query = query.Where(o => o.Estado == estado);
        if (fechaDesde.HasValue)
            query = query.Where(o => o.FechaEmision >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(o => o.FechaEmision <= fechaHasta.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.FechaEmision)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<IReadOnlyList<OrdenCompra>> GetPendientesPorFacturarAsync(
        Guid tenantId, CancellationToken ct = default)
        => _context.OrdenesCompra
            .Where(o => o.TenantId == tenantId
                && (o.Estado == "Aprobada" || o.Estado == "RecibidaParcial")
                && o.Detalles.Any(d => d.CantidadFacturada < d.CantidadPedida))
            .OrderBy(o => o.FechaEmision)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<OrdenCompra>)t.Result, TaskContinuationOptions.ExecuteSynchronously);

    public Task<bool> FacturaYaVinculadaAsync(
        Guid tenantId, Guid ordenId, Guid facturaId, CancellationToken ct = default)
        => _context.OrdenesCompraFacturas
            .AnyAsync(v => v.TenantId == tenantId
                && v.OrdenCompraId == ordenId
                && v.CompraFacturaId == facturaId, ct);

    public async Task<IReadOnlyList<(Guid CompraFacturaId, string NumeroFactura, DateTime FechaVinculacion)>>
        GetVinculacionesAsync(Guid tenantId, Guid ordenId, CancellationToken ct = default)
    {
        var results = await _context.OrdenesCompraFacturas
            .Where(v => v.TenantId == tenantId && v.OrdenCompraId == ordenId)
            .Join(_context.CompraFacturas,
                  v => v.CompraFacturaId,
                  f => f.Id,
                  (v, f) => new { v.CompraFacturaId, f.NumeroFactura, v.FechaVinculacion })
            .ToListAsync(ct);

        return results
            .Select(x => (x.CompraFacturaId, x.NumeroFactura, x.FechaVinculacion))
            .ToList();
    }

    public Task AddOrdenCompraFacturaAsync(OrdenCompraFactura vinculo, CancellationToken ct = default)
        => _context.OrdenesCompraFacturas.AddAsync(vinculo, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
