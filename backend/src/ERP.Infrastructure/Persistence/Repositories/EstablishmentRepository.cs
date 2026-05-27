using ERP.Application.Common;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class EstablishmentRepository : IEstablishmentRepository
{
    private readonly ErpDbContext          _db;
    private readonly IPlatformQueryAccessor _platform;

    public EstablishmentRepository(ErpDbContext db, IPlatformQueryAccessor platform)
    {
        _db       = db;
        _platform = platform;
    }

    public Task<Establishment?> GetMainByBranchAsync(Guid subscriberId, Guid branchId, CancellationToken ct = default)
        => _db.Establishments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.SubscriberId == subscriberId
                                   && e.BranchId     == branchId
                                   && e.IsMain
                                   && e.IsActive, ct);

    public Task<Establishment?> GetMainByCompanyAsync(Guid subscriberId, Guid companyId, CancellationToken ct = default)
        => _db.Establishments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.SubscriberId == subscriberId
                                   && e.CompanyId    == companyId
                                   && e.IsMain
                                   && e.IsActive, ct);

    public Task<bool> ExistsAsync(Guid subscriberId, Guid branchId, string code, CancellationToken ct = default)
        => _platform
            .Unfiltered(_db.Establishments, PlatformQueryReason.Seeding)
            .AnyAsync(e => e.SubscriberId == subscriberId
                        && e.BranchId     == branchId
                        && e.Code         == code, ct);

    public Task AddAsync(Establishment establishment, CancellationToken ct = default)
    {
        _db.Establishments.Add(establishment);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
