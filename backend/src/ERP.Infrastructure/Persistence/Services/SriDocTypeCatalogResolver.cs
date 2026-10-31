using ERP.Application.Common.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Services;

public sealed class SriDocTypeCatalogResolver : ISriDocTypeCatalogResolver
{
    private readonly ErpDbContext _db;

    public SriDocTypeCatalogResolver(ErpDbContext db) => _db = db;

    public Task<bool> IsActiveElectronicDocTypeAsync(string code, CancellationToken ct = default) =>
        _db
            .SriDocTypes.AsNoTracking()
            .AnyAsync(d => d.Code == code && d.IsActive && d.IsElectronic, ct);

    public Task<bool> IsActiveDocTypeAsync(string code, CancellationToken ct = default) =>
        _db.SriDocTypes.AsNoTracking().AnyAsync(d => d.Code == code && d.IsActive, ct);

    public Task<bool> IsPhysicalDocTypePermittedAsync(
        string taxRegimeCode,
        string docTypeCode,
        CancellationToken ct = default
    ) =>
        _db
            .SriTaxRegimePhysicalDocTypes.AsNoTracking()
            .AnyAsync(
                p =>
                    p.TaxRegimeCode == taxRegimeCode
                    && p.DocTypeCode == docTypeCode
                    && p.IsActive
                    && _db.SriTaxRegimes.Any(r => r.Code == taxRegimeCode && r.IsActive),
                ct
            );
}
