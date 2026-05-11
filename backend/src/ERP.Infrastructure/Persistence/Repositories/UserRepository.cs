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

    /// <summary>
    /// Usuario legacy por id y empresa. Usa <see cref="IgnoreQueryFilters"/> y filtra por <c>TenantId</c>
    /// para no depender del tenant ambiente del <see cref="ErpDbContext"/> (p. ej. requests anónimos).
    /// </summary>
    public async Task<User?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId, ct);

    /// <summary>Lectura cross-tenant por id (operador / diagnóstico). No filtra por empresa.</summary>
    public async Task<User?> GetByIdSystemAsync(Guid id, CancellationToken ct = default)
        => await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <summary>Usuario legacy por email y empresa (tenant explícito, sin depender del filtro global).</summary>
    public async Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == normalized, ct);
    }

    /// <summary>Email + tenant explícitos sin depender del filtro global del DbContext.</summary>
    public async Task<User?> GetByEmailSystemAsync(string email, Guid tenantId, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == normalized, ct);
    }

    /// <summary>SuperAdmin vive en <c>tenant_id = Guid.Empty</c>; IQF necesario y el rol acota la fila.</summary>
    public async Task<User?> GetSingleSuperAdminByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Role == "SuperAdmin" && u.Email == normalized, ct);
    }

    /// <summary>Conteo global de SuperAdmin (sin filtro de empresa).</summary>
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

    /// <summary>Métricas de plataforma: todos los usuarios legacy, todas las empresas.</summary>
    public async Task<int> CountAllSystemAsync(CancellationToken ct = default)
        => await _context.Users
            .IgnoreQueryFilters()
            .CountAsync(ct);

    /// <summary>Métricas de plataforma: usuarios legacy activos en cualquier tenant.</summary>
    public async Task<int> CountActiveSystemAsync(CancellationToken ct = default)
        => await _context.Users
            .IgnoreQueryFilters()
            .CountAsync(u => u.IsActive, ct);

    public async Task<bool> ExistsAsync(string email, Guid tenantId, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.TenantId == tenantId && u.Email == normalized, ct);
    }

    /// <summary>Índice único global por email en <c>users</c>; IQF intencional.</summary>
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
