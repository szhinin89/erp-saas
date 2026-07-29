using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Geography.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoCantons;

public sealed class GetGeoCantonsHandler : IRequestHandler<GetGeoCantonsQuery, Result<IReadOnlyList<GeographyItemDto>>>
{
    private readonly IGeographyReadRepository _geo;

    public GetGeoCantonsHandler(IGeographyReadRepository geo) => _geo = geo;

    public async Task<Result<IReadOnlyList<GeographyItemDto>>> Handle(GetGeoCantonsQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.ProvinceId))
            return Result<IReadOnlyList<GeographyItemDto>>.Success(Array.Empty<GeographyItemDto>());

        var items = await _geo.GetCantonsByProvinceAsync(query.ProvinceId.Trim(), cancellationToken);
        return Result<IReadOnlyList<GeographyItemDto>>.Success(
            items.Select(x => new GeographyItemDto(x.Id, x.Name)).ToList());
    }
}
