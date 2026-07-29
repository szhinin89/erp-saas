using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class UserSessionRepository : IUserSessionRepository
{
    private readonly ErpDbContext _db;

    public UserSessionRepository(ErpDbContext db)
    {
        _db = db;
    }

    // IgnoreQueryFilters(): esta consulta puede abarcar varias empresas del mismo tenant
    // (un usuario puede tener una sesión Active por empresa) y se invoca durante el propio
    // flujo de login, antes de que exista un ICurrentCompany ambiente confiable — mismo
    // criterio ya usado por AccessRepository para CompanyUserMembership.
    public async Task<IReadOnlyList<UserSession>> GetActiveSessionsAsync(
        Guid identityUserId,
        Guid tenantId,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .UserSessions.IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == tenantId
                && x.IdentityUserId == identityUserId
                && x.Status == UserSessionStatus.Active
            )
            .ToListAsync(cancellationToken);

    // IgnoreQueryFilters(): limpieza de sistema cross-tenant (Hangfire, Fase 9) — mismo
    // criterio que GetActiveSessionsAsync arriba, archivo ya en la allowlist del gate.
    public async Task<IReadOnlyList<UserSession>> GetExpiredActiveSessionsAsync(
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .UserSessions.IgnoreQueryFilters()
            .Where(x => x.Status == UserSessionStatus.Active && x.StartedAt < olderThanUtc)
            .ToListAsync(cancellationToken);

    // IgnoreQueryFilters(): dashboard administrativo (Fase 10) — CompanyId es un filtro
    // opcional explícito, no el scope automático de ICompanyOperationalEntity, porque un
    // administrador de tenant debe poder ver sesiones de varias empresas a la vez.
    public async Task<(IReadOnlyList<UserSession> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        Guid? identityUserId,
        Guid? companyId,
        UserSessionStatus? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var query = _db.UserSessions.IgnoreQueryFilters().Where(x => x.TenantId == tenantId);

        if (identityUserId is Guid uid)
            query = query.Where(x => x.IdentityUserId == uid);
        if (companyId is Guid cid)
            query = query.Where(x => x.CompanyId == cid);
        if (status is UserSessionStatus st)
            query = query.Where(x => x.Status == st);
        if (fromUtc is DateTime from)
            query = query.Where(x => x.StartedAt >= from);
        if (toUtc is DateTime to)
            query = query.Where(x => x.StartedAt <= to);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.StartedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    // IgnoreQueryFilters(): mismo criterio que GetPagedAsync — estadísticas cross-company
    // dentro del tenant.
    public async Task<IReadOnlyDictionary<UserSessionStatus, int>> GetStatusCountsAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken cancellationToken = default
    )
    {
        var query = _db.UserSessions.IgnoreQueryFilters().Where(x => x.TenantId == tenantId);

        if (companyId is Guid cid)
            query = query.Where(x => x.CompanyId == cid);

        var counts = await query
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.Status, x => x.Count);
    }

    // Sin IgnoreQueryFilters — se apoya deliberadamente en el filtro automático de
    // ICompanyOperationalEntity (tenant + empresa actual). Ver nota en la interfaz.
    public Task<UserSession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default
    ) => _db.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

    // IgnoreQueryFilters(): logout es [AllowAnonymous], sin contexto de empresa confiable —
    // mismo criterio que GetActiveSessionsAsync. Ver nota en la interfaz.
    public Task<UserSession?> GetByRefreshTokenIdAsync(
        Guid refreshTokenId,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .UserSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.RefreshTokenId == refreshTokenId, cancellationToken);

    public Task AddAsync(UserSession session, CancellationToken cancellationToken = default) =>
        _db.UserSessions.AddAsync(session, cancellationToken).AsTask();

    // No-op intencional: reatachar/forzar EntityState sobre UserSession está fuera de la
    // lista blanca de ATT-GATE-01. La entidad debe llegar aquí ya tracked (obtenida vía
    // GetActiveSessionsAsync en este mismo DbContext y mutada con sus métodos de dominio);
    // el ChangeTracker persiste el cambio al llamar SaveChangesAsync sin necesidad de Update().
    public Task UpdateAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
