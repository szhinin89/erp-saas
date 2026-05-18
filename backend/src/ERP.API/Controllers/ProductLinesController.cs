using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.DTOs;
using ERP.Application.Products.UseCases.CreateProductLine;
using ERP.Application.Products.UseCases.DisableProductLine;
using ERP.Application.Products.UseCases.EnableProductLine;
using ERP.Application.Products.UseCases.GetProductLines;
using ERP.Application.Products.UseCases.UpdateProductLine;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>Catálogo maestro: Líneas de producto del tenant autenticado.</summary>
[AppFeature("Líneas de producto", "perm:inventory.product-lines.view", "🏷️", "/inventory/catalog-structure", "perm:inventory.products.view", 37)]
[ApiController]
[Route("api/inventory/product-lines")]
[Authorize]
[Produces("application/json")]
public class ProductLinesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductLinesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista líneas de producto del tenant.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:inventory.product-lines.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductLineDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _mediator.Send(new GetProductLinesQuery(activeFilter, search), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ProductLineDto>());
    }

    [HttpPost]
    [Authorize(Policy = "perm:inventory.product-lines.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductLineDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateProductLineCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:inventory.product-lines.update")]
    [ProducesResponseType(typeof(ApiResponse<ProductLineDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductLineCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:inventory.product-lines.delete")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableProductLineCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Deshabilitado");
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:inventory.product-lines.update")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableProductLineCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Habilitado");
    }
}
