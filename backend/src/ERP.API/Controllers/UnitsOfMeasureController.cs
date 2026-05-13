using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateUnitOfMeasure;
using ERP.Application.Products.Catalogs.UseCases.GetUnitsOfMeasure;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo maestro: Unidades de medida (UoM) del tenant autenticado.
/// </summary>
[Modulo("Unidades de medida", "perm:inventario.units.view", "📏", "/catalog/units", null, 37)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class UnitsOfMeasureController : ControllerBase
{
    private readonly IMediator _mediator;

    public UnitsOfMeasureController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lista unidades de medida del tenant.</summary>
    /// <param name="onlyActive">Si es true, retorna únicamente unidades habilitadas.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Lista de unidades (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.units.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UnitOfMeasureDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetUnitsOfMeasureQuery(onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<UnitOfMeasureDto>());
    }

    /// <summary>Crea una nueva unidad de medida.</summary>
    /// <remarks>El código/nombre debe ser único por tenant.</remarks>
    /// <response code="201">Unidad creada correctamente.</response>
    /// <response code="400">Error de validación (por ejemplo, duplicado).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpPost]
    [Authorize(Policy = "perm:inventario.units.create")]
    [ProducesResponseType(typeof(ApiResponse<UnitOfMeasureDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateUnitOfMeasureCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }
}

