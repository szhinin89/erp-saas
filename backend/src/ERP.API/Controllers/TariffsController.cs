using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.DTOs;
using ERP.Application.Products.UseCases.CreateTariff;
using ERP.Application.Products.UseCases.GetTariffs;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo maestro: Tarifas / aranceles del tenant autenticado.
/// </summary>
[AppFeature("Tarifas", "perm:inventario.tariffs.view", "💲", "/inventario/tariffs", "perm:inventario.products.view", 37)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TariffsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TariffsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lista tarifas (aranceles) del tenant.</summary>
    /// <param name="onlyActive">Si es true, retorna únicamente tarifas habilitadas.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Lista de tarifas (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.tariffs.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TariffDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTariffsQuery(onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<TariffDto>());
    }

    /// <summary>Crea una nueva tarifa (arancel).</summary>
    /// <remarks>El nombre/código debe ser único por tenant.</remarks>
    /// <response code="201">Tarifa creada correctamente.</response>
    /// <response code="400">Error de validación (por ejemplo, duplicado).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpPost]
    [Authorize(Policy = "perm:inventario.tariffs.create")]
    [ProducesResponseType(typeof(ApiResponse<TariffDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateTariffCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }
}

