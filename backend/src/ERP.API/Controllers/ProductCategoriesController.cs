using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateProductCategory;
using ERP.Application.Products.Catalogs.UseCases.DisableProductCategory;
using ERP.Application.Products.Catalogs.UseCases.EnableProductCategory;
using ERP.Application.Products.Catalogs.UseCases.GetProductCategories;
using ERP.Application.Products.Catalogs.UseCases.UpdateProductCategory;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo maestro: Categorías de producto (dependen de una línea).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ProductCategoriesController : ControllerBase
{
    private readonly CreateProductCategoryHandler _create;
    private readonly GetProductCategoriesHandler _get;
    private readonly UpdateProductCategoryHandler _update;
    private readonly DisableProductCategoryHandler _disable;
    private readonly EnableProductCategoryHandler _enable;

    public ProductCategoriesController(
        CreateProductCategoryHandler create,
        GetProductCategoriesHandler get,
        UpdateProductCategoryHandler update,
        DisableProductCategoryHandler disable,
        EnableProductCategoryHandler enable)
    {
        _create = create;
        _get = get;
        _update = update;
        _disable = disable;
        _enable = enable;
    }

    [HttpGet]
    [Authorize(Policy = "perm:catalog.categories.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductCategoryListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? lineId, CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _get.HandleAsync(lineId, activeFilter, search, ct);
        return Ok(new ApiResponse<IReadOnlyList<ProductCategoryListItemDto>>(
            Success: result.IsSuccess,
            Message: result.IsSuccess ? "OK" : result.Error ?? "Error",
            ResponseObject: result.Value ?? Array.Empty<ProductCategoryListItemDto>()));
    }

    [HttpPost]
    [Authorize(Policy = "perm:catalog.categories.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductCategoryDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateProductCategoryCommand command, CancellationToken ct = default)
    {
        var result = await _create.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<ProductCategoryDto?>(
                Success: true,
                Message: "Creado",
                ResponseObject: result.Value))
            : BadRequest(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "Error",
                ResponseObject: new { }));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:catalog.categories.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCategoryCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return BadRequest(new ApiResponse<object>(false, "El id de ruta no coincide con el cuerpo.", new { }));

        var result = await _update.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<ProductCategoryDto?>(true, "OK", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:catalog.categories.delete")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _disable.HandleAsync(id, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<ProductCategoryDto?>(true, "Deshabilitado", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:catalog.categories.update")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _enable.HandleAsync(id, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<ProductCategoryDto?>(true, "Habilitado", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }
}
