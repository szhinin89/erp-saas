using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Application.Modules.Branches.UseCases.GetGeoCantons;
using ERP.Application.Modules.Branches.UseCases.GetGeoCountries;
using ERP.Application.Modules.Branches.UseCases.GetGeoParishes;
using ERP.Application.Modules.Branches.UseCases.GetGeoProvinces;

namespace ERP.API.Controllers;

/// <summary>Catálogo de ubicación (solo lectura) para combos en cascada.</summary>
/// <remarks>
/// Autorización: política por defecto <c>Session</c> (ver <c>Program.cs</c>). Sin <c>perm:*</c>:
/// catálogo global de bajo riesgo; cualquier usuario con sesión ERP puede consultarlo.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
public sealed class GeographyController : ControllerBase
{
    private readonly GetGeoCountriesHandler _countries;
    private readonly GetGeoProvincesHandler _provinces;
    private readonly GetGeoCantonsHandler _cantons;
    private readonly GetGeoParishesHandler _parishes;

    public GeographyController(
        GetGeoCountriesHandler countries,
        GetGeoProvincesHandler provinces,
        GetGeoCantonsHandler cantons,
        GetGeoParishesHandler parishes)
    {
        _countries = countries;
        _provinces = provinces;
        _cantons = cantons;
        _parishes = parishes;
    }

    [HttpGet("countries")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Countries(CancellationToken ct = default)
    {
        var result = await _countries.HandleAsync(ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }

    [HttpGet("provinces")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Provinces([FromQuery] string countryId, CancellationToken ct = default)
    {
        var result = await _provinces.HandleAsync(countryId, ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }

    [HttpGet("cantons")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cantons([FromQuery] string provinceId, CancellationToken ct = default)
    {
        var result = await _cantons.HandleAsync(provinceId, ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }

    [HttpGet("parishes")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GeographyItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Parishes([FromQuery] string cantonId, CancellationToken ct = default)
    {
        var result = await _parishes.HandleAsync(cantonId, ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<GeographyItemDto>());
    }
}
