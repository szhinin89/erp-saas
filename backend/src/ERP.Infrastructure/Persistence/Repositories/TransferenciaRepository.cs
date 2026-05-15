using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class TransferenciaRepository : ITransferenciaRepository
{
    private readonly ErpDbContext _context;

    public TransferenciaRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(Transferencia transferencia, CancellationToken ct = default)
        => _context.Transferencias.AddAsync(transferencia, ct).AsTask();

    public Task<Transferencia?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.Transferencias
            .Include(t => t.BodegaOrigen)
            .Include(t => t.BodegaDestino)
            .Include(t => t.Detalles)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);

    public async Task<int> GetNextSecuencialAsync(Guid tenantId, CancellationToken ct = default)
    {
        // MaxAsync on empty sequence throws; use nullable Max then coalesce.
        // Also compatible with EF InMemory (DefaultIfEmpty+MaxAsync not translatable there).
        var max = await _context.Transferencias
            .Where(t => t.TenantId == tenantId)
            .MaxAsync(t => (int?)t.Secuencial, ct);
        return (max ?? 0) + 1;
    }

    public async Task<(IReadOnlyList<Transferencia> Items, int TotalCount)> GetPagedAsync(
        Guid      tenantId,
        int       pageNumber,
        int       pageSize,
        Guid?     bodegaOrigenId,
        Guid?     bodegaDestinoId,
        string?   estado,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        CancellationToken ct = default)
    {
        var query = _context.Transferencias
            .Include(t => t.BodegaOrigen)
            .Include(t => t.BodegaDestino)
            .Where(t => t.TenantId == tenantId);

        if (bodegaOrigenId.HasValue)
            query = query.Where(t => t.BodegaOrigenId == bodegaOrigenId.Value);
        if (bodegaDestinoId.HasValue)
            query = query.Where(t => t.BodegaDestinoId == bodegaDestinoId.Value);
        if (!string.IsNullOrEmpty(estado))
            query = query.Where(t => t.Estado == estado);
        if (fechaDesde.HasValue)
            query = query.Where(t => t.FechaTransferencia >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(t => t.FechaTransferencia <= fechaHasta.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.FechaTransferencia)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
