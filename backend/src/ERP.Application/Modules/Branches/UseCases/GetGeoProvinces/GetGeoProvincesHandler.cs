using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Geography.Interfaces;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoProvinces;

public sealed class GetGeoProvincesHandler
{
    private readonly IGeographyReadRepository _geo;

    public GetGeoProvincesHandler(IGeographyReadRepository geo)
    {
        _geo = geo;
    }

    public async Task<Result<IReadOnlyList<GeographyItemDto>>> HandleAsync(string countryId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(countryId))
            return Result<IReadOnlyList<GeographyItemDto>>.Success(Array.Empty<GeographyItemDto>());

        var items = await _geo.GetProvincesByCountryAsync(countryId.Trim(), ct);
        return Result<IReadOnlyList<GeographyItemDto>>.Success(
            items.Select(x => new GeographyItemDto(x.Id, x.Name)).ToList());
    }
}
