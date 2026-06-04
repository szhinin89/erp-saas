using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Kits.DTOs;
using ERP.Application.Kits.UseCases.CreateKit;
using ERP.Application.Kits.UseCases.AddKitLine;
using ERP.Application.Kits.UseCases.GetKitById;
using ERP.Domain.Modules.Kits.Enums;

namespace ERP.API.Controllers;

[AppFeature("Kits / BOM", "perm:kits.manage", "🧩", "/kits", null, 50)]
[ApiController]
[Route("api/kits")]
[Authorize]
[Produces("application/json")]
public sealed class KitsController : ControllerBase
{
    private readonly IMediator _mediator;
    public KitsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("by-item/{itemId:guid}")]
    [Authorize(Policy = "perm:kits.manage")]
    [ProducesResponseType(typeof(ApiResponse<KitDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByItem(Guid itemId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetKitByItemIdQuery(itemId), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpPost]
    [Authorize(Policy = "perm:kits.manage")]
    [ProducesResponseType(typeof(ApiResponse<KitDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateKitCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Kit creado");
    }

    [HttpPost("{id:guid}/lines")]
    [Authorize(Policy = "perm:kits.manage")]
    [ProducesResponseType(typeof(ApiResponse<KitLineDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddLine(
        Guid id, [FromBody] AddKitLineRequest request, CancellationToken ct)
    {
        var command = new AddKitLineCommand(
            id, request.ComponentItemId, request.UomCode, request.Quantity,
            request.ComponentVariantId, request.IsOptional, request.SortOrder);
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Línea agregada");
    }
}

public record AddKitLineRequest(
    Guid    ComponentItemId,
    string  UomCode,
    decimal Quantity,
    Guid?   ComponentVariantId = null,
    bool    IsOptional         = false,
    int     SortOrder          = 0
);
