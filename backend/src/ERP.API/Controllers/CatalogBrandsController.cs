using ERP.API.Extensions;
using ERP.Application.Items.UseCases.Brands;
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
public sealed class CatalogBrandsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogBrandsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ══════════════════════════════════════════════════════════════════════
    // BRANDS
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("brands")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetBrands(
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetBrandsQuery(isActive), cancellationToken),
            "OK"
        );

    [HttpGet("brands/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetBrandById(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetBrandByIdQuery(id), cancellationToken));

    [HttpPost("brands")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> CreateBrand(
        [FromBody] CreateBrandCommand command,
        CancellationToken cancellationToken
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    [HttpPut("brands/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> UpdateBrand(
        Guid id,
        [FromBody] UpdateBrandCommand command,
        CancellationToken cancellationToken
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    [HttpPatch("brands/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> EnableBrand(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnableBrandCommand(id), cancellationToken));

    [HttpPatch("brands/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> DisableBrand(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisableBrandCommand(id), cancellationToken));
}
