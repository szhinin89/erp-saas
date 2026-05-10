using Microsoft.EntityFrameworkCore;
using ERP.Domain.Ventas.Entities;
using ERP.Domain.Ventas.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class VentasRepository : IVentasRepository
{
    private readonly ErpDbContext _context;

    public VentasRepository(ErpDbContext context) => _context = context;

    public Task AddFacturaAsync(VentasFactura factura, CancellationToken ct = default)
        => _context.VentasFacturas.AddAsync(factura, ct).AsTask();

    public Task<VentasFactura?> GetFacturaByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.VentasFacturas
            .Include(f => f.Cliente)
            .Include(f => f.Detalles)
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == id, ct);

    public async Task<IReadOnlyList<VentasFactura>> GetFacturasAsync(
        Guid tenantId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        string? estado,
        CancellationToken ct = default)
    {
        var query = _context.VentasFacturas
            .Include(f => f.Cliente)
            .Where(f => f.TenantId == tenantId);

        if (fechaDesde.HasValue)
            query = query.Where(f => f.FechaEmision >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(f => f.FechaEmision <= fechaHasta.Value);
        if (!string.IsNullOrEmpty(estado))
            query = query.Where(f => f.Estado == estado);

        return await query.ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<VentasFactura> Items, int TotalCount)> GetFacturasPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        Guid? clienteId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        string? estado,
        string? search,
        CancellationToken ct = default)
    {
        var query = _context.VentasFacturas
            .Include(f => f.Cliente)
            .Where(f => f.TenantId == tenantId);

        if (clienteId.HasValue)
            query = query.Where(f => f.ClienteId == clienteId.Value);
        if (fechaDesde.HasValue)
            query = query.Where(f => f.FechaEmision >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(f => f.FechaEmision <= fechaHasta.Value);
        if (!string.IsNullOrEmpty(estado))
            query = query.Where(f => f.Estado == estado);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(f =>
                f.Secuencial.Contains(search) ||
                f.ClaveAcceso.Contains(search) ||
                f.NumeroAutorizacion != null && f.NumeroAutorizacion.Contains(search));

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(f => f.FechaEmision)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}