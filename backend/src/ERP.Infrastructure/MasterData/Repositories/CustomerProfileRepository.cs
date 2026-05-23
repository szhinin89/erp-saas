using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

public sealed class CustomerProfileRepository : ICustomerProfileRepository
{
    private readonly ErpDbContext _db;

    public CustomerProfileRepository(ErpDbContext db) => _db = db;

    public Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.CustomerProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<CustomerProfile?> GetByBusinessPartnerIdAsync(Guid businessPartnerId, CancellationToken ct = default)
        => _db.CustomerProfiles.AsNoTracking()
              .FirstOrDefaultAsync(x => x.BusinessPartnerId == businessPartnerId, ct);

    public Task<bool> ExistsForBusinessPartnerAsync(Guid businessPartnerId, CancellationToken ct = default)
        => _db.CustomerProfiles.AnyAsync(x => x.BusinessPartnerId == businessPartnerId, ct);

    public async Task AddAsync(CustomerProfile profile, CancellationToken ct = default)
        => await _db.CustomerProfiles.AddAsync(profile, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
