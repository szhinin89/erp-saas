using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class BodegaRepository : IBodegaRepository
{
    private readonly ErpDbContext _context;

    public BodegaRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(Bodega bodega, CancellationToken ct = default)
        => _context.Bodegas.AddAsync(bodega, ct).AsTask();

    public Task<Bodega?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.Bodegas.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == id, ct);

    public async Task<bool> ExistsNombreAsync(
        Guid tenantId,
        string nombre,
        Guid? excludeId,
        CancellationToken ct = default)
    {
        var q = _context.Bodegas
            .Where(b => b.TenantId == tenantId && b.Nombre == nombre.Trim());
        if (excludeId.HasValue)
            q = q.Where(b => b.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    public async Task<IReadOnlyList<Bodega>> GetAsync(
        Guid tenantId,
        bool? activeFilter,
        string? search,
        Guid? sucursalId,
        CancellationToken ct = default)
    {
        var q = _context.Bodegas.Where(b => b.TenantId == tenantId);

        if (activeFilter is true)  q = q.Where(b => b.IsActive);
        else if (activeFilter is false) q = q.Where(b => !b.IsActive);

        if (sucursalId.HasValue) q = q.Where(b => b.SucursalId == sucursalId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(b =>
                b.Nombre.ToLower().Contains(s) ||
                (b.Ubicacion != null && b.Ubicacion.ToLower().Contains(s)) ||
                (b.Encargado != null && b.Encargado.ToLower().Contains(s)));
        }

        return await q.OrderBy(b => b.Nombre).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
