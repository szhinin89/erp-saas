using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateTaxRate;
using ERP.Application.Products.Catalogs.UseCases.GetTaxRates;
using ERP.Domain.Products.Entities;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo maestro: Tarifas de impuestos (IVA/ICE) del tenant autenticado.
/// </summary>
[Modulo("Impuestos (IVA/ICE)", "perm:inventario.taxRates.view", "📊", "/inventario/tax-rates", "perm:inventario.products.view", 37)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TaxRatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public TaxRatesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lista tarifas de impuestos (IVA/ICE).</summary>
    /// <param name="type">Filtra por tipo de tarifa (por ejemplo IVA o ICE). Si es null, retorna todas.</param>
    /// <param name="onlyActive">Si es true, retorna únicamente tarifas habilitadas.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Lista de tarifas (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.taxRates.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TaxRateDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] TaxRateType? type, [FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTaxRatesQuery(type, onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<TaxRateDto>());
    }

    /// <summary>Crea una nueva tarifa de impuesto (IVA/ICE).</summary>
    /// <remarks>El porcentaje y el tipo deben ser consistentes según la normativa del tenant.</remarks>
    /// <response code="201">Tarifa creada correctamente.</response>
    /// <response code="400">Error de validación (por ejemplo, duplicado o porcentaje inválido).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpPost]
    [Authorize(Policy = "perm:inventario.taxRates.create")]
    [ProducesResponseType(typeof(ApiResponse<TaxRateDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateTaxRateCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }
}

