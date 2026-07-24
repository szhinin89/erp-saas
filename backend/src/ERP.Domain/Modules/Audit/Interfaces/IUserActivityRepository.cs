using ERP.Domain.Audit.Entities;

namespace ERP.Domain.Audit.Interfaces;

public interface IUserActivityRepository
{
    Task AddAsync(UserActivity activity, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserActivity>> GetMyRecentAsync(
        Guid tenantId,
        Guid userId,
        string? moduleName = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Historial de auditoría de una entidad concreta (todos los usuarios del tenant), más reciente primero.
    /// </summary>
    Task<IReadOnlyList<UserActivity>> GetByEntityAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        int take = 10,
        CancellationToken cancellationToken = default);
}

