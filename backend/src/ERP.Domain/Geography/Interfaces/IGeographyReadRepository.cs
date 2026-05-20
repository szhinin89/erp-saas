using ERP.Domain.Geography.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Geography.Interfaces;

public interface IGeographyReadRepository
{
    Task<IReadOnlyList<SriCountry>> GetCountriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GeoProvince>> GetProvincesByCountryAsync(string countryId, CancellationToken ct = default);
    Task<IReadOnlyList<GeoCanton>> GetCantonsByProvinceAsync(string provinceId, CancellationToken ct = default);
    Task<IReadOnlyList<GeoParish>> GetParishesByCantonAsync(string cantonId, CancellationToken ct = default);
}
