using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Company.UseCases.PhysicalDocumentAuthorizations;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// PHYSICAL-FISCAL-F1 — autorizaciones de impresión del SRI por punto de emisión físico y tipo de
/// comprobante. Se administran en la pantalla de Numeración de documentos (misma numeración por
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
    /// <response code="200">Autorizaciones de impresión de la empresa.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentSequencesManage}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<PhysicalDocumentAuthorizationDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetPhysicalDocumentAuthorizationsQuery(),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Registra una autorización de impresión tal como consta en el documento del SRI.</summary>
    /// <remarks>
    /// Solo para puntos de emisión físicos activos de la empresa activa y tipos de comprobante
    /// habilitados. El número de autorización contiene solo dígitos, la caducidad no puede ser anterior
    /// a la fecha de autorización ni exceder un año desde ella, y el rango de secuenciales no puede
    /// superponerse con otra autorización activa del mismo punto y tipo. Solo registra el dato: no
    /// captura numeración ni modifica la secuencia.
    /// </remarks>
    /// <param name="command">Punto de emisión, tipo de comprobante, número y fechas de autorización y rango de secuenciales.</param>
    /// <response code="201">Autorización registrada.</response>
    /// <response code="404">El punto de emisión no existe en la empresa activa.</response>
    /// <response code="409">El rango se superpone con otra autorización activa del mismo punto y tipo.</response>
    /// <response code="422">Punto no físico o inactivo, tipo de comprobante no habilitado o datos inválidos.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentSequencesManage}")]
    [ProducesResponseType(
        typeof(ApiResponse<PhysicalDocumentAuthorizationDto>),
        StatusCodes.Status201Created
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePhysicalDocumentAuthorizationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>Deshabilita una autorización (soft delete).</summary>
    /// <remarks>
    /// Las autorizaciones son inmutables y nunca se borran: una renovación o corrección se registra
    /// como una autorización nueva y la anterior se deshabilita.
    /// </remarks>
    /// <param name="id">Id de la autorización.</param>
    /// <response code="200">Autorización deshabilitada (<c>data</c> es <c>true</c>).</response>
    /// <response code="404">La autorización no existe en la empresa activa.</response>
    /// <response code="409">La autorización ya estaba deshabilitada.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentSequencesManage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new DisablePhysicalDocumentAuthorizationCommand(id),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }
}
