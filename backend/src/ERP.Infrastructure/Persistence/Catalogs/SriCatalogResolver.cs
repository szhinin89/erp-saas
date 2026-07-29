using ERP.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Catalogs;

public sealed class SriCatalogResolver : ISriCatalogResolver
{
    private readonly ErpDbContext _db;

    public SriCatalogResolver(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<string, SriUomInfo>> ResolveUomsAsync(
        IEnumerable<string> codes,
        CancellationToken ct = default
    )
    {
        var list = codes.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        if (list.Count == 0)
            return new Dictionary<string, SriUomInfo>();

        return await _db
            .SriUoms.Where(u => list.Contains(u.Code))
            .ToDictionaryAsync(u => u.Code, u => new SriUomInfo(u.Abbrev ?? u.Name, u.Name), ct);
    }

    public async Task<IReadOnlyDictionary<string, SriVatInfo>> ResolveVatRatesAsync(
        IEnumerable<string> codes,
        CancellationToken ct = default
    )
    {
        var list = codes.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        if (list.Count == 0)
            return new Dictionary<string, SriVatInfo>();

        return await _db
            .SriVatRates.Where(v => list.Contains(v.Code) && v.IsActive)
            .ToDictionaryAsync(v => v.Code, v => new SriVatInfo(v.Name, v.Percentage), ct);
    }

    public async Task<IReadOnlyDictionary<string, SriIceInfo>> ResolveIceRatesAsync(
        IEnumerable<string> codes,
        CancellationToken ct = default
    )
    {
        var list = codes.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        if (list.Count == 0)
            return new Dictionary<string, SriIceInfo>();

        return await _db
            .SriIceRates.Where(r => list.Contains(r.Code) && r.IsActive)
            .ToDictionaryAsync(r => r.Code, r => new SriIceInfo(r.Name, r.Percentage), ct);
    }
}
