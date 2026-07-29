using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common.Models;
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
    "/settings/electronic-invoicing",
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
    /// Estado liviano de facturación electrónica para UI transversal (banner de ambiente) —
    /// visible para cualquier usuario autenticado de la empresa, sin requerir el permiso de
    /// Configuración SRI: no expone datos sensibles, solo ambiente/estado de emisión.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(
        typeof(ApiResponse<ElectronicInvoicingStatusDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetElectronicInvoicingStatusQuery(),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

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
    /// Diagnostica la configuración SRI actual (certificado, contraseña, vigencia, conectividad
    /// del webservice) — solo lectura, nunca modifica la configuración guardada.
    /// </summary>
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
    /// Sube (o reemplaza) el certificado digital .p12/.pfx de la empresa. El backend administra
    /// el almacenamiento — el cliente nunca envía ni conoce una ruta de servidor.
    /// </summary>
    [HttpPost("sri-configuration/certificate")]
    [Authorize(Policy = $"perm:{ElectronicInvoicingPermissions.Configure}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(
        typeof(ApiResponse<SriCertificateUploadResultDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadCertificate(
        IFormFile? file,
        CancellationToken cancellationToken = default
    )
    {
        if (file is null || file.Length == 0)
            return this.ApiBadRequest("Debe adjuntar el archivo del certificado.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;

        var content = new MediaUploadContent(stream, file.FileName, file.ContentType, file.Length);
        var result = await _mediator.Send(
            new UploadSriCertificateCommand(content),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Prueba una contraseña contra el certificado ya subido, sin persistirla — usado para dar
    /// feedback en vivo mientras el usuario escribe, antes de pulsar "Guardar".
    /// </summary>
    [HttpPost("sri-configuration/certificate/inspect")]
    [Authorize(Policy = $"perm:{ElectronicInvoicingPermissions.Configure}")]
    [ProducesResponseType(typeof(ApiResponse<SriCertificateInfoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> InspectCertificate(
        [FromBody] InspectSriCertificateQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(query, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
