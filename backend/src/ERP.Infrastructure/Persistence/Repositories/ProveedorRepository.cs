using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly ErpDbContext _context;

    public SupplierRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(Supplier Supplier, CancellationToken ct = default)
        => _context.Suppliers.AddAsync(Supplier, ct).AsTask();

    public Task<Supplier?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
        => _context.Suppliers.FirstOrDefaultAsync(p => p.SubscriberId == subscriberId && p.Id == id, ct);

    public Task<Supplier?> GetByRucAsync(Guid subscriberId, string ruc, CancellationToken ct = default)
    {
        var r = (ruc ?? string.Empty).Trim();
        return _context.Suppliers.FirstOrDefaultAsync(p => p.SubscriberId == subscriberId && p.Ruc == r, ct);
    }

    public async Task<bool> ExistsRucAsync(
        Guid subscriberId,
        string ruc,
        Guid? excludeId,
        CancellationToken ct = default)
    {
        var q = _context.Suppliers
            .Where(p => p.SubscriberId == subscriberId && p.Ruc == ruc.Trim());
        if (excludeId.HasValue)
            q = q.Where(p => p.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    public async Task<IReadOnlyList<Supplier>> GetAsync(
        Guid subscriberId,
        bool? activeFilter,
        string? search,
        string? tipoPersona,
        CancellationToken ct = default)
    {
        var q = _context.Suppliers.Where(p => p.SubscriberId == subscriberId);

        if (activeFilter is true)        q = q.Where(p => p.IsActive);
        else if (activeFilter is false)  q = q.Where(p => !p.IsActive);

        if (!string.IsNullOrWhiteSpace(tipoPersona))
            q = q.Where(p => p.PersonType == tipoPersona);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(p =>
                p.LegalName.ToLower().Contains(s) ||
                p.Ruc.Contains(s) ||
                (p.Email != null && p.Email.ToLower().Contains(s)) ||
                (p.Phone != null && p.Phone.Contains(s)));
        }

        return await q.OrderBy(p => p.LegalName).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
