using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Geography.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoCountries;

public sealed class GetGeoCountriesHandler
    : IRequestHandler<GetGeoCountriesQuery, Result<IReadOnlyList<GeographyItemDto>>>
{
    private readonly IGeographyReadRepository _geo;

    public GetGeoCountriesHandler(IGeographyReadRepository geo) => _geo = geo;

    public async Task<Result<IReadOnlyList<GeographyItemDto>>> Handle(
        GetGeoCountriesQuery query,
        CancellationToken cancellationToken
    )
    {
        var items = await _geo.GetCountriesAsync(cancellationToken);
        return Result<IReadOnlyList<GeographyItemDto>>.Success(
            items.Select(x => new GeographyItemDto(x.Iso2!, x.Name)).ToList()
        );
    }
}
