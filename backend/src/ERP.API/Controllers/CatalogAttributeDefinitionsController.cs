using ERP.API.Extensions;
using ERP.Application.Items.UseCases.AttributeDefinitions;
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
public sealed class CatalogAttributeDefinitionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogAttributeDefinitionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ATTRIBUTE DEFINITIONS
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("attribute-definitions")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetAttributeDefinitions(
        [FromQuery] Guid? groupId = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetAttributeDefinitionsQuery(groupId, isActive),
                cancellationToken
            ),
            "OK"
        );

    [HttpGet("attribute-definitions/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetAttributeDefinitionById(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetAttributeDefinitionByIdQuery(id), cancellationToken)
        );

    [HttpPost("attribute-definitions")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> CreateAttributeDefinition(
        [FromBody] CreateAttributeDefinitionCommand command,
        CancellationToken cancellationToken
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    [HttpPut("attribute-definitions/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> UpdateAttributeDefinition(
        Guid id,
        [FromBody] UpdateAttributeDefinitionCommand command,
        CancellationToken cancellationToken
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    [HttpPatch("attribute-definitions/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> EnableAttributeDefinition(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new EnableAttributeDefinitionCommand(id), cancellationToken)
        );

    [HttpPatch("attribute-definitions/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> DisableAttributeDefinition(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DisableAttributeDefinitionCommand(id), cancellationToken)
        );
}
