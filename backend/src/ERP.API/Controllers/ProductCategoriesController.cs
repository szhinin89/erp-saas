using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Application.Products.Catalogs.UseCases.CreateProductCategory;
using ERP.Application.Products.Catalogs.UseCases.DisableProductCategory;
using ERP.Application.Products.Catalogs.UseCases.EnableProductCategory;
using ERP.Application.Products.Catalogs.UseCases.GetProductCategories;
using ERP.Application.Products.Catalogs.UseCases.UpdateProductCategory;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>Catálogo maestro: Categorías de producto (dependen de una línea).</summary>
[Modulo("Categorías", "perm:inventario.categories.view", "📁", "/inventario/structure", "perm:inventario.products.view", 39)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ProductCategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductCategoriesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:inventario.categories.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductCategoryListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? lineId, CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _mediator.Send(new GetProductCategoriesQuery(lineId, activeFilter, search), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ProductCategoryListItemDto>());
    }

    [HttpPost]
    [Authorize(Policy = "perm:inventario.categories.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductCategoryDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateProductCategoryCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:inventario.categories.update")]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCategoryCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:inventario.categories.delete")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableProductCategoryCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Deshabilitado");
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:inventario.categories.update")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableProductCategoryCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Habilitado");
    }
}
