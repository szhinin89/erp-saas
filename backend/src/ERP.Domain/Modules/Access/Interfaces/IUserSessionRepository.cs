using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;

namespace ERP.Domain.Access.Interfaces;

public interface IUserSessionRepository
{
    /// <summary>
    /// Sesiones en estado Active de un usuario dentro de un tenant. Puede devolver más de una
    /// fila — un mismo usuario puede tener a lo sumo una sesión activa POR EMPRESA, pero varias
    /// empresas del mismo tenant en paralelo son válidas (regla de negocio: una activa por empresa,
    /// no una activa por tenant).
    /// </summary>
    Task<IReadOnlyList<UserSession>> GetActiveSessionsAsync(
        Guid identityUserId,
        Guid tenantId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Sesiones Active con StartedAt anterior a <paramref name="olderThanUtc"/>, cruzando todas
    /// las empresas/tenants (limpieza de sistema vía Hangfire, Fase 9) — igual criterio
    /// cross-tenant ya usado por AccessRepository/UserSessionRepository (archivo ya en la
    /// allowlist de IgnoreQueryFilters, sin necesidad de una nueva entrada).
    /// </summary>
    Task<IReadOnlyList<UserSession>> GetExpiredActiveSessionsAsync(
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Listado paginado para el dashboard administrativo (Fase 10) — cruza todas las empresas
    /// del tenant (CompanyId es un filtro opcional, no un scope automático), mismo criterio
    /// cross-company ya usado por GetActiveSessionsAsync/GetExpiredActiveSessionsAsync.
    /// </summary>
    Task<(IReadOnlyList<UserSession> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        Guid? identityUserId,
        Guid? companyId,
        UserSessionStatus? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    /// <summary>Conteo de sesiones por Status dentro del tenant (Fase 10), CompanyId opcional.</summary>
    Task<IReadOnlyDictionary<UserSessionStatus, int>> GetStatusCountsAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Búsqueda por Id SIN IgnoreQueryFilters — se apoya en el filtro automático de
    /// ICompanyOperationalEntity (tenant + empresa actual), a propósito: el cierre
    /// administrativo (Fase 10) nunca debe poder alcanzar una sesión fuera de la empresa activa
    /// del actor, sin necesidad de validación manual adicional.
    /// </summary>
    Task<UserSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fase E — localiza la UserSession creada en el login que quedó vinculada a este RefreshToken
    /// (UserSession.RefreshTokenId), para que el logout pueda cerrarla. IgnoreQueryFilters(): el
    /// logout es [AllowAnonymous] (el access token típicamente ya expiró), sin
    /// ICompanyOperationalEntity ambiente confiable — mismo criterio que GetActiveSessionsAsync.
    /// </summary>
    Task<UserSession?> GetByRefreshTokenIdAsync(
        Guid refreshTokenId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// No reatacha ni fuerza EntityState — la entidad debe haberse obtenido previamente vía
    /// GetActiveSessionsAsync (misma instancia de ErpDbContext) y mutado con sus métodos de
    /// dominio. El ChangeTracker ya la tiene rastreada; este método existe por simetría de
    /// contrato para los handlers que lo invoquen explícitamente antes de SaveChangesAsync.
    /// </summary>
    Task UpdateAsync(UserSession session, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
