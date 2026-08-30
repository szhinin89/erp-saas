using ERP.Domain.Modules.DocTypes.Entities;
using ERP.Domain.Modules.DocTypes.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.DocTypes;

public sealed class DocumentFlowPolicyRepository : IDocumentFlowPolicyRepository
{
    private readonly ErpDbContext _db;

    public DocumentFlowPolicyRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<(DocumentFlowPolicy Policy, string DocumentTypeName)>> ListAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    )
    {
        var rows = await _db
            .DocumentFlowPolicies.Where(p => p.TenantId == tenantId && p.CompanyId == companyId)
            .Join(_db.DocTypes, p => p.DocumentTypeCode, dt => dt.Code, (p, dt) => new { p, dt.Name })
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Select(x => (x.p, x.Name)).ToList();
    }

    public async Task<(DocumentFlowPolicy Policy, string DocumentTypeName)?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    )
    {
        var row = await _db
            .DocumentFlowPolicies.Where(p =>
                p.TenantId == tenantId && p.CompanyId == companyId && p.Id == id
            )
            .Join(_db.DocTypes, p => p.DocumentTypeCode, dt => dt.Code, (p, dt) => new { p, dt.Name })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : (row.p, row.Name);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default) => await _db.SaveChangesAsync(ct);
}
