using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateProductType;
using ERP.Application.Products.Catalogs.UseCases.GetProductTypes;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo maestro: Tipos de producto del tenant autenticado.
/// </summary>
[Modulo("Tipos de producto", "perm:inventario.productTypes.view", "🔖", "/inventario/product-types", "perm:inventario.products.view", 37)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ProductTypesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductTypesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lista tipos de producto del tenant.</summary>
    /// <param name="onlyActive">Si es true, retorna únicamente tipos habilitados.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Lista de tipos (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.productTypes.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductTypeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductTypesQuery(onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ProductTypeDto>());
    }

    /// <summary>Crea un nuevo tipo de producto.</summary>
    /// <remarks>El nombre debe ser único por tenant.</remarks>
    /// <response code="201">Tipo creado correctamente.</response>
    /// <response code="400">Error de validación (por ejemplo, duplicado).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpPost]
    [Authorize(Policy = "perm:inventario.productTypes.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductTypeDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateProductTypeCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }
}

