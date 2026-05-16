using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateBrand;
using ERP.Application.Products.Catalogs.UseCases.GetBrands;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo maestro: Marcas (Brands) del tenant autenticado.
/// </summary>
[AppFeature("Marcas", "perm:inventario.brands.view", "✨", "/inventario/brands", "perm:inventario.products.view", 37)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class BrandsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BrandsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lista marcas del tenant.</summary>
    /// <param name="onlyActive">Si es true, retorna únicamente marcas habilitadas.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Lista de marcas (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.brands.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BrandDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBrandsQuery(onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<BrandDto>());
    }

    /// <summary>Crea una nueva marca.</summary>
    /// <remarks>El nombre debe ser único por tenant.</remarks>
    /// <response code="201">Marca creada correctamente.</response>
    /// <response code="400">Error de validación (por ejemplo, duplicado).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpPost]
    [Authorize(Policy = "perm:inventario.brands.create")]
    [ProducesResponseType(typeof(ApiResponse<BrandDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateBrandCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }
}

