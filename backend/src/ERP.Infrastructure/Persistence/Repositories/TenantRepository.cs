using Microsoft.EntityFrameworkCore;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public class SubscriberRepository : ISubscriberRepository
{
    private readonly ErpDbContext _context;

    public SubscriberRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<Subscriber?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Subscribers.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Subscriber?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await _context.Subscribers.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task<IReadOnlyList<Subscriber>> GetAllAsync(CancellationToken ct = default)
        => await _context.Subscribers
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => await _context.Subscribers.AnyAsync(t => t.Id == id, ct);

    public async Task AddAsync(Subscriber tenant, CancellationToken ct = default)
        => await _context.Subscribers.AddAsync(tenant, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
