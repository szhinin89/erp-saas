using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Geography.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoProvinces;

public sealed class GetGeoProvincesHandler : IRequestHandler<GetGeoProvincesQuery, Result<IReadOnlyList<GeographyItemDto>>>
{
    private readonly IGeographyReadRepository _geo;

    public GetGeoProvincesHandler(IGeographyReadRepository geo) => _geo = geo;

    public async Task<Result<IReadOnlyList<GeographyItemDto>>> Handle(GetGeoProvincesQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.CountryId))
            return Result<IReadOnlyList<GeographyItemDto>>.Success(Array.Empty<GeographyItemDto>());

        var items = await _geo.GetProvincesByCountryAsync(query.CountryId.Trim(), cancellationToken);
        return Result<IReadOnlyList<GeographyItemDto>>.Success(
            items.Select(x => new GeographyItemDto(x.Id, x.Name)).ToList());
    }
}
