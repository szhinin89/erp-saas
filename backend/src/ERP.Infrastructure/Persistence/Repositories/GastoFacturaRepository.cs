using Microsoft.EntityFrameworkCore;
using ERP.Domain.Gastos.Entities;
using ERP.Domain.Gastos.Enums;
using ERP.Domain.Gastos.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class GastoFacturaRepository : IGastoFacturaRepository
{
    private readonly ErpDbContext _context;

    public GastoFacturaRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(GastoFactura gasto, CancellationToken ct = default)
        => _context.GastoFacturas.AddAsync(gasto, ct).AsTask();

    public Task<GastoFactura?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.GastoFacturas.FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Id == id, ct);

    public Task<bool> ExistsClaveAccesoAsync(Guid tenantId, string claveAcceso, CancellationToken ct = default)
        => _context.GastoFacturas.AnyAsync(
            g => g.TenantId == tenantId && g.ClaveAcceso == claveAcceso, ct);

    public async Task<IReadOnlyList<GastoFactura>> GetAsync(
        Guid tenantId,
        EstadoGasto? estado,
        Guid? proveedorId,
        DateTime? desde,
        DateTime? hasta,
        string? search,
        CancellationToken ct = default)
    {
        var q = _context.GastoFacturas.Where(g => g.TenantId == tenantId);

        if (estado.HasValue)       q = q.Where(g => g.Estado == estado.Value);
        if (proveedorId.HasValue) q = q.Where(g => g.ProveedorId == proveedorId.Value);
        if (desde.HasValue)        q = q.Where(g => g.FechaEmision >= desde.Value.Date);
        if (hasta.HasValue)        q = q.Where(g => g.FechaEmision <= hasta.Value.Date);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(g =>
                g.Concepto.ToLower().Contains(s) ||
                (g.ClaveAcceso != null && g.ClaveAcceso.Contains(s)) ||
                (g.NumeroFactura != null && g.NumeroFactura.ToLower().Contains(s)));
        }

        return await q.OrderByDescending(g => g.FechaEmision).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
