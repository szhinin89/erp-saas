using Microsoft.EntityFrameworkCore;
using ERP.Domain.Compras.Entities;
using ERP.Domain.Compras.Enums;
using ERP.Domain.Compras.Interfaces;

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

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
