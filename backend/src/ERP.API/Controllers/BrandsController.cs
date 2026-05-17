using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.DTOs;
using ERP.Application.Products.UseCases.CreateBrand;
using ERP.Application.Products.UseCases.GetBrands;
using ERP.Application.Products.UseCases.UpdateBrand;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>Master catalog: Brands of the authenticated tenant.</summary>
[AppFeature("Marcas", "perm:inventario.brands.view", "✨", "/inventario/brands", "perm:inventario.products.view", 37)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class BrandsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BrandsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Returns all brands for the tenant.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.brands.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BrandDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBrandsQuery(onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<BrandDto>());
    }

    /// <summary>Creates a new brand.</summary>
    [HttpPost]
    [Authorize(Policy = "perm:inventario.brands.create")]
    [ProducesResponseType(typeof(ApiResponse<BrandDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateBrandCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Created");
    }

    /// <summary>Updates an existing brand.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:inventario.brands.update")]
    [ProducesResponseType(typeof(ApiResponse<BrandDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBrandCommand command, CancellationToken ct = default)
    {
        if (id != command.BrandId)
            return BadRequest("Route id does not match body BrandId.");
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Updated");
    }

    /// <summary>Disables a brand (soft delete).</summary>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:inventario.brands.delete")]
    [ProducesResponseType(typeof(ApiResponse<BrandDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableBrandCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Disabled");
    }

    /// <summary>Re-enables a disabled brand.</summary>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:inventario.brands.update")]
    [ProducesResponseType(typeof(ApiResponse<BrandDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableBrandCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Enabled");
    }
}
