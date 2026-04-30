using Microsoft.EntityFrameworkCore;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Auth.ValueObjects;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ErpDbContext _context;

    public UserRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    public async Task<bool> ExistsAsync(string email, Guid tenantId, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _context.Users
            .AnyAsync(u => u.Email == normalized, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _context.Users.AddAsync(user, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
