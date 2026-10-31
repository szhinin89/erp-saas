using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class PhysicalDocumentAuthorizationRepository
    : IPhysicalDocumentAuthorizationRepository
{
    private readonly ErpDbContext _db;

    public PhysicalDocumentAuthorizationRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<PhysicalDocumentAuthorization>> GetAllByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .PhysicalDocumentAuthorizations.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .OrderByDescending(a => a.IsActive)
            .ThenByDescending(a => a.AuthorizedOn)
            .ThenBy(a => a.FirstSequential)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PhysicalDocumentAuthorization>> GetActiveAsync(
        Guid tenantId,
        Guid companyId,
        Guid emissionPointId,
        string docTypeCode,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .PhysicalDocumentAuthorizations.AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId
                && a.CompanyId == companyId
                && a.EmissionPointId == emissionPointId
                && a.DocTypeCode == docTypeCode
                && a.IsActive
            )
            .OrderBy(a => a.FirstSequential)
            .ToListAsync(cancellationToken);

    public Task<PhysicalDocumentAuthorization?> GetByIdForCompanyAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        _db.PhysicalDocumentAuthorizations.FirstOrDefaultAsync(
            a => a.Id == id && a.TenantId == tenantId && a.CompanyId == companyId,
            cancellationToken
        );

    public async Task AddAsync(
        PhysicalDocumentAuthorization authorization,
        CancellationToken cancellationToken = default
    ) => await _db.PhysicalDocumentAuthorizations.AddAsync(authorization, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
