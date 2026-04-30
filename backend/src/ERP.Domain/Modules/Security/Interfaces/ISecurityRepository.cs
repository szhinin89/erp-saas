using ERP.Domain.Security.Entities;

namespace ERP.Domain.Security.Interfaces;

public interface ISecurityRepository
{
    Task<IReadOnlyList<SecurityAdminScopeAssignment>> GetAdminScopesAsync(Guid tenantId, CancellationToken ct = default);
    Task<SecurityAdminScopeAssignment?> GetAdminScopeAsync(
        Guid tenantId,
        string subjectType,
        string subjectKey,
        int scope,
        CancellationToken ct = default);

    Task AddAsync(SecurityAdminScopeAssignment entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

