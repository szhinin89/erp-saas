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

    public async Task<IReadOnlyList<SecurityAdminScopeAssignment>> GetAdminScopesAsync(Guid subscriberId, CancellationToken ct = default)
        => await _context.SecurityAdminScopeAssignments
            .Where(x => x.SubscriberId == subscriberId)
            .OrderBy(x => x.SubjectType)
            .ThenBy(x => x.SubjectKey)
            .ThenBy(x => x.Scope)
            .ToListAsync(ct);

    public async Task<SecurityAdminScopeAssignment?> GetAdminScopeAsync(
        Guid subscriberId,
        string subjectType,
        string subjectKey,
        int scope,
        CancellationToken ct = default)
        => await _context.SecurityAdminScopeAssignments
            .FirstOrDefaultAsync(
                x => x.SubscriberId == subscriberId
                     && x.SubjectType == subjectType
                     && x.SubjectKey == subjectKey
                     && x.Scope == scope,
                ct);

    public async Task AddAsync(SecurityAdminScopeAssignment entity, CancellationToken ct = default)
        => await _context.SecurityAdminScopeAssignments.AddAsync(entity, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}

