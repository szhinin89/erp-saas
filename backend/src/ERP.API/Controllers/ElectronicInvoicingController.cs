using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.API.Uploads;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.GetElectronicInvoicingStatus;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSriConfiguration;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.InspectSriCertificate;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.UploadSriCertificate;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSriConfiguration;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.ValidateSriConfiguration;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Facturación electrónica",
    $"perm:{ElectronicInvoicingPermissions.View}",
    "🧾",
    "/sri/configuration/electronic-invoicing",
    "perm:settings.group",
    25
)]
[ApiController]
[Route("api/v1/electronic-invoicing")]
[Authorize]
[Produces("application/json")]
public sealed class ElectronicInvoicingController : ControllerBase
{
    private readonly IMediator _mediator;

    public ElectronicInvoicingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Estado liviano de la facturación electrónica de la empresa.
    /// </summary>
    /// <remarks>
    /// Visible para cualquier usuario autenticado de la empresa, sin requerir el permiso de Configuración SRI: solo
    /// informa ambiente y estado de emisión. Por defecto (<c>checkConnectivity</c> ausente o falso) no hace ping
    /// externo al SRI: resuelve solo el estado local (certificado, ambiente y URL). Con
    /// <c>checkConnectivity=true</c> fuerza el ping real.
    /// </remarks>
    /// <param name="checkConnectivity">Si es <c>true</c> hace un ping real al SRI; por defecto solo resuelve el estado local.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Estado de la facturación electrónica.</response>
    [HttpGet("status")]
    [ProducesResponseType(
        typeof(ApiResponse<ElectronicInvoicingStatusDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetStatus(
        [FromQuery] bool checkConnectivity = false,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetElectronicInvoicingStatusQuery(checkConnectivity),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Obtiene la configuración SRI de la empresa.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Configuración SRI.</response>
    [HttpGet("sri-configuration")]
    [Authorize(Policy = $"perm:{ElectronicInvoicingPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<SriConfigurationDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSriConfiguration(
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(new GetSriConfigurationQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Crea o actualiza la configuración SRI de la empresa.
    /// </summary>
    /// <remarks>
    /// Los secretos del cuerpo (por ejemplo la contraseña del certificado) son de solo escritura y no se devuelven.
    /// </remarks>
    /// <param name="command">Datos de la configuración SRI.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Configuración SRI guardada.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPut("sri-configuration")]
    [Authorize(Policy = $"perm:{ElectronicInvoicingPermissions.Configure}")]
    [ProducesResponseType(typeof(ApiResponse<SriConfigurationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertSriConfiguration(
        [FromBody] UpsertSriConfigurationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Diagnostica la configuración SRI actual.
    /// </summary>
    /// <remarks>
    /// Revisa certificado, contraseña, vigencia y conectividad del webservice. Solo lectura: nunca modifica la
    /// configuración guardada.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado del diagnóstico.</response>
    [HttpGet("sri-configuration/validate")]
    [Authorize(Policy = $"perm:{ElectronicInvoicingPermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<SriConfigurationValidationDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> ValidateSriConfiguration(
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(new ValidateSriConfigurationQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Sube o reemplaza el certificado digital de la empresa.
    /// </summary>
    /// <remarks>
    /// Solicitud <c>multipart/form-data</c> con el archivo en el campo <c>file</c>. Acepta solo extensiones
    /// <c>.p12</c> y <c>.pfx</c>, hasta 5 MB. El backend administra el almacenamiento: el cliente nunca envía ni
    /// conoce una ruta de servidor.
    /// </remarks>
    /// <param name="file">Archivo del certificado digital (<c>.p12</c> o <c>.pfx</c>, máximo 5 MB).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Certificado cargado.</response>
    /// <response code="400">No se adjuntó archivo o el archivo está vacío.</response>
    /// <response code="422">El certificado no supera la validación (extensión, tamaño o contenido).</response>
    [HttpPost("sri-configuration/certificate")]
    [Authorize(Policy = $"perm:{ElectronicInvoicingPermissions.Configure}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(
        typeof(ApiResponse<SriCertificateUploadResultDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadCertificate(
        IFormFile? file,
        CancellationToken cancellationToken = default
    )
    {
        if (file is null || file.Length == 0)
            return this.ApiBadRequest("Debe adjuntar el archivo del certificado.");

        await using var upload = await BufferedFormFile.CreateAsync(file, cancellationToken);
        var result = await _mediator.Send(
            new UploadSriCertificateCommand(upload.Content),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Prueba una contraseña contra el certificado ya subido.
    /// </summary>
    /// <remarks>
    /// No persiste la contraseña. Sirve para dar retroalimentación mientras el usuario escribe, antes de guardar la
    /// configuración.
    /// </remarks>
    /// <param name="query">Contraseña a probar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Información del certificado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("sri-configuration/certificate/inspect")]
    [Authorize(Policy = $"perm:{ElectronicInvoicingPermissions.Configure}")]
    [ProducesResponseType(typeof(ApiResponse<SriCertificateInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InspectCertificate(
        [FromBody] InspectSriCertificateQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(query, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
