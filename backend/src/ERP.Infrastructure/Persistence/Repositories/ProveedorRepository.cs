using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class ProveedorRepository : IProveedorRepository
{
    private readonly ErpDbContext _context;

    public ProveedorRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(Proveedor proveedor, CancellationToken ct = default)
        => _context.Proveedores.AddAsync(proveedor, ct).AsTask();

    public Task<Proveedor?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.Proveedores.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public Task<Proveedor?> GetByRucAsync(Guid tenantId, string ruc, CancellationToken ct = default)
    {
        var r = (ruc ?? string.Empty).Trim();
        return _context.Proveedores.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Ruc == r, ct);
    }

    public async Task<bool> ExistsRucAsync(
        Guid tenantId,
        string ruc,
        Guid? excludeId,
        CancellationToken ct = default)
    {
        var q = _context.Proveedores
            .Where(p => p.TenantId == tenantId && p.Ruc == ruc.Trim());
        if (excludeId.HasValue)
            q = q.Where(p => p.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    public async Task<IReadOnlyList<Proveedor>> GetAsync(
        Guid tenantId,
        bool? activeFilter,
        string? search,
        string? tipoPersona,
        CancellationToken ct = default)
    {
        var q = _context.Proveedores.Where(p => p.TenantId == tenantId);

        if (activeFilter is true)        q = q.Where(p => p.IsActive);
        else if (activeFilter is false)  q = q.Where(p => !p.IsActive);

        if (!string.IsNullOrWhiteSpace(tipoPersona))
            q = q.Where(p => p.TipoPersona == tipoPersona);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(p =>
                p.RazonSocial.ToLower().Contains(s) ||
                p.Ruc.Contains(s) ||
                (p.Correo != null && p.Correo.ToLower().Contains(s)) ||
                (p.Telefono != null && p.Telefono.Contains(s)));
        }

        return await q.OrderBy(p => p.RazonSocial).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
