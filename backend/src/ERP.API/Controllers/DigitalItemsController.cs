using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.DigitalItems.DTOs;
using ERP.Application.DigitalItems.UseCases.CreateDigitalItem;
using ERP.Application.DigitalItems.UseCases.AddDeliverable;
using ERP.Application.DigitalItems.UseCases.GetDigitalItem;

namespace ERP.API.Controllers;

[AppFeature("Ítems Digitales", "perm:digital-items.manage", "💾", "/digital-items", null, 53)]
[ApiController]
[Route("api/digital-items")]
[Authorize]
[Produces("application/json")]
public sealed class DigitalItemsController : ControllerBase
{
    private readonly IMediator _mediator;
    public DigitalItemsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("by-item/{itemId:guid}")]
    [Authorize(Policy = "perm:digital-items.manage")]
    [ProducesResponseType(typeof(ApiResponse<DigitalItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByItem(Guid itemId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDigitalItemByItemIdQuery(itemId), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpPost]
    [Authorize(Policy = "perm:digital-items.manage")]
    [ProducesResponseType(typeof(ApiResponse<DigitalItemDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDigitalItemCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Ítem digital creado");
    }

    [HttpPost("{id:guid}/deliverables")]
    [Authorize(Policy = "perm:digital-items.manage")]
    [ProducesResponseType(typeof(ApiResponse<DeliverableDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddDeliverable(
        Guid id, [FromBody] AddDeliverableRequest request, CancellationToken ct)
    {
        var command = new AddDeliverableCommand(id, request.Name, request.StorageObjectId, request.Version);
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Descargable agregado");
    }
}

public record AddDeliverableRequest(
    string  Name,
    Guid    StorageObjectId,
    string? Version = null
);
