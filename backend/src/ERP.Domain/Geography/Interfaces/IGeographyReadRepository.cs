using ERP.Domain.Geography.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Geography.Interfaces;

public interface IGeographyReadRepository
{
    Task<IReadOnlyList<SriCountry>> GetCountriesAsync(
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<GeoProvince>> GetProvincesByCountryAsync(
        string countryId,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<GeoCanton>> GetCantonsByProvinceAsync(
        string provinceId,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<GeoParish>> GetParishesByCantonAsync(
        string cantonId,
        CancellationToken cancellationToken = default
    );
}
