using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateBrand;
using ERP.Application.Products.Catalogs.UseCases.GetBrands;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo maestro: Marcas (Brands) del tenant autenticado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class BrandsController : ControllerBase
{
    private readonly CreateBrandHandler _create;
    private readonly GetBrandsHandler _get;

    public BrandsController(CreateBrandHandler create, GetBrandsHandler get)
    {
        _create = create;
        _get = get;
    }

    /// <summary>Lista marcas del tenant.</summary>
    /// <param name="onlyActive">Si es true, retorna únicamente marcas habilitadas.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Lista de marcas (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [Authorize(Policy = "perm:catalog.brands.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BrandDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _get.HandleAsync(onlyActive, ct);
        return Ok(new ApiResponse<IReadOnlyList<BrandDto>>(
            Success: result.IsSuccess,
            Message: result.IsSuccess ? "OK" : result.Error ?? "Error",
            ResponseObject: result.Value ?? Array.Empty<BrandDto>()));
    }

    /// <summary>Crea una nueva marca.</summary>
    /// <remarks>El nombre debe ser único por tenant.</remarks>
    /// <response code="201">Marca creada correctamente.</response>
    /// <response code="400">Error de validación (por ejemplo, duplicado).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpPost]
    [Authorize(Policy = "perm:catalog.brands.create")]
    [ProducesResponseType(typeof(ApiResponse<BrandDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateBrandCommand command, CancellationToken ct = default)
    {
        var result = await _create.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<BrandDto?>(
                Success: true,
                Message: "Creado",
                ResponseObject: result.Value))
            : BadRequest(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "Error",
                ResponseObject: new { }));
    }
}

