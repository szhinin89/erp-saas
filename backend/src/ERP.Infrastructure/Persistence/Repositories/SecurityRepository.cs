using ERP.Domain.Security.Entities;
using ERP.Domain.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public class SecurityRepository : ISecurityRepository
{
    private readonly ErpDbContext _context;

    public SecurityRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SecurityAdminScopeAssignment>> GetAdminScopesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await _context.SecurityAdminScopeAssignments
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SubjectType)
            .ThenBy(x => x.SubjectKey)
            .ThenBy(x => x.Scope)
            .ToListAsync(cancellationToken);

    public async Task<SecurityAdminScopeAssignment?> GetAdminScopeAsync(
        Guid tenantId,
        string subjectType,
        string subjectKey,
        int scope,
        CancellationToken cancellationToken = default)
        => await _context.SecurityAdminScopeAssignments
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                     && x.SubjectType == subjectType
                     && x.SubjectKey == subjectKey
                     && x.Scope == scope,
                cancellationToken);

    public async Task AddAsync(SecurityAdminScopeAssignment entity, CancellationToken cancellationToken = default)
        => await _context.SecurityAdminScopeAssignments.AddAsync(entity, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}

