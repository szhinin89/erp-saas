using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateTariff;
using ERP.Application.Products.Catalogs.UseCases.GetTariffs;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo maestro: Tarifas / aranceles del tenant autenticado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TariffsController : ControllerBase
{
    private readonly CreateTariffHandler _create;
    private readonly GetTariffsHandler _get;

    public TariffsController(CreateTariffHandler create, GetTariffsHandler get)
    {
        _create = create;
        _get = get;
    }

    /// <summary>Lista tarifas (aranceles) del tenant.</summary>
    /// <param name="onlyActive">Si es true, retorna únicamente tarifas habilitadas.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Lista de tarifas (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [Authorize(Policy = "perm:catalog.tariffs.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TariffDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _get.HandleAsync(onlyActive, ct);
        return Ok(new ApiResponse<IReadOnlyList<TariffDto>>(
            Success: result.IsSuccess,
            Message: result.IsSuccess ? "OK" : result.Error ?? "Error",
            ResponseObject: result.Value ?? Array.Empty<TariffDto>()));
    }

    /// <summary>Crea una nueva tarifa (arancel).</summary>
    /// <remarks>El nombre/código debe ser único por tenant.</remarks>
    /// <response code="201">Tarifa creada correctamente.</response>
    /// <response code="400">Error de validación (por ejemplo, duplicado).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpPost]
    [Authorize(Policy = "perm:catalog.tariffs.create")]
    [ProducesResponseType(typeof(ApiResponse<TariffDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateTariffCommand command, CancellationToken ct = default)
    {
        var result = await _create.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<TariffDto?>(
                Success: true,
                Message: "Creado",
                ResponseObject: result.Value))
            : BadRequest(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "Error",
                ResponseObject: new { }));
    }
}

