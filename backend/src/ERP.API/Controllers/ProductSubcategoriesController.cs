using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateProductSubcategory;
using ERP.Application.Products.Catalogs.UseCases.DisableProductSubcategory;
using ERP.Application.Products.Catalogs.UseCases.EnableProductSubcategory;
using ERP.Application.Products.Catalogs.UseCases.GetProductSubcategories;
using ERP.Application.Products.Catalogs.UseCases.UpdateProductSubcategory;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>Catálogo maestro: Subcategorías (dependen de una categoría).</summary>
[AppFeature("Subcategorías", "perm:inventario.subcategories.view", "📂", "/inventario/structure", "perm:inventario.products.view", 39)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ProductSubcategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductSubcategoriesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:inventario.subcategories.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductSubcategoryListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? lineId, [FromQuery] Guid? categoryId, CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _mediator.Send(new GetProductSubcategoriesQuery(lineId, categoryId, activeFilter, search), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ProductSubcategoryListItemDto>());
    }

    [HttpPost]
    [Authorize(Policy = "perm:inventario.subcategories.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductSubcategoryDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateProductSubcategoryCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:inventario.subcategories.update")]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductSubcategoryCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:inventario.subcategories.delete")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableProductSubcategoryCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Deshabilitado");
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:inventario.subcategories.update")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableProductSubcategoryCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Habilitado");
    }
}
