using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Geography.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoParishes;

public sealed class GetGeoParishesHandler
    : IRequestHandler<GetGeoParishesQuery, Result<IReadOnlyList<GeographyItemDto>>>
{
    private readonly IGeographyReadRepository _geo;

    public GetGeoParishesHandler(IGeographyReadRepository geo) => _geo = geo;

    public async Task<Result<IReadOnlyList<GeographyItemDto>>> Handle(
        GetGeoParishesQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.CantonId))
            return Result<IReadOnlyList<GeographyItemDto>>.Success(Array.Empty<GeographyItemDto>());

        var items = await _geo.GetParishesByCantonAsync(query.CantonId.Trim(), cancellationToken);
        return Result<IReadOnlyList<GeographyItemDto>>.Success(
            items.Select(x => new GeographyItemDto(x.Id, x.Name)).ToList()
        );
    }
}
