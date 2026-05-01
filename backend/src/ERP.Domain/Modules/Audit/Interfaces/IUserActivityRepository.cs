using ERP.Domain.Audit.Entities;

namespace ERP.Domain.Audit.Interfaces;

public interface IUserActivityRepository
{
    Task AddAsync(UserActivity activity, CancellationToken ct = default);

    Task<IReadOnlyList<UserActivity>> GetMyRecentAsync(
        Guid tenantId,
        Guid userId,
        string? module = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default);
}

