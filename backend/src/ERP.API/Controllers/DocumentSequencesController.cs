using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Company.UseCases.ConfigureDocumentSequence;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// DOCUMENT-SEQUENCES-CONFIG-03 — administración mínima de <c>DocumentSequence</c> (secuencias
/// documentales SRI centralizadas, ADR-019). Un único endpoint: configurar el número inicial
/// antes del primer uso real. Sin UI en esta fase (ver
/// docs/decisions/DOCUMENT-SEQUENCES-DESIGN-02.md) — <c>IsVisibleInMenu = false</c> registra el
/// permiso en el catálogo (para asignación por rol) sin agregar un ítem de navegación real.
/// </summary>
[AppFeature(
    "Secuencias documentales",
    $"perm:{SettingsPermissions.DocumentSequencesManage}",
    null,
    "/settings/document-sequences",
    $"perm:{SettingsPermissions.EmissionPointsView}",
    41,
    IsVisibleInMenu = false
)]
[ApiController]
[Route("api/v1/settings/document-sequences")]
[Authorize]
[Produces("application/json")]
public sealed class DocumentSequencesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentSequencesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Configura el próximo secuencial de una secuencia documental SRI, antes de su primer uso
    /// real. Rechaza el ajuste (409) si la secuencia ya entregó al menos un número real — ver
    /// <c>DocumentSequence.ConfigureNextNumber</c>.
    /// </summary>
    [HttpPut("configure")]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentSequencesManage}")]
    public async Task<IActionResult> Configure(
        [FromBody] ConfigureDocumentSequenceCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
