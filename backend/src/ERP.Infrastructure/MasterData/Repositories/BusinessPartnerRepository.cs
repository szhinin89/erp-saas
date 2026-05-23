using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

public sealed class BusinessPartnerRepository : IBusinessPartnerRepository
{
    private readonly ErpDbContext _db;

    public BusinessPartnerRepository(ErpDbContext db) => _db = db;

    public Task<BusinessPartner?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.BusinessPartners
              .AsNoTracking()
              .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<BusinessPartner?> GetByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        CancellationToken ct = default)
        => _db.BusinessPartners
              .AsNoTracking()
              .FirstOrDefaultAsync(x =>
                  x.Identification.Type   == identificationType &&
                  x.Identification.Number == identificationNumber, ct);

    public async Task<IReadOnlyList<BusinessPartner>> SearchAsync(
        string?  query      = null,
        bool?    isActive   = true,
        bool?    isCustomer = null,
        bool?    isSupplier = null,
        int      skip       = 0,
        int      take       = 50,
        CancellationToken ct = default)
    {
        var q = _db.BusinessPartners.AsNoTracking();

        if (isActive.HasValue)
            q = q.Where(x => x.IsActive == isActive.Value);

        if (isCustomer == true)
            q = q.Where(x => x.CustomerProfile != null);
        else if (isCustomer == false)
            q = q.Where(x => x.CustomerProfile == null);

        if (isSupplier == true)
            q = q.Where(x => x.SupplierProfile != null);
        else if (isSupplier == false)
            q = q.Where(x => x.SupplierProfile == null);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var lower = query.Trim().ToLower();
            q = q.Where(x =>
                x.LegalName.ToLower().Contains(lower) ||
                (x.TradeName != null && x.TradeName.ToLower().Contains(lower)) ||
                x.Identification.Number.Contains(lower));
        }

        return await q
            .OrderBy(x => x.LegalName)
            .Skip(skip)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);
    }

    public Task<bool> ExistsByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        Guid?  excludeId = null,
        CancellationToken ct = default)
        => _db.BusinessPartners
              .AnyAsync(x =>
                  x.Identification.Type   == identificationType &&
                  x.Identification.Number == identificationNumber &&
                  (excludeId == null || x.Id != excludeId.Value), ct);

    public async Task AddAsync(BusinessPartner businessPartner, CancellationToken ct = default)
        => await _db.BusinessPartners.AddAsync(businessPartner, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
