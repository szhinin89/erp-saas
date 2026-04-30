using Microsoft.EntityFrameworkCore;
using ERP.Domain.Geography.Entities;
using ERP.Domain.Geography.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class GeographyReadRepository : IGeographyReadRepository
{
    private readonly ErpDbContext _context;

    public GeographyReadRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<GeoCountry>> GetCountriesAsync(CancellationToken ct = default)
        => await _context.GeoCountries.OrderBy(x => x.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<GeoProvince>> GetProvincesByCountryAsync(string countryId, CancellationToken ct = default)
        => await _context.GeoProvinces.Where(x => x.CountryId == countryId).OrderBy(x => x.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<GeoCanton>> GetCantonsByProvinceAsync(string provinceId, CancellationToken ct = default)
        => await _context.GeoCantons.Where(x => x.ProvinceId == provinceId).OrderBy(x => x.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<GeoParish>> GetParishesByCantonAsync(string cantonId, CancellationToken ct = default)
        => await _context.GeoParishes.Where(x => x.CantonId == cantonId).OrderBy(x => x.Name).ToListAsync(ct);
}
