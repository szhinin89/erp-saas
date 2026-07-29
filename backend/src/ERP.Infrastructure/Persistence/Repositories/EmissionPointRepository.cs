using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class EmissionPointRepository : IEmissionPointRepository
{
    private readonly ErpDbContext _db;

    public EmissionPointRepository(ErpDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<EmissionPoint>> GetByEstablishmentAsync(
        Guid tenantId,
        Guid establishmentId,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .EmissionPoints.AsNoTracking()
            .Where(ep => ep.TenantId == tenantId && ep.EstablishmentId == establishmentId)
            .OrderBy(ep => ep.Code)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<EmissionPoint>)t.Result, cancellationToken);

    public async Task<IReadOnlyList<EmissionPoint>> GetAllByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        bool? activeFilter,
        string? search,
        CancellationToken cancellationToken = default
    )
    {
        var q = _db
            .EmissionPoints.AsNoTracking()
            .Include(ep => ep.Establishment)
                .ThenInclude(e => e.Branch)
            .Where(ep => ep.TenantId == tenantId && ep.CompanyId == companyId);

        if (activeFilter.HasValue)
            q = q.Where(ep => ep.IsActive == activeFilter.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(ep =>
                EF.Functions.ILike(ep.Code, $"%{term}%")
                || (ep.Name != null && EF.Functions.ILike(ep.Name, $"%{term}%"))
                || EF.Functions.ILike(ep.Establishment.Name, $"%{term}%")
                || EF.Functions.ILike(ep.Establishment.Code, $"%{term}%")
            );
        }

        var list = await q.OrderBy(ep => ep.Establishment.Code)
            .ThenBy(ep => ep.Code)
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task<EmissionPoint?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .EmissionPoints.Include(ep => ep.Establishment)
            .FirstOrDefaultAsync(
                ep => ep.Id == id && ep.TenantId == tenantId && ep.IsActive,
                cancellationToken
            );

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EmissionPoint>> GetActiveByBranchAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .EmissionPoints.AsNoTracking()
            .Include(ep => ep.Establishment)
            .Where(ep =>
                ep.TenantId == tenantId && ep.Establishment.BranchId == branchId && ep.IsActive
            )
            .OrderBy(ep => ep.Establishment.Code)
            .ThenBy(ep => ep.Code)
            .ToListAsync(cancellationToken);

    public async Task<EmissionPoint?> GetDefaultForBranchAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken = default
    )
    {
        var establishmentId = await _db
            .Establishments.Where(e =>
                e.TenantId == tenantId && e.BranchId == branchId && e.IsMain && e.IsActive
            )
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (establishmentId is null)
            return null;

        return await _db
            .EmissionPoints.Include(ep => ep.Establishment)
            .FirstOrDefaultAsync(
                ep => ep.EstablishmentId == establishmentId && ep.IsDefault && ep.IsActive,
                cancellationToken
            );
    }

    /// <inheritdoc/>
    public async Task<EmissionPoint?> GetDefaultForCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    )
    {
        var establishmentId = await _db
            .Establishments.Where(e =>
                e.TenantId == tenantId && e.CompanyId == companyId && e.IsMain && e.IsActive
            )
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (establishmentId is null)
            return null;

        return await _db
            .EmissionPoints.Include(ep => ep.Establishment)
            .FirstOrDefaultAsync(
                ep => ep.EstablishmentId == establishmentId && ep.IsDefault && ep.IsActive,
                cancellationToken
            );
    }

    public async Task ClearDefaultExceptAsync(
        Guid tenantId,
        Guid establishmentId,
        Guid? exceptId,
        Guid updatedBy,
        CancellationToken cancellationToken = default
    )
    {
        var others = await _db
            .EmissionPoints.Where(ep =>
                ep.TenantId == tenantId
                && ep.EstablishmentId == establishmentId
                && ep.IsDefault
                && (exceptId == null || ep.Id != exceptId)
            )
            .ToListAsync(cancellationToken);

        foreach (var ep in others)
            ep.SetDefault(false, updatedBy);
    }

    public Task<bool> ExistsAsync(
        Guid tenantId,
        Guid establishmentId,
        string code,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .EmissionPoints.IgnoreQueryFilters()
            .AnyAsync(
                ep =>
                    ep.TenantId == tenantId
                    && ep.EstablishmentId == establishmentId
                    && ep.Code == code,
                cancellationToken
            );

    public Task AddAsync(EmissionPoint emissionPoint, CancellationToken cancellationToken = default)
    {
        _db.EmissionPoints.Add(emissionPoint);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
