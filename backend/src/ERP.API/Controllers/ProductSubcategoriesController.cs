using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateProductSubcategory;
using ERP.Application.Products.Catalogs.UseCases.DisableProductSubcategory;
using ERP.Application.Products.Catalogs.UseCases.EnableProductSubcategory;
using ERP.Application.Products.Catalogs.UseCases.GetProductSubcategories;
using ERP.Application.Products.Catalogs.UseCases.UpdateProductSubcategory;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo maestro: Subcategorías (dependen de una categoría).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ProductSubcategoriesController : ControllerBase
{
    private readonly CreateProductSubcategoryHandler _create;
    private readonly GetProductSubcategoriesHandler _get;
    private readonly UpdateProductSubcategoryHandler _update;
    private readonly DisableProductSubcategoryHandler _disable;
    private readonly EnableProductSubcategoryHandler _enable;

    public ProductSubcategoriesController(
        CreateProductSubcategoryHandler create,
        GetProductSubcategoriesHandler get,
        UpdateProductSubcategoryHandler update,
        DisableProductSubcategoryHandler disable,
        EnableProductSubcategoryHandler enable)
    {
        _create = create;
        _get = get;
        _update = update;
        _disable = disable;
        _enable = enable;
    }

    [HttpGet]
    [Authorize(Policy = "perm:catalog.subcategories.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductSubcategoryListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? lineId, [FromQuery] Guid? categoryId, CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _get.HandleAsync(lineId, categoryId, activeFilter, search, ct);
        return Ok(new ApiResponse<IReadOnlyList<ProductSubcategoryListItemDto>>(
            Success: result.IsSuccess,
            Message: result.IsSuccess ? "OK" : result.Error ?? "Error",
            ResponseObject: result.Value ?? Array.Empty<ProductSubcategoryListItemDto>()));
    }

    [HttpPost]
    [Authorize(Policy = "perm:catalog.subcategories.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductSubcategoryDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateProductSubcategoryCommand command, CancellationToken ct = default)
    {
        var result = await _create.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<ProductSubcategoryDto?>(
                Success: true,
                Message: "Creado",
                ResponseObject: result.Value))
            : BadRequest(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "Error",
                ResponseObject: new { }));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:catalog.subcategories.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductSubcategoryCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return BadRequest(new ApiResponse<object>(false, "El id de ruta no coincide con el cuerpo.", new { }));

        var result = await _update.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<ProductSubcategoryDto?>(true, "OK", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:catalog.subcategories.delete")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _disable.HandleAsync(id, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<ProductSubcategoryDto?>(true, "Deshabilitado", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:catalog.subcategories.update")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _enable.HandleAsync(id, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<ProductSubcategoryDto?>(true, "Habilitado", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }
}
