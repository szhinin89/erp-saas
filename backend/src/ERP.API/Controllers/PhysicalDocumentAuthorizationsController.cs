using ERP.API.Extensions;
using ERP.Application.Modules.Company.UseCases.PhysicalDocumentAuthorizations;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// PHYSICAL-FISCAL-F1 — autorizaciones de impresión del SRI por punto de emisión físico y tipo de
/// comprobante. Se administran en la pantalla de Secuencias documentales (misma numeración por
/// punto y tipo), con su mismo permiso; no tienen ítem de menú propio.
/// </summary>
[ApiController]
[Route("api/v1/settings/physical-document-authorizations")]
[Authorize]
[Produces("application/json")]
public sealed class PhysicalDocumentAuthorizationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PhysicalDocumentAuthorizationsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista las autorizaciones (activas e inactivas) de la empresa activa.</summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentSequencesManage}")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetPhysicalDocumentAuthorizationsQuery(),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Registra una autorización de impresión tal como consta en el documento del SRI.</summary>
    [HttpPost]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentSequencesManage}")]
    public async Task<IActionResult> Create(
        [FromBody] CreatePhysicalDocumentAuthorizationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>Deshabilita una autorización (soft delete).</summary>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentSequencesManage}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new DisablePhysicalDocumentAuthorizationCommand(id),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }
}
