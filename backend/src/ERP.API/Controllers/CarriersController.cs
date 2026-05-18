using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Logistics.DTOs;
using ERP.Application.Modules.Logistics.UseCases.CreateCarrier;
using ERP.Application.Modules.Logistics.UseCases.DisableCarrier;
using ERP.Application.Modules.Logistics.UseCases.GetCarriers;
using ERP.Application.Modules.Logistics.UseCases.UpdateCarrier;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Master catalog: Authorized transport carriers for delivery guides.</summary>
[AppFeature("Transportistas", "perm:logistics.carriers.view", "local_shipping", "/logistics/carriers", null, 60)]
[ApiController]
[Route("api/logistics/carriers")]
[Authorize]
[Produces("application/json")]
public sealed class CarriersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CarriersController(IMediator mediator) => _mediator = mediator;

    /// <summary>Returns all carriers for the authenticated tenant.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:logistics.carriers.view")]
    [ProducesResponseType(typeof(ApiResponse<List<CarrierDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search    = null,
        [FromQuery] string? activeStatus = "all",
        CancellationToken ct = default)
    {
        bool? isActive = activeStatus switch
        {
            "active"   => true,
            "inactive" => false,
            _          => null,
        };
        var result = await _mediator.Send(new GetCarriersQuery(search, isActive), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>Creates a new carrier.</summary>
    [HttpPost]
    [Authorize(Policy = "perm:logistics.carriers.create")]
    [ProducesResponseType(typeof(ApiResponse<CarrierDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateCarrierCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Created");
    }

    /// <summary>Updates an existing carrier.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:logistics.carriers.update")]
    [ProducesResponseType(typeof(ApiResponse<CarrierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCarrierCommand command, CancellationToken ct = default)
    {
        if (id != command.CarrierId)
            return BadRequest("Route id does not match body CarrierId.");
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Updated");
    }

    /// <summary>Disables a carrier (soft delete).</summary>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:logistics.carriers.delete")]
    [ProducesResponseType(typeof(ApiResponse<CarrierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableCarrierCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Disabled");
    }

    /// <summary>Re-enables a disabled carrier.</summary>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:logistics.carriers.update")]
    [ProducesResponseType(typeof(ApiResponse<CarrierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableCarrierCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Enabled");
    }
}
