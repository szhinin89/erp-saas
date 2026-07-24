using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Domain.Kernel.Permissions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.ActivateBusinessPartner;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.DeactivateBusinessPartner;
using ERP.Application.MasterData.UseCases.GetBusinessPartner;
using ERP.Application.MasterData.UseCases.SearchBusinessPartners;
using ERP.Application.MasterData.UseCases.UpdateBusinessPartner;
using ERP.Domain.MasterData.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Gestiona la identidad fiscal de los Business Partners (terceros).
///
/// FLUJO TÍPICO POST-CREACIÓN:
///   1. POST /business-partners → crea identidad
///   2. POST /business-partners/{id}/roles → asigna rol Customer/Supplier
///   3. POST /business-partners/{id}/contacts → registra representante legal, teléfonos
///   4. POST /business-partners/{id}/locations → registra direcciones
///   5. PUT  /business-partners/{id}/trading-settings → configura crédito por empresa
/// </summary>
[ApiController]
[Route("api/v1/master/business-partners")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData — Business Partners")]
public sealed class BusinessPartnersController : ControllerBase
{
    private readonly IMediator _mediator;

    public BusinessPartnersController(IMediator mediator) => _mediator = mediator;

    // ── Búsqueda y consulta ───────────────────────────────────────────────────

    /// <summary>
    /// Busca Business Partners con filtros opcionales. Paginado.
    /// Filtro por roles: usa el parámetro 'roles' (extensible) en lugar de isCustomer/isSupplier.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BusinessPartnerSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string?     q        = null,
        [FromQuery] bool?       isActive = true,
        [FromQuery] RoleType[]? roles    = null,
        [FromQuery] int         skip     = 0,
        [FromQuery] int         take     = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new SearchBusinessPartnersQuery(q, isActive, roles, skip, take), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Obtiene el line completo de un BP incluyendo todos sus roles con configs.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetBusinessPartnerQuery(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    // ── Creación ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Crea la identidad fiscal de un nuevo tercero en el tenant.
    /// Solo registra la identidad — asignar roles y contactos en pasos posteriores.
    /// Validación algoritmo RUC/CI ecuatoriano aplicada automáticamente.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersCreate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerSummaryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBusinessPartnerRequest body, CancellationToken cancellationToken = default)
    {
        var cmd = new CreateBusinessPartnerCommand(
            body.IdentificationType,
            body.IdentificationNumber,
            body.PersonType,
            body.LegalName,
            body.TradeName,
            body.CountryCode);

        var result = await _mediator.Send(cmd, cancellationToken);
        return result.IsSuccess
            ? this.ApiCreated(result.Value!)
            : this.ToOkOrBadRequest(result);
    }

    // ── Actualizaciones ───────────────────────────────────────────────────────

    /// <summary>Actualiza nombre legal, nombre comercial, tipo de persona y país.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProfile(
        [FromRoute] Guid id,
        [FromBody]  UpdateBusinessPartnerRequest body,
        CancellationToken cancellationToken = default)
    {
        var cmd = new UpdateBusinessPartnerCommand(
            id, body.LegalName, body.PersonType, body.TradeName, body.CountryCode);
        var result = await _mediator.Send(cmd, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Cambia la identificación fiscal. Operación de alto impacto — genera evento de auditoría.
    /// Valida algoritmo RUC/CI ecuatoriano. Devuelve 409 si el nuevo número ya existe.
    /// </summary>
    [HttpPatch("{id:guid}/identification")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateIdentification(
        [FromRoute] Guid id,
        [FromBody]  UpdateIdentificationRequest body,
        CancellationToken cancellationToken = default)
    {
        var cmd = new UpdateBusinessPartnerIdentificationCommand(
            id, body.IdentificationType, body.IdentificationNumber);
        var result = await _mediator.Send(cmd, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    // ── Ciclo de vida ─────────────────────────────────────────────────────────

    /// <summary>Reactiva un BP previamente desactivado.</summary>
    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ActivateBusinessPartnerCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Desactiva el BP (soft delete). No elimina ni sus roles ni sus ubicaciones.
    /// No se puede desactivar un BP con roles activos y documentos en curso (ver ADR-BP-14).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersDisable}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Deactivate([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DeactivateBusinessPartnerCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
