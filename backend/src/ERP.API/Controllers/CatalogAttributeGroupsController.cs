using ERP.API.Extensions;
using ERP.Application.Items.UseCases.AttributeGroups;
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
public sealed class CatalogAttributeGroupsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogAttributeGroupsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ATTRIBUTE GROUPS
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("attribute-groups")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetAttributeGroups(
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetAttributeGroupsQuery(isActive), cancellationToken),
            "OK"
        );

    [HttpGet("attribute-groups/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetAttributeGroupById(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetAttributeGroupByIdQuery(id), cancellationToken)
        );

    [HttpPost("attribute-groups")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> CreateAttributeGroup(
        [FromBody] CreateAttributeGroupCommand command,
        CancellationToken cancellationToken
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    [HttpPut("attribute-groups/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> UpdateAttributeGroup(
        Guid id,
        [FromBody] UpdateAttributeGroupCommand command,
        CancellationToken cancellationToken
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    [HttpPatch("attribute-groups/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> EnableAttributeGroup(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new EnableAttributeGroupCommand(id), cancellationToken)
        );

    [HttpPatch("attribute-groups/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> DisableAttributeGroup(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DisableAttributeGroupCommand(id), cancellationToken)
        );
}
