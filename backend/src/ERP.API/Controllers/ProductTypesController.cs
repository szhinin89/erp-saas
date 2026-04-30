using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateProductType;
using ERP.Application.Products.Catalogs.UseCases.GetProductTypes;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo maestro: Tipos de producto del tenant autenticado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ProductTypesController : ControllerBase
{
    private readonly CreateProductTypeHandler _create;
    private readonly GetProductTypesHandler _get;

    public ProductTypesController(CreateProductTypeHandler create, GetProductTypesHandler get)
    {
        _create = create;
        _get = get;
    }

    /// <summary>Lista tipos de producto del tenant.</summary>
    /// <param name="onlyActive">Si es true, retorna únicamente tipos habilitados.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Lista de tipos (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [Authorize(Policy = "perm:catalog.productTypes.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductTypeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _get.HandleAsync(onlyActive, ct);
        return Ok(new ApiResponse<IReadOnlyList<ProductTypeDto>>(
            Success: result.IsSuccess,
            Message: result.IsSuccess ? "OK" : result.Error ?? "Error",
            ResponseObject: result.Value ?? Array.Empty<ProductTypeDto>()));
    }

    /// <summary>Crea un nuevo tipo de producto.</summary>
    /// <remarks>El nombre debe ser único por tenant.</remarks>
    /// <response code="201">Tipo creado correctamente.</response>
    /// <response code="400">Error de validación (por ejemplo, duplicado).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpPost]
    [Authorize(Policy = "perm:catalog.productTypes.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductTypeDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateProductTypeCommand command, CancellationToken ct = default)
    {
        var result = await _create.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<ProductTypeDto?>(
                Success: true,
                Message: "Creado",
                ResponseObject: result.Value))
            : BadRequest(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "Error",
                ResponseObject: new { }));
    }
}

