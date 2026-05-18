using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.DTOs;
using ERP.Application.Products.UseCases.CreateUnitOfMeasure;
using ERP.Application.Products.UseCases.GetUnitsOfMeasure;
using ERP.Application.Products.UseCases.UpdateUnitOfMeasure;
using ERP.Application.Products.UseCases.DisableUnitOfMeasure;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Master catalog: units of measure for the authenticated tenant.
/// </summary>
[AppFeature("Unidades de medida", "perm:inventory.units.view", "📏", "/inventory/units", "perm:inventory.products.view", 37)]
[ApiController]
[Route("api/inventory/units")]
[Authorize]
[Produces("application/json")]
public class UnitsOfMeasureController : ControllerBase
{
    private readonly IMediator _mediator;

    public UnitsOfMeasureController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lists units of measure for the current tenant.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:inventory.units.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UnitOfMeasureDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetUnitsOfMeasureQuery(onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<UnitOfMeasureDto>());
    }

    /// <summary>Creates a new unit of measure.</summary>
    [HttpPost]
    [Authorize(Policy = "perm:inventory.units.create")]
    [ProducesResponseType(typeof(ApiResponse<UnitOfMeasureDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateUnitOfMeasureCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Created");
    }

    /// <summary>Updates an existing unit of measure.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:inventory.units.update")]
    [ProducesResponseType(typeof(ApiResponse<UnitOfMeasureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUnitOfMeasureCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command with { UnitOfMeasureId = id }, ct);
        return this.ToOkOrBadRequest(result, "Updated");
    }

    /// <summary>Disables (soft-delete) a unit of measure.</summary>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:inventory.units.delete")]
    [ProducesResponseType(typeof(ApiResponse<UnitOfMeasureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableUnitOfMeasureCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Disabled");
    }

    /// <summary>Re-enables a disabled unit of measure.</summary>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:inventory.units.update")]
    [ProducesResponseType(typeof(ApiResponse<UnitOfMeasureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableUnitOfMeasureCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Enabled");
    }
}
