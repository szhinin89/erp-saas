using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Application.Modules.Branches.UseCases.GetGeoCantons;
using ERP.Application.Modules.Branches.UseCases.GetGeoCountries;
using ERP.Application.Modules.Branches.UseCases.GetGeoParishes;
using ERP.Application.Modules.Branches.UseCases.GetGeoProvinces;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Catálogo de ubicación (solo lectura) para combos en cascada.</summary>
[AppFeature("Geografía", $"perm:{SettingsPermissions.GeographyView}", "🌎", "/settings/geography", "perm:settings.group", 210)]
[ApiController]
[Route("api/v1/settings/geography")]
[Authorize(Policy = $"perm:{SettingsPermissions.GeographyView}")]
[Produces("application/json")]
public sealed class GeographyController : ControllerBase
{
    private readonly IMediator _mediator;

    public GeographyController(IMediator mediator) => _mediator = mediator;

    [HttpGet("countries")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Countries(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetGeoCountriesQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }

    [HttpGet("provinces")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Provinces([FromQuery] string countryId, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetGeoProvincesQuery(countryId), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }

    [HttpGet("cantons")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cantons([FromQuery] string provinceId, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetGeoCantonsQuery(provinceId), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }

    [HttpGet("parishes")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Parishes([FromQuery] string cantonId, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetGeoParishesQuery(cantonId), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }
}
