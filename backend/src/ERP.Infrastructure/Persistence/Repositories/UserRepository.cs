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

    public async Task<User?> GetByIdSystemAsync(Guid id, CancellationToken ct = default)
        => await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    public async Task<User?> GetByEmailSystemAsync(string email, Guid tenantId, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == normalized, ct);
    }

    public async Task<User?> GetSingleSuperAdminByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Role == "SuperAdmin" && u.Email == normalized, ct);
    }

    public async Task<bool> AnySuperAdminAsync(CancellationToken ct = default)
        => await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Role == "SuperAdmin", ct);

    /// <summary>Usuarios del tenant indicado; ignora filtro global para operaciones cross-tenant (p. ej. SuperAdmin con JWT sin empresa).</summary>
    public async Task<IReadOnlyList<User>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(ct);

    public async Task<int> CountAllSystemAsync(CancellationToken ct = default)
        => await _context.Users
            .IgnoreQueryFilters()
            .CountAsync(ct);

    public async Task<int> CountActiveSystemAsync(CancellationToken ct = default)
        => await _context.Users
            .IgnoreQueryFilters()
            .CountAsync(u => u.IsActive, ct);

    public async Task<bool> ExistsAsync(string email, Guid tenantId, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _context.Users
            .AnyAsync(u => u.Email == normalized, ct);
    }

    public async Task<bool> ExistsByEmailGloballyAsync(string email, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == normalized, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _context.Users.AddAsync(user, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
