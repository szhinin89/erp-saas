using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Application.Modules.Branches.UseCases.GetGeoCantons;
using ERP.Application.Modules.Branches.UseCases.GetGeoCountries;
using ERP.Application.Modules.Branches.UseCases.GetGeoParishes;
using ERP.Application.Modules.Branches.UseCases.GetGeoProvinces;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>Catálogo de ubicación (solo lectura) para combos en cascada.</summary>
[Modulo("Geografía", "session:geography", "🌎", "/configuracion/geografia", null, 210)]
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
public sealed class GeographyController : ControllerBase
{
    private readonly IMediator _mediator;

    public GeographyController(IMediator mediator) => _mediator = mediator;

    [HttpGet("countries")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Countries(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetGeoCountriesQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }

    [HttpGet("provinces")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Provinces([FromQuery] string countryId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetGeoProvincesQuery(countryId), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }

    [HttpGet("cantons")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cantons([FromQuery] string provinceId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetGeoCantonsQuery(provinceId), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }

    [HttpGet("parishes")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Parishes([FromQuery] string cantonId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetGeoParishesQuery(cantonId), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }
}
