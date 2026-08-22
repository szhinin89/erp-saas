using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Settings.Operations.DTOs;
using ERP.Application.Modules.Settings.Operations.UseCases.GetOperationalPreferences;
using ERP.Application.Modules.Settings.Operations.UseCases.UpdateOperationalPreferences;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// CONFIG-DYNAMIC-OPERATIONS-01: preferencias operativas dinámicas por empresa (Ventas/POS, Caja,
/// Compras, Inventario, Impresión, Documentos Electrónicos, Notificaciones) — OrgSettings,
/// scope=Company. Cada grupo del PUT es opcional: solo se escribe el grupo enviado.
/// </summary>
[AppFeature(
    "Preferencias operativas",
    $"perm:{OperationalPreferencesPermissions.View}",
    "tune",
    "/settings/operations",
    "perm:settings.group",
    28
)]
[ApiController]
[Route("api/v1/settings/operations")]
[Authorize]
[Produces("application/json")]
public sealed class OperationalPreferencesController : ControllerBase
{
    private readonly IMediator _mediator;

    public OperationalPreferencesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Devuelve las preferencias operativas efectivas de la empresa actual.</summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{OperationalPreferencesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<OperationalPreferencesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetOperationalPreferencesQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza uno o más grupos de preferencias operativas de la empresa actual.</summary>
    [HttpPut]
    [Authorize(Policy = $"perm:{OperationalPreferencesPermissions.Configure}")]
    [ProducesResponseType(typeof(ApiResponse<OperationalPreferencesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateOperationalPreferencesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
