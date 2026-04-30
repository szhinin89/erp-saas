using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateProductLine;
using ERP.Application.Products.Catalogs.UseCases.DisableProductLine;
using ERP.Application.Products.Catalogs.UseCases.EnableProductLine;
using ERP.Application.Products.Catalogs.UseCases.GetProductLines;
using ERP.Application.Products.Catalogs.UseCases.UpdateProductLine;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo maestro: Líneas de producto (Product Lines) del tenant autenticado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ProductLinesController : ControllerBase
{
    private readonly CreateProductLineHandler _create;
    private readonly GetProductLinesHandler _get;
    private readonly UpdateProductLineHandler _update;
    private readonly DisableProductLineHandler _disable;
    private readonly EnableProductLineHandler _enable;

    public ProductLinesController(
        CreateProductLineHandler create,
        GetProductLinesHandler get,
        UpdateProductLineHandler update,
        DisableProductLineHandler disable,
        EnableProductLineHandler enable)
    {
        _create = create;
        _get = get;
        _update = update;
        _disable = disable;
        _enable = enable;
    }

    /// <summary>Lista líneas de producto del tenant.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:catalog.productLines.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductLineDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _get.HandleAsync(activeFilter, search, ct);
        return Ok(new ApiResponse<IReadOnlyList<ProductLineDto>>(
            Success: result.IsSuccess,
            Message: result.IsSuccess ? "OK" : result.Error ?? "Error",
            ResponseObject: result.Value ?? Array.Empty<ProductLineDto>()));
    }

    [HttpPost]
    [Authorize(Policy = "perm:catalog.productLines.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductLineDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateProductLineCommand command, CancellationToken ct = default)
    {
        var result = await _create.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<ProductLineDto?>(
                Success: true,
                Message: "Creado",
                ResponseObject: result.Value))
            : BadRequest(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "Error",
                ResponseObject: new { }));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:catalog.productLines.update")]
    [ProducesResponseType(typeof(ApiResponse<ProductLineDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductLineCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return BadRequest(new ApiResponse<object>(false, "El id de ruta no coincide con el cuerpo.", new { }));

        var result = await _update.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<ProductLineDto?>(true, "OK", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:catalog.productLines.delete")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _disable.HandleAsync(id, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<ProductLineDto?>(true, "Deshabilitado", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:catalog.productLines.update")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _enable.HandleAsync(id, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<ProductLineDto?>(true, "Habilitado", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }
}
