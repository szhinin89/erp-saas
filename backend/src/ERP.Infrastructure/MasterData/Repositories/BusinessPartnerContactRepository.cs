using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

public sealed class BusinessPartnerContactRepository : IBusinessPartnerContactRepository
{
    private readonly ErpDbContext _db;

    public BusinessPartnerContactRepository(ErpDbContext db) => _db = db;

    public Task<BusinessPartnerContact?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.BusinessPartnerContacts.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<IReadOnlyList<BusinessPartnerContact>> GetByBusinessPartnerAsync(
        Guid subscriberId, Guid companyId, Guid businessPartnerId,
        bool? onlyActive = true, CancellationToken ct = default)
    {
        var q = _db.BusinessPartnerContacts
            .Where(c => c.BusinessPartnerId == businessPartnerId);

        if (onlyActive.HasValue)
            q = q.Where(c => c.IsActive == onlyActive.Value);

        return q.OrderByDescending(c => c.IsPrimary)
                .ThenBy(c => c.FirstName)
                .ToListAsync(ct)
                .ContinueWith(t => (IReadOnlyList<BusinessPartnerContact>)t.Result, ct);
    }

    public Task<BusinessPartnerContact?> GetPrimaryAsync(
        Guid subscriberId, Guid companyId, Guid businessPartnerId, CancellationToken ct = default)
        => _db.BusinessPartnerContacts
            .FirstOrDefaultAsync(c =>
                c.BusinessPartnerId == businessPartnerId &&
                c.IsPrimary && c.IsActive, ct);

    public async Task ClearPrimaryAsync(
        Guid subscriberId, Guid companyId, Guid businessPartnerId, CancellationToken ct = default)
    {
        await _db.BusinessPartnerContacts
            .Where(c => c.BusinessPartnerId == businessPartnerId && c.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsPrimary, false), ct);
    }

    public Task AddAsync(BusinessPartnerContact contact, CancellationToken ct = default)
        => _db.BusinessPartnerContacts.AddAsync(contact, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
