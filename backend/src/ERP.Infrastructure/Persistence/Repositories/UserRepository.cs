using ERP.Application.Common;
using Microsoft.EntityFrameworkCore;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Auth.ValueObjects;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ErpDbContext _context;
    private readonly IPlatformQueryAccessor _platform;

    public UserRepository(ErpDbContext context, IPlatformQueryAccessor platform)
    {
        _context = context;
        _platform = platform;
    }

    /// <summary>
    /// Usuario legacy por id y empresa. Consulta sin filtro global y filtra por <c>SubscriberId</c>
    /// para no depender del tenant ambiente del <see cref="ErpDbContext"/> (p. ej. requests anónimos).
    /// </summary>
    public async Task<User?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
        => await _platform.Unfiltered(_context.Users, PlatformQueryReason.TenantScopedExplicit)
            .FirstOrDefaultAsync(u => u.Id == id && u.SubscriberId == subscriberId, ct);

    /// <summary>Lectura cross-tenant por id (operador / diagnóstico). No filtra por empresa.</summary>
    public async Task<User?> GetByIdSystemAsync(Guid id, CancellationToken ct = default)
        => await _platform.Unfiltered(_context.Users, PlatformQueryReason.CrossTenantSystem)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <summary>Usuario legacy por email y empresa (tenant explícito, sin depender del filtro global).</summary>
    public async Task<User?> GetByEmailAsync(string email, Guid subscriberId, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _platform.Unfiltered(_context.Users, PlatformQueryReason.TenantScopedExplicit)
            .FirstOrDefaultAsync(u => u.SubscriberId == subscriberId && u.Email == normalized, ct);
    }

    /// <summary>Email + tenant explícitos sin depender del filtro global del DbContext.</summary>
    public async Task<User?> GetByEmailSystemAsync(string email, Guid subscriberId, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _platform.Unfiltered(_context.Users, PlatformQueryReason.TenantScopedExplicit)
            .FirstOrDefaultAsync(u => u.SubscriberId == subscriberId && u.Email == normalized, ct);
    }

    /// <summary>SuperAdmin vive en <c>subscriber_id = Guid.Empty</c>; consulta de plataforma y el rol acota la fila.</summary>
    public async Task<User?> GetSingleSuperAdminByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _platform.Unfiltered(_context.Users, PlatformQueryReason.CrossTenantSystem)
            .FirstOrDefaultAsync(u => u.Role == "SuperAdmin" && u.Email == normalized, ct);
    }

    /// <summary>Conteo global de SuperAdmin (sin filtro de empresa).</summary>
    public async Task<bool> AnySuperAdminAsync(CancellationToken ct = default)
        => await _platform.Unfiltered(_context.Users, PlatformQueryReason.CrossTenantSystem)
            .AnyAsync(u => u.Role == "SuperAdmin", ct);

    /// <summary>Usuarios del tenant indicado; consulta sin filtro global para SuperAdmin con JWT sin empresa.</summary>
    public async Task<IReadOnlyList<User>> GetAllByTenantAsync(Guid subscriberId, CancellationToken ct = default)
        => await _platform.Unfiltered(_context.Users, PlatformQueryReason.TenantScopedExplicit)
            .Where(u => u.SubscriberId == subscriberId)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(ct);

    /// <summary>Métricas de plataforma: todos los usuarios legacy, todas las empresas.</summary>
    public async Task<int> CountAllSystemAsync(CancellationToken ct = default)
        => await _platform.Unfiltered(_context.Users, PlatformQueryReason.PlatformMetrics)
            .CountAsync(ct);

    /// <summary>Métricas de plataforma: usuarios legacy activos en cualquier tenant.</summary>
    public async Task<int> CountActiveSystemAsync(CancellationToken ct = default)
        => await _platform.Unfiltered(_context.Users, PlatformQueryReason.PlatformMetrics)
            .CountAsync(u => u.IsActive, ct);

    public async Task<bool> ExistsAsync(string email, Guid subscriberId, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _platform.Unfiltered(_context.Users, PlatformQueryReason.TenantScopedExplicit)
            .AnyAsync(u => u.SubscriberId == subscriberId && u.Email == normalized, ct);
    }

    /// <summary>Índice único global por email en <c>users</c>; consulta cross-tenant intencional.</summary>
    public async Task<bool> ExistsByEmailGloballyAsync(string email, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _platform.Unfiltered(_context.Users, PlatformQueryReason.CrossTenantSystem)
            .AnyAsync(u => u.Email == normalized, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> GetNonSuperAdminLegacyUsersByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return await _platform.Unfiltered(_context.Users, PlatformQueryReason.CrossTenantSystem)
            .Where(u => u.Email == normalized && u.Role != "SuperAdmin" && u.IsActive)
            .ToListAsync(ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _context.Users.AddAsync(user, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
