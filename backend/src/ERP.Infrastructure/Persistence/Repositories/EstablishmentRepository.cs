using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class EstablishmentRepository : IEstablishmentRepository
{
    private readonly ErpDbContext _db;

    public EstablishmentRepository(ErpDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<Establishment>> GetByBranchAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .Establishments.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.BranchId == branchId)
            .OrderBy(e => e.Code)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Establishment>)t.Result, cancellationToken);

    public Task<IReadOnlyList<Establishment>> GetActiveByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .Establishments.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.CompanyId == companyId && e.IsActive)
            .OrderBy(e => e.Code)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Establishment>)t.Result, cancellationToken);

    public async Task<IReadOnlyList<Establishment>> GetFilteredAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        bool? isActive,
        string? search,
        CancellationToken cancellationToken = default
    )
    {
        var q = _db
            .Establishments.AsNoTracking()
            .Include(e => e.Branch)
            .Include(e => e.EmissionPoints)
            .Where(e => e.TenantId == tenantId && e.CompanyId == companyId);

        if (branchId.HasValue)
            q = q.Where(e => e.BranchId == branchId.Value);

        if (isActive.HasValue)
            q = q.Where(e => e.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(e =>
                EF.Functions.ILike(e.Name, $"%{term}%")
                || EF.Functions.ILike(e.Code, $"%{term}%")
                || EF.Functions.ILike(e.Address, $"%{term}%")
            );
        }

        return await q.OrderBy(e => e.Code).ToListAsync(cancellationToken);
    }

    public Task<Establishment?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .Establishments.Include(e => e.Branch)
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, cancellationToken);

    public Task<Establishment?> GetMainByBranchAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .Establishments.AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.BranchId == branchId && e.IsMain && e.IsActive,
                cancellationToken
            );

    public Task<Establishment?> GetMainByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .Establishments.AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.CompanyId == companyId && e.IsMain && e.IsActive,
                cancellationToken
            );

    public Task<bool> ExistsAsync(
        Guid tenantId,
        Guid companyId,
        string code,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .Establishments.IgnoreQueryFilters()
            .AnyAsync(
                e => e.TenantId == tenantId && e.CompanyId == companyId && e.Code == code,
                cancellationToken
            );

    public Task<bool> HasActiveEmissionPointsAsync(
        Guid tenantId,
        Guid establishmentId,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .EmissionPoints.AsNoTracking()
            .AnyAsync(
                ep =>
                    ep.TenantId == tenantId && ep.EstablishmentId == establishmentId && ep.IsActive,
                cancellationToken
            );

    public Task AddAsync(Establishment establishment, CancellationToken cancellationToken = default)
    {
        _db.Establishments.Add(establishment);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ClearMainExceptAsync(
        Guid tenantId,
        Guid companyId,
        Guid? exceptEstablishmentId,
        Guid updatedBy,
        CancellationToken cancellationToken = default
    )
    {
        var q = _db.Establishments.Where(e =>
            e.TenantId == tenantId && e.CompanyId == companyId && e.IsMain
        );
        if (exceptEstablishmentId is not null)
            q = q.Where(e => e.Id != exceptEstablishmentId.Value);

        var others = await q.ToListAsync(cancellationToken);
        foreach (var e in others)
            e.SetMain(false, updatedBy);

        return others.Select(e => e.Id).ToList();
    }
}
