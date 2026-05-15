using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class CompraRepository : ICompraRepository
{
    private readonly ErpDbContext _context;

    public CompraRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(CompraFactura compra, CancellationToken ct = default)
        => _context.CompraFacturas.AddAsync(compra, ct).AsTask();

    public Task<CompraFactura?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.CompraFacturas
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public Task<CompraFactura?> GetByIdWithDetailsAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.CompraFacturas
            .Include(c => c.Detalles)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public Task<bool> ExistsClaveAccesoAsync(Guid tenantId, string claveAcceso, CancellationToken ct = default)
        => _context.CompraFacturas
            .AnyAsync(c => c.TenantId == tenantId && c.ClaveAcceso == claveAcceso, ct);

    public async Task<IReadOnlyList<CompraFactura>> GetAsync(
        Guid tenantId,
        EstadoCompra? estado,
        Guid?         proveedorId,
        DateTime?     desde,
        DateTime?     hasta,
        string?       search,
        CancellationToken ct = default)
    {
        var q = _context.CompraFacturas.Where(c => c.TenantId == tenantId);

        if (estado.HasValue)        q = q.Where(c => c.Estado == estado.Value);
        if (proveedorId.HasValue)   q = q.Where(c => c.ProveedorId == proveedorId.Value);
        if (desde.HasValue)         q = q.Where(c => c.FechaFactura >= desde.Value.Date);
        if (hasta.HasValue)         q = q.Where(c => c.FechaFactura <= hasta.Value.Date);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(c =>
                c.NumeroFactura.ToLower().Contains(s) ||
                (c.ClaveAcceso != null && c.ClaveAcceso.Contains(s)));
        }

        return await q.OrderByDescending(c => c.FechaFactura).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CompraBodegaAsignacion>> GetBodegaAsignacionesByCompraFacturaIdAsync(
        Guid tenantId,
        Guid compraFacturaId,
        CancellationToken ct = default)
        => await _context.CompraBodegaAsignaciones
            .Where(a => a.TenantId == tenantId && a.CompraFacturaId == compraFacturaId)
            .OrderBy(a => a.CompraDetalleId)
            .ThenBy(a => a.BodegaId)
            .ToListAsync(ct);

    public Task AddBodegaAsignacionAsync(CompraBodegaAsignacion asignacion, CancellationToken ct = default)
        => _context.CompraBodegaAsignaciones.AddAsync(asignacion, ct).AsTask();

    public Task AddRetencionEmitidaAsync(CompraRetencionEmitida retencion, CancellationToken ct = default)
        => _context.CompraRetencionesEmitidas.AddAsync(retencion, ct).AsTask();

    public Task<CompraRetencionEmitida?> GetRetencionEmitidaByIdWithDetailsAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.CompraRetencionesEmitidas
            .Include(r => r.Proveedor)
            .Include(r => r.Detalles)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

    public async Task<IReadOnlyList<CompraRetencionEmitida>> GetRetencionesEmitidasAsync(
        Guid tenantId,
        Guid? proveedorId,
        CancellationToken ct = default)
    {
        var q = _context.CompraRetencionesEmitidas
            .Include(r => r.Proveedor)
            .Where(r => r.TenantId == tenantId);
        if (proveedorId.HasValue)
            q = q.Where(r => r.ProveedorId == proveedorId.Value);
        return await q.OrderByDescending(r => r.FechaEmision).ToListAsync(ct);
    }

    public Task AddNotaProveedorAsync(CompraNotaProveedor nota, CancellationToken ct = default)
        => _context.CompraNotasProveedor.AddAsync(nota, ct).AsTask();

    public Task<CompraNotaProveedor?> GetNotaProveedorByIdWithDetailsAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
        => _context.CompraNotasProveedor
            .Include(n => n.Detalles)
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id, ct);

    public Task<bool> ExistsNotaProveedorClaveAccesoAsync(
        Guid tenantId,
        string claveAcceso,
        CancellationToken ct = default)
        => _context.CompraNotasProveedor.AnyAsync(
            n => n.TenantId == tenantId && n.ClaveAcceso == claveAcceso, ct);

    public async Task<IReadOnlyList<CompraNotaProveedor>> GetNotasProveedorAsync(
        Guid tenantId,
        Guid? proveedorId,
        Guid? compraFacturaId,
        Guid? gastoFacturaId,
        string? estado,
        CancellationToken ct = default)
    {
        var q = _context.CompraNotasProveedor.Where(n => n.TenantId == tenantId);
        if (proveedorId.HasValue) q = q.Where(n => n.ProveedorId == proveedorId.Value);
        if (compraFacturaId.HasValue) q = q.Where(n => n.CompraFacturaId == compraFacturaId.Value);
        if (gastoFacturaId.HasValue) q = q.Where(n => n.GastoFacturaId == gastoFacturaId.Value);
        if (!string.IsNullOrWhiteSpace(estado))
        {
            var e = estado.Trim();
            q = q.Where(n => n.Estado == e);
        }

        return await q.OrderByDescending(n => n.FechaEmision).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
