using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateUnitOfMeasure;
using ERP.Application.Products.Catalogs.UseCases.GetUnitsOfMeasure;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo maestro: Unidades de medida (UoM) del tenant autenticado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class UnitsOfMeasureController : ControllerBase
{
    private readonly CreateUnitOfMeasureHandler _create;
    private readonly GetUnitsOfMeasureHandler _get;

    public UnitsOfMeasureController(CreateUnitOfMeasureHandler create, GetUnitsOfMeasureHandler get)
    {
        _create = create;
        _get = get;
    }

    /// <summary>Lista unidades de medida del tenant.</summary>
    /// <param name="onlyActive">Si es true, retorna únicamente unidades habilitadas.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Lista de unidades (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [Authorize(Policy = "perm:catalog.units.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UnitOfMeasureDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _get.HandleAsync(onlyActive, ct);
        return Ok(new ApiResponse<IReadOnlyList<UnitOfMeasureDto>>(
            Success: result.IsSuccess,
            Message: result.IsSuccess ? "OK" : result.Error ?? "Error",
            ResponseObject: result.Value ?? Array.Empty<UnitOfMeasureDto>()));
    }

    /// <summary>Crea una nueva unidad de medida.</summary>
    /// <remarks>El código/nombre debe ser único por tenant.</remarks>
    /// <response code="201">Unidad creada correctamente.</response>
    /// <response code="400">Error de validación (por ejemplo, duplicado).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpPost]
    [Authorize(Policy = "perm:catalog.units.create")]
    [ProducesResponseType(typeof(ApiResponse<UnitOfMeasureDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateUnitOfMeasureCommand command, CancellationToken ct = default)
    {
        var result = await _create.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<UnitOfMeasureDto?>(
                Success: true,
                Message: "Creado",
                ResponseObject: result.Value))
            : BadRequest(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "Error",
                ResponseObject: new { }));
    }
}

