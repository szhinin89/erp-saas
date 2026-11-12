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
[AppFeature(
    "Ubicaciones geográficas",
    $"perm:{SettingsPermissions.GeographyView}",
    "🌎",
    "/settings/geography",
    "perm:settings.group",
    210
)]
[ApiController]
[Route("api/v1/settings/geography")]
[Authorize(Policy = $"perm:{SettingsPermissions.GeographyView}")]
[Produces("application/json")]
public sealed class GeographyController : ControllerBase
{
    private readonly IMediator _mediator;

    public GeographyController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista los países del catálogo geográfico (solo lectura).
    /// </summary>
    /// <remarks>
    /// Es el primer nivel de la cascada país → provincia → cantón → parroquia que usan los formularios
    /// de sucursales. Los ids son códigos de texto y se envían tal cual en los campos `countryId`,
    /// `provinceId`, `cantonId` y `parishId`.
    /// </remarks>
    /// <response code="200">Países del catálogo.</response>
    [HttpGet("countries")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> Countries(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetGeoCountriesQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }

    /// <summary>
    /// Lista las provincias de un país del catálogo geográfico (solo lectura).
    /// </summary>
    /// <remarks>
    /// `countryId` es obligatorio: si falta o llega vacío se responde 422. Un valor en blanco o un id
    /// sin provincias devuelve una lista vacía (no es error), y el id se recorta de espacios antes de
    /// consultar.
    /// </remarks>
    /// <param name="countryId">Id (código) del país cuyas provincias se listan.</param>
    /// <response code="200">Provincias del país indicado (vacío si no hay).</response>
    /// <response code="422">Falta el parámetro `countryId`.</response>
    [HttpGet("provinces")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Provinces(
        [FromQuery] string countryId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(new GetGeoProvincesQuery(countryId), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }

    /// <summary>
    /// Lista las cantones de un provincia del catálogo geográfico (solo lectura).
    /// </summary>
    /// <remarks>
    /// `provinceId` es obligatorio: si falta o llega vacío se responde 422. Un valor en blanco o un id
    /// sin cantones devuelve una lista vacía (no es error), y el id se recorta de espacios antes de
    /// consultar.
    /// </remarks>
    /// <param name="provinceId">Id (código) del provincia cuyas cantones se listan.</param>
    /// <response code="200">Cantones del provincia indicado (vacío si no hay).</response>
    /// <response code="422">Falta el parámetro `provinceId`.</response>
    [HttpGet("cantons")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cantons(
        [FromQuery] string provinceId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(new GetGeoCantonsQuery(provinceId), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }

    /// <summary>
    /// Lista las parroquias de un cantón del catálogo geográfico (solo lectura).
    /// </summary>
    /// <remarks>
    /// `cantonId` es obligatorio: si falta o llega vacío se responde 422. Un valor en blanco o un id
    /// sin parroquias devuelve una lista vacía (no es error), y el id se recorta de espacios antes de
    /// consultar.
    /// </remarks>
    /// <param name="cantonId">Id (código) del cantón cuyas parroquias se listan.</param>
    /// <response code="200">Parroquias del cantón indicado (vacío si no hay).</response>
    /// <response code="422">Falta el parámetro `cantonId`.</response>
    [HttpGet("parishes")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Parishes(
        [FromQuery] string cantonId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(new GetGeoParishesQuery(cantonId), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }
}
