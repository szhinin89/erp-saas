using ERP.Domain.Geography.Entities;

namespace ERP.Domain.Geography.Interfaces;

public interface IGeographyReadRepository
{
    Task<IReadOnlyList<GeoCountry>> GetCountriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GeoProvince>> GetProvincesByCountryAsync(string countryId, CancellationToken ct = default);
    Task<IReadOnlyList<GeoCanton>> GetCantonsByProvinceAsync(string provinceId, CancellationToken ct = default);
    Task<IReadOnlyList<GeoParish>> GetParishesByCantonAsync(string cantonId, CancellationToken ct = default);
}
