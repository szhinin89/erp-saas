using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.BpContacts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Gestiona los contactos de un BusinessPartner.
///
/// SCOPE: subscriber-scoped — los contactos son datos maestros del tercero,
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
[Route("api/master/business-partners/{bpId:guid}/contacts")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData — Business Partners")]
public sealed class BusinessPartnerContactsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BusinessPartnerContactsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista los contactos del BP. Por defecto solo activos.</summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.View}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BpContactDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContacts(
        [FromRoute] Guid  bpId,
        [FromQuery] bool? onlyActive = true,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBpContactsQuery(bpId, onlyActive), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Obtiene un contacto por Id.</summary>
    [HttpGet("{contactId:guid}")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.View}")]
    [ProducesResponseType(typeof(ApiResponse<BpContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContact(
        [FromRoute] Guid bpId,
        [FromRoute] Guid contactId,
        CancellationToken ct = default)
    {
        _ = bpId;
        var result = await _mediator.Send(new GetBpContactByIdQuery(contactId), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Registra un nuevo contacto del BP.
    /// Si IsPrimary=true, el contacto primario anterior pierde el flag automáticamente.
    /// LocationId opcional — si se especifica, debe pertenecer al mismo BP.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<BpContactDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateContact(
        [FromRoute] Guid                 bpId,
        [FromBody]  CreateContactRequest body,
        CancellationToken ct = default)
    {
        var cmd = new CreateBpContactCommand(
            bpId, body.FirstName, body.Role,
            body.LocationId, body.LastName, body.Position,
            body.Phone, body.Mobile, body.Email,
            body.Notes, body.IsPrimary, body.OtherDescription);

        var result = await _mediator.Send(cmd, ct);
        return result.IsSuccess
            ? this.ApiCreated(result.Value!, "Contacto creado.")
            : this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza los datos del contacto.</summary>
    [HttpPut("{contactId:guid}")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<BpContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateContact(
        [FromRoute] Guid                 bpId,
        [FromRoute] Guid                 contactId,
        [FromBody]  UpdateContactRequest body,
        CancellationToken ct = default)
    {
        _ = bpId;
        var cmd = new UpdateBpContactCommand(
            contactId, body.FirstName, body.Role,
            body.LocationId, body.LastName, body.Position,
            body.Phone, body.Mobile, body.Email,
            body.Notes, body.OtherDescription);

        var result = await _mediator.Send(cmd, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Marca este contacto como primario del BP.
    /// El contacto primario anterior pierde el flag automáticamente.
    /// </summary>
    [HttpPatch("{contactId:guid}/set-primary")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetPrimary(
        [FromRoute] Guid bpId,
        [FromRoute] Guid contactId,
        CancellationToken ct = default)
    {
        _ = bpId;
        var result = await _mediator.Send(new SetPrimaryBpContactCommand(contactId), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Reactiva un contacto previamente desactivado.</summary>
    [HttpPatch("{contactId:guid}/activate")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid bpId,
        [FromRoute] Guid contactId,
        CancellationToken ct = default)
    {
        _ = bpId;
        var result = await _mediator.Send(new ActivateBpContactCommand(contactId), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Desactiva el contacto (soft delete).</summary>
    [HttpDelete("{contactId:guid}")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid bpId,
        [FromRoute] Guid contactId,
        CancellationToken ct = default)
    {
        _ = bpId;
        var result = await _mediator.Send(new DeactivateBpContactCommand(contactId), ct);
        return this.ToOkOrBadRequest(result);
    }
}
