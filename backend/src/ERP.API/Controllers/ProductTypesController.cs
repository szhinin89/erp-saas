using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.DTOs;
using ERP.Application.Products.UseCases.CreateProductType;
using ERP.Application.Products.UseCases.GetProductTypes;
using ERP.Application.Products.UseCases.UpdateProductType;
using ERP.Application.Products.UseCases.DisableProductType;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Master catalog: product types for the authenticated tenant.
/// </summary>
[AppFeature("Tipos de producto", "perm:inventario.productTypes.view", "🔖", "/inventario/product-types", "perm:inventario.products.view", 37)]
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

    /// <summary>Lists product types for the current tenant.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.productTypes.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductTypeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductTypesQuery(onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ProductTypeDto>());
    }

    /// <summary>Creates a new product type.</summary>
    [HttpPost]
    [Authorize(Policy = "perm:inventario.productTypes.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductTypeDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateProductTypeCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Created");
    }

    /// <summary>Updates an existing product type.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:inventario.productTypes.update")]
    [ProducesResponseType(typeof(ApiResponse<ProductTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductTypeCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command with { ProductTypeId = id }, ct);
        return this.ToOkOrBadRequest(result, "Updated");
    }

    /// <summary>Disables (soft-delete) a product type.</summary>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:inventario.productTypes.delete")]
    [ProducesResponseType(typeof(ApiResponse<ProductTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableProductTypeCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Disabled");
    }

    /// <summary>Re-enables a disabled product type.</summary>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:inventario.productTypes.update")]
    [ProducesResponseType(typeof(ApiResponse<ProductTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableProductTypeCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Enabled");
    }
}
