using ERP.API.Extensions;
using ERP.Application.Items.UseCases.CategoryNodes;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// Split out of CatalogController (B-controller max-lines) — mismo Route base, mismos
// endpoints, mismos permisos. Ver CatalogController.cs para el resto del catálogo.
[ApiController]
[Route("api/v1/catalog")]
[Authorize]
[Produces("application/json")]
public sealed class CatalogCategoryNodesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogCategoryNodesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ══════════════════════════════════════════════════════════════════════
    // CATEGORY NODES (unified tree)
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("category-nodes")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetCategoryTree(
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCategoryTreeQuery(includeInactive), cancellationToken)
        );

    [HttpGet("category-nodes/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetCategoryNodeById(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetCategoryNodeByIdQuery(id), cancellationToken)
        );

    [HttpPost("category-nodes")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> CreateCategoryNode(
        [FromBody] CreateCategoryNodeCommand command,
        CancellationToken cancellationToken
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    [HttpPut("category-nodes/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> UpdateCategoryNode(
        Guid id,
        [FromBody] UpdateCategoryNodeCommand command,
        CancellationToken cancellationToken
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    [HttpPatch("category-nodes/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> DisableCategoryNode(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DisableCategoryNodeCommand(id), cancellationToken)
        );

    [HttpPatch("category-nodes/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> EnableCategoryNode(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new EnableCategoryNodeCommand(id), cancellationToken)
        );
}
