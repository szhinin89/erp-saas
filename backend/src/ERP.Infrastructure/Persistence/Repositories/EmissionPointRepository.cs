using ERP.Application.Common;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class EmissionPointRepository : IEmissionPointRepository
{
    private readonly ErpDbContext          _db;
    private readonly IPlatformQueryAccessor _platform;

    public EmissionPointRepository(ErpDbContext db, IPlatformQueryAccessor platform)
    {
        _db       = db;
        _platform = platform;
    }

    public Task<IReadOnlyList<EmissionPoint>> GetByEstablishmentAsync(Guid subscriberId, Guid establishmentId, CancellationToken ct = default)
        => _db.EmissionPoints
            .AsNoTracking()
            .Where(ep => ep.SubscriberId == subscriberId && ep.EstablishmentId == establishmentId)
            .OrderBy(ep => ep.Code)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<EmissionPoint>)t.Result, ct);

    public Task<EmissionPoint?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
        => _db.EmissionPoints
            .Include(ep => ep.Establishment)
            .FirstOrDefaultAsync(ep => ep.Id == id && ep.SubscriberId == subscriberId && ep.IsActive, ct);

    /// <inheritdoc/>
    public async Task<EmissionPoint?> GetDefaultForBranchAsync(Guid subscriberId, Guid branchId, CancellationToken ct = default)
    {
        var establishmentId = await _db.Establishments
            .Where(e => e.SubscriberId == subscriberId
                     && e.BranchId     == branchId
                     && e.IsMain
                     && e.IsActive)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(ct);

        if (establishmentId is null)
            return null;

        return await _db.EmissionPoints
            .Include(ep => ep.Establishment)
            .FirstOrDefaultAsync(ep => ep.EstablishmentId == establishmentId
                                     && ep.IsDefault
                                     && ep.IsActive, ct);
    }

    /// <inheritdoc/>
    public async Task<EmissionPoint?> GetDefaultForCompanyAsync(Guid subscriberId, Guid companyId, CancellationToken ct = default)
    {
        var establishmentId = await _db.Establishments
            .Where(e => e.SubscriberId == subscriberId
                     && e.CompanyId    == companyId
                     && e.IsMain
                     && e.IsActive)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(ct);

        if (establishmentId is null)
            return null;

        return await _db.EmissionPoints
            .Include(ep => ep.Establishment)
            .FirstOrDefaultAsync(ep => ep.EstablishmentId == establishmentId
                                     && ep.IsDefault
                                     && ep.IsActive, ct);
    }

    public async Task ClearDefaultExceptAsync(Guid subscriberId, Guid establishmentId, Guid? exceptId, Guid updatedBy, CancellationToken ct = default)
    {
        var others = await _db.EmissionPoints
            .Where(ep => ep.SubscriberId    == subscriberId
                      && ep.EstablishmentId == establishmentId
                      && ep.IsDefault
                      && (exceptId == null || ep.Id != exceptId))
            .ToListAsync(ct);

        foreach (var ep in others)
            ep.SetDefault(false, updatedBy);
    }

    public Task<bool> ExistsAsync(Guid subscriberId, Guid establishmentId, string code, CancellationToken ct = default)
        => _platform
            .Unfiltered(_db.EmissionPoints, PlatformQueryReason.Seeding)
            .AnyAsync(ep => ep.SubscriberId    == subscriberId
                         && ep.EstablishmentId == establishmentId
                         && ep.Code            == code, ct);

    public Task AddAsync(EmissionPoint emissionPoint, CancellationToken ct = default)
    {
        _db.EmissionPoints.Add(emissionPoint);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
