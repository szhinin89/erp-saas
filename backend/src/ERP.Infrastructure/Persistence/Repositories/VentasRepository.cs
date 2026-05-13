using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Ventas.Entities;
using ERP.Domain.Modules.Ventas.Interfaces;

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

    public Task AddNotaCreditoDebitoAsync(VentasNotaCreditoDebito nota, CancellationToken ct = default)
        => _context.VentasNotasCreditoDebito.AddAsync(nota, ct).AsTask();

    public Task<VentasNotaCreditoDebito?> GetNotaByIdWithDetailsAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.VentasNotasCreditoDebito
            .Include(n => n.FacturaOriginal)
                .ThenInclude(f => f.Cliente)
            .Include(n => n.Detalles)
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id, ct);

    public async Task<IReadOnlyList<VentasNotaCreditoDebito>> GetNotasAsync(
        Guid tenantId,
        Guid? facturaOriginalId,
        string? estado,
        CancellationToken ct = default)
    {
        var q = _context.VentasNotasCreditoDebito
            .Include(n => n.FacturaOriginal)
                .ThenInclude(f => f.Cliente)
            .Where(n => n.TenantId == tenantId);

        if (facturaOriginalId.HasValue)
            q = q.Where(n => n.VentasFacturaOriginalId == facturaOriginalId.Value);
        if (!string.IsNullOrWhiteSpace(estado))
            q = q.Where(n => n.Estado == estado);

        return await q.OrderByDescending(n => n.FechaEmision).ToListAsync(ct);
    }

    public Task AddRetencionRecibidaAsync(VentasRetencionRecibida retencion, CancellationToken ct = default)
        => _context.VentasRetencionesRecibidas.AddAsync(retencion, ct).AsTask();

    public async Task<IReadOnlyList<VentasRetencionRecibida>> GetRetencionesRecibidasAsync(
        Guid tenantId,
        CancellationToken ct = default)
        => await _context.VentasRetencionesRecibidas
            .Include(r => r.Cliente)
            .Include(r => r.Detalles)
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.FechaEmision)
            .ToListAsync(ct);

    public Task<bool> ExistsRetencionRecibidaClaveAsync(Guid tenantId, string claveAcceso, CancellationToken ct = default)
        => _context.VentasRetencionesRecibidas.AnyAsync(
            r => r.TenantId == tenantId && r.ClaveAcceso == claveAcceso, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}