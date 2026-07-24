using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Services;

namespace ERP.Infrastructure.Persistence.Services;

public sealed class SriDocTypeCatalogResolver : ISriDocTypeCatalogResolver
{
    private readonly ErpDbContext _db;
    public SriDocTypeCatalogResolver(ErpDbContext db) => _db = db;

    public Task<bool> IsActiveElectronicDocTypeAsync(string code, CancellationToken ct = default)
        => _db.SriDocTypes.AsNoTracking()
            .AnyAsync(d => d.Code == code && d.IsActive && d.IsElectronic, ct);
}
