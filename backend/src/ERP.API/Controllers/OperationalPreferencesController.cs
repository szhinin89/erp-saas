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
    "Parámetros generales",
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
    /// <response code="200">Preferencias de todos los grupos (ventas/POS, caja, compras, inventario, impresión, documentos electrónicos, notificaciones y cuentas por pagar).</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{OperationalPreferencesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<OperationalPreferencesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetOperationalPreferencesQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza uno o más grupos de preferencias operativas de la empresa actual.</summary>
    /// <remarks>
    /// Cada grupo es opcional: solo se escribe el grupo presente en la solicitud y los demás quedan
    /// intactos; dentro de un grupo presente se envían todos sus campos. Los cambios se guardan en una
    /// sola transacción y la respuesta devuelve las preferencias efectivas resultantes. Cambiar el grupo
    /// <c>electronicDocuments</c> toma el bloqueo del interruptor de emisión electrónica. Rangos
    /// validados: descuento máximo 0–100, copias de recibo 1–3, ancho de papel <c>80mm</c> o
    /// <c>58mm</c>, modo de impresión <c>AskBeforePrint</c>, <c>AlwaysPrint</c> o
    /// <c>NeverAutoPrint</c>, idioma <c>es</c> o <c>en</c>, y los montos no pueden ser negativos.
    /// </remarks>
    /// <param name="command">Grupos de preferencias a actualizar.</param>
    /// <response code="200">Preferencias efectivas tras la actualización.</response>
    /// <response code="422">Algún valor no cumple las reglas del grupo enviado.</response>
    [HttpPut]
    [Authorize(Policy = $"perm:{OperationalPreferencesPermissions.Configure}")]
    [ProducesResponseType(typeof(ApiResponse<OperationalPreferencesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateOperationalPreferencesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
