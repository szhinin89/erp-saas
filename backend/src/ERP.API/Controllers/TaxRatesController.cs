using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.GetTaxRates;
using ERP.Domain.Products.Entities;

namespace ERP.API.Controllers;

/// <summary>
/// Tarifas de impuestos SRI (IVA/ICE) — solo lectura.
/// Los valores oficiales se cargan desde sri_vat_rate; el tenant los asigna al producto, no los crea.
/// </summary>
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

    /// <summary>Lista tarifas de impuestos SRI (IVA/ICE) disponibles para asignar a productos.</summary>
    /// <param name="type">Filtra por tipo (VAT = IVA, Excise = ICE). Si es null, retorna todas.</param>
    /// <param name="onlyActive">Si es true, retorna únicamente tarifas vigentes.</param>
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
}

