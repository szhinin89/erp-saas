using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Geography.Interfaces;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoCantons;

public sealed class GetGeoCantonsHandler
{
    private readonly IGeographyReadRepository _geo;

    public GetGeoCantonsHandler(IGeographyReadRepository geo)
    {
        _geo = geo;
    }

    public async Task<Result<IReadOnlyList<GeographyItemDto>>> HandleAsync(string provinceId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provinceId))
            return Result<IReadOnlyList<GeographyItemDto>>.Success(Array.Empty<GeographyItemDto>());

        var items = await _geo.GetCantonsByProvinceAsync(provinceId.Trim(), ct);
        return Result<IReadOnlyList<GeographyItemDto>>.Success(
            items.Select(x => new GeographyItemDto(x.Id, x.Name)).ToList());
    }
}
