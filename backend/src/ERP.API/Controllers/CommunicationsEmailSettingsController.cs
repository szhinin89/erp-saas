using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.UseCases.GetCompanyEmailSettings;
using ERP.Application.Modules.Communications.UseCases.SendTestEmail;
using ERP.Application.Modules.Communications.UseCases.UpdateCompanyEmailSettings;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Configuración SMTP por empresa (OrgSettings, scope=Company) — CONFIG-AUDIT-01/
/// COMMUNICATIONS-SETTINGS-UI-01. La variable de entorno Communications:Email:* sigue
/// funcionando como fallback de infraestructura cuando la empresa no configuró nada aquí.
/// </summary>
[AppFeature(
    "Correo electrónico",
    $"perm:{CommunicationsPermissions.View}",
    "✉️",
    "/settings/communications/email",
    "perm:settings.group",
    27
)]
[ApiController]
[Route("api/v1/communications/email-settings")]
[Authorize]
[Produces("application/json")]
public sealed class CommunicationsEmailSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CommunicationsEmailSettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Devuelve la configuración SMTP efectiva de la empresa actual (nunca el password en texto plano).</summary>
    /// <response code="200">Configuración de correo; indica si hay contraseña configurada sin revelarla.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{CommunicationsPermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<CommunicationEmailSettingsDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCompanyEmailSettingsQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza la configuración SMTP de la empresa actual.</summary>
    /// <remarks>
    /// Una contraseña SMTP nula o en blanco conserva la ya guardada: este endpoint nunca borra una
    /// contraseña y solo la reemplaza cuando se envía un valor no vacío. Para activar el envío
    /// (<c>enabled</c> en <c>true</c>) se exigen host, usuario y remitente, y que exista una contraseña
    /// (guardada o enviada). La respuesta nunca incluye la contraseña.
    /// </remarks>
    /// <param name="command">Configuración SMTP de la empresa.</param>
    /// <response code="200">Configuración actualizada.</response>
    /// <response code="422">Datos inválidos: faltan campos para activar el envío, no hay contraseña, puerto fuera de 1–65535, correo no válido o reintentos fuera de 0–20.</response>
    [HttpPut]
    [Authorize(Policy = $"perm:{CommunicationsPermissions.Configure}")]
    [ProducesResponseType(
        typeof(ApiResponse<CommunicationEmailSettingsDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateCompanyEmailSettingsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Envía un correo de prueba usando la configuración SMTP actual de la empresa — no crea factura ni toca Sales.</summary>
    /// <remarks>Envía un mensaje real por SMTP al destinatario indicado; no guarda ni modifica la configuración.</remarks>
    /// <param name="command">Correo del destinatario de la prueba.</param>
    /// <response code="200">Correo de prueba enviado.</response>
    /// <response code="422">Destinatario no válido, configuración SMTP incompleta o inactiva, o el servidor SMTP rechazó la conexión, las credenciales o agotó el tiempo de espera.</response>
    [HttpPost("test")]
    [Authorize(Policy = $"perm:{CommunicationsPermissions.Configure}")]
    [ProducesResponseType(typeof(ApiResponse<SendTestEmailResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SendTest(
        [FromBody] SendTestEmailCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
