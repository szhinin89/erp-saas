using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

public sealed class SupplierProfileRepository : ISupplierProfileRepository
{
    private readonly ErpDbContext _db;

    public SupplierProfileRepository(ErpDbContext db) => _db = db;

    public Task<SupplierProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.SupplierProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<SupplierProfile?> GetByBusinessPartnerIdAsync(Guid businessPartnerId, CancellationToken ct = default)
        => _db.SupplierProfiles.AsNoTracking()
              .FirstOrDefaultAsync(x => x.BusinessPartnerId == businessPartnerId, ct);

    public Task<bool> ExistsForBusinessPartnerAsync(Guid businessPartnerId, CancellationToken ct = default)
        => _db.SupplierProfiles.AnyAsync(x => x.BusinessPartnerId == businessPartnerId, ct);

    public async Task AddAsync(SupplierProfile profile, CancellationToken ct = default)
        => await _db.SupplierProfiles.AddAsync(profile, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
