using ERP.Domain.Security.Entities;

namespace ERP.Domain.Security.Interfaces;

public interface ISecurityRepository
{
    Task<IReadOnlyList<SecurityAdminScopeAssignment>> GetAdminScopesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<SecurityAdminScopeAssignment?> GetAdminScopeAsync(
        Guid tenantId,
        string subjectType,
        string subjectKey,
        int scope,
        CancellationToken cancellationToken = default);

    Task AddAsync(SecurityAdminScopeAssignment entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

