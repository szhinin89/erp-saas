using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

public sealed class BusinessPartnerLocationRepository : IBusinessPartnerLocationRepository
{
    private readonly ErpDbContext _db;

    public BusinessPartnerLocationRepository(ErpDbContext db) => _db = db;

    public Task<BusinessPartnerLocation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.BusinessPartnerLocations.FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<IReadOnlyList<BusinessPartnerLocation>> GetByBusinessPartnerAsync(
        Guid subscriberId, Guid companyId, Guid businessPartnerId,
        bool? onlyActive = true, CancellationToken ct = default)
    {
        var q = _db.BusinessPartnerLocations
            .Where(l => l.BusinessPartnerId == businessPartnerId);

        if (onlyActive.HasValue)
            q = q.Where(l => l.IsActive == onlyActive.Value);

        return q.OrderByDescending(l => l.IsPrimary)
                .ThenBy(l => l.Name)
                .ToListAsync(ct)
                .ContinueWith(t => (IReadOnlyList<BusinessPartnerLocation>)t.Result, ct);
    }

    public Task<BusinessPartnerLocation?> GetPrimaryAsync(
        Guid subscriberId, Guid companyId, Guid businessPartnerId, CancellationToken ct = default)
        => _db.BusinessPartnerLocations
            .FirstOrDefaultAsync(l =>
                l.BusinessPartnerId == businessPartnerId &&
                l.IsPrimary && l.IsActive, ct);

    public async Task ClearPrimaryAsync(
        Guid subscriberId, Guid companyId, Guid businessPartnerId, CancellationToken ct = default)
    {
        await _db.BusinessPartnerLocations
            .Where(l => l.BusinessPartnerId == businessPartnerId && l.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.IsPrimary, false), ct);
    }

    public Task AddAsync(BusinessPartnerLocation location, CancellationToken ct = default)
        => _db.BusinessPartnerLocations.AddAsync(location, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
