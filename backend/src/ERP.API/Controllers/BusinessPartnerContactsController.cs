using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.BpContacts;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Gestiona los contactos de un BusinessPartner.
///
/// SCOPE: tenant-scoped — los contactos son datos maestros del tercero,
/// compartidos entre todas las empresas del tenant. Ver ADR-BP-02 (Fase 4).
///
/// CASOS DE USO TÍPICOS:
///   - Registrar representante legal: Role=Legal (9)
///   - Registrar contacto de facturación: Role=Billing (6)
///   - Registrar asesor comercial: Role=Commercial (1)
///
/// INVARIANTES:
///   - Solo un contacto puede ser primario (IsPrimary=true) por BP.
///   - Para ContactRole=Other, OtherDescription es obligatorio.
/// </summary>
[ApiController]
[Route("api/v1/master/business-partners/{bpId:guid}/contacts")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData — Business Partners")]
public sealed class BusinessPartnerContactsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BusinessPartnerContactsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista los contactos de un tercero.
    /// </summary>
    /// <remarks>
    /// Por defecto solo los activos.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="onlyActive">Si es <c>true</c> (por defecto) lista solo los registros activos; con <c>false</c> incluye los desactivados.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Contactos del tercero.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<BpContactDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetContacts(
        [FromRoute] Guid bpId,
        [FromQuery] bool? onlyActive = true,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetBpContactsQuery(bpId, onlyActive),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Obtiene un contacto de un tercero.
    /// </summary>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="contactId">Id del contacto.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Contacto.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{contactId:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(typeof(ApiResponse<BpContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContact(
        [FromRoute] Guid bpId,
        [FromRoute] Guid contactId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetBpContactByIdQuery(bpId, contactId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Registra un contacto de un tercero.
    /// </summary>
    /// <remarks>
    /// Responde 201. Con <c>isPrimary</c> en verdadero, el contacto primario anterior pierde la marca.
    /// <c>locationId</c> es opcional y, si se indica, debe pertenecer al mismo tercero.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="body">Datos del contacto.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Contacto creado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BpContactDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateContact(
        [FromRoute] Guid bpId,
        [FromBody] CreateContactRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var cmd = new CreateBpContactCommand(
            bpId,
            body.FirstName,
            body.Role,
            body.LocationId,
            body.LastName,
            body.Position,
            body.Phone,
            body.Mobile,
            body.Email,
            body.Notes,
            body.IsPrimary,
            body.OtherDescription
        );

        var result = await _mediator.Send(cmd, cancellationToken);
        return result.IsSuccess ? this.ApiCreated(result.Value!) : this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza un contacto de un tercero.
    /// </summary>
    /// <remarks>
    /// No cambia la marca de primario; para eso se usa <c>set-primary</c>.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="contactId">Id del contacto.</param>
    /// <param name="body">Datos del contacto.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Contacto actualizado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPut("{contactId:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BpContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateContact(
        [FromRoute] Guid bpId,
        [FromRoute] Guid contactId,
        [FromBody] UpdateContactRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var cmd = new UpdateBpContactCommand(
            bpId,
            contactId,
            body.FirstName,
            body.Role,
            body.LocationId,
            body.LastName,
            body.Position,
            body.Phone,
            body.Mobile,
            body.Email,
            body.Notes,
            body.OtherDescription
        );

        var result = await _mediator.Send(cmd, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Marca un contacto como primario del tercero.
    /// </summary>
    /// <remarks>
    /// El contacto primario anterior pierde la marca.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="contactId">Id del contacto.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Contacto marcado como primario.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPatch("{contactId:guid}/set-primary")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPrimary(
        [FromRoute] Guid bpId,
        [FromRoute] Guid contactId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new SetPrimaryBpContactCommand(bpId, contactId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Reactiva un contacto desactivado.
    /// </summary>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="contactId">Id del contacto.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Contacto activado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{contactId:guid}/activate")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid bpId,
        [FromRoute] Guid contactId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new ActivateBpContactCommand(bpId, contactId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Desactiva un contacto de un tercero.
    /// </summary>
    /// <remarks>
    /// Baja lógica (el método DELETE no elimina el registro).
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="contactId">Id del contacto.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Contacto desactivado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpDelete("{contactId:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid bpId,
        [FromRoute] Guid contactId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new DeactivateBpContactCommand(bpId, contactId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }
}
