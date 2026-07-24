using Microsoft.EntityFrameworkCore;
using ERP.Domain.Geography.Entities;
using ERP.Domain.Geography.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class GeographyReadRepository : IGeographyReadRepository
{
    private readonly ErpDbContext _context;

    public GeographyReadRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SriCountry>> GetCountriesAsync(CancellationToken cancellationToken = default)
        => await _context.SriCountries
            .Where(x => x.IsActive && x.Iso2 != null)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<GeoProvince>> GetProvincesByCountryAsync(string countryId, CancellationToken cancellationToken = default)
        => await _context.GeoProvinces.Where(x => x.CountryId == countryId).OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<GeoCanton>> GetCantonsByProvinceAsync(string provinceId, CancellationToken cancellationToken = default)
        => await _context.GeoCantons.Where(x => x.ProvinceId == provinceId).OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<GeoParish>> GetParishesByCantonAsync(string cantonId, CancellationToken cancellationToken = default)
        => await _context.GeoParishes.Where(x => x.CantonId == cantonId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
}
