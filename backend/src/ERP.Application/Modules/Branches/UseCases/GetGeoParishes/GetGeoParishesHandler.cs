using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Geography.Interfaces;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoParishes;

public sealed class GetGeoParishesHandler
{
    private readonly IGeographyReadRepository _geo;

    public GetGeoParishesHandler(IGeographyReadRepository geo)
    {
        _geo = geo;
    }

    public async Task<Result<IReadOnlyList<GeographyItemDto>>> HandleAsync(string cantonId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cantonId))
            return Result<IReadOnlyList<GeographyItemDto>>.Success(Array.Empty<GeographyItemDto>());

        var items = await _geo.GetParishesByCantonAsync(cantonId.Trim(), ct);
        return Result<IReadOnlyList<GeographyItemDto>>.Success(
            items.Select(x => new GeographyItemDto(x.Id, x.Name)).ToList());
    }
}
