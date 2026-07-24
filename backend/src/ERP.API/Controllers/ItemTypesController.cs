using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Items.UseCases;
using ERP.Domain.Kernel.Permissions;

namespace ERP.API.Controllers;

[AppFeature("Tipos de Ítem", $"perm:{InventoryPermissions.ItemsView}", "🏷️", "/inventory/item-types", null, 36)]
[ApiController]
[Route("api/v1/item-types")]
[Authorize]
[Produces("application/json")]
public sealed class ItemTypesController : ControllerBase
{
    private readonly IMediator _mediator;
    public ItemTypesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool onlyActive = true, CancellationToken ct = default)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetItemTypesQuery(onlyActive), ct), "OK");

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => this.ToOkOrNotFound(await _mediator.Send(new GetItemTypeByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsCreate}")]
    public async Task<IActionResult> Create(
        [FromBody] CreateItemTypeCommand cmd, CancellationToken ct)
        => this.ToCreatedOrBadRequest(await _mediator.Send(cmd, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateItemTypeCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct));
    }

    [HttpPost("{id:guid}/toggle")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new ToggleItemTypeCommand(id), ct));
}
