using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.ActivateBusinessPartner;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.DeactivateBusinessPartner;
using ERP.Application.MasterData.UseCases.GetBusinessPartner;
using ERP.Application.MasterData.UseCases.SearchBusinessPartners;
using ERP.Application.MasterData.UseCases.UpdateBusinessPartner;
using ERP.Domain.Kernel.Permissions;
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
///   5. PUT  /business-partners/{id}/sales-settings → configura condición de ventas por empresa
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
    /// Busca terceros (Business Partners) con filtros y paginación.
    /// </summary>
    /// <remarks>
    /// Resultado paginado por <c>skip</c> y <c>take</c> (50 por defecto). El filtro <c>roles</c> acepta uno o
    /// varios tipos de rol (por ejemplo cliente o proveedor) y es la forma extensible de filtrar por rol.
    /// </remarks>
    /// <param name="q">Texto libre de búsqueda por identificación o nombre.</param>
    /// <param name="isActive">Filtra por estado activo (<c>true</c>) o inactivo (<c>false</c>); omitido, no filtra.</param>
    /// <param name="roles">Tipos de rol por los que se filtra; omitido, no filtra por rol.</param>
    /// <param name="skip">Cantidad de registros a omitir (0 por defecto).</param>
    /// <param name="take">Cantidad máxima de registros a devolver (50 por defecto).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Página de terceros.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResult<BusinessPartnerSummaryDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> Search(
        [FromQuery] string? q = null,
        [FromQuery] bool? isActive = true,
        [FromQuery] RoleType[]? roles = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new SearchBusinessPartnersQuery(q, isActive, roles, skip, take),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Obtiene el detalle de un tercero con todos sus roles y configuraciones.
    /// </summary>
    /// <param name="id">Id de el tercero (Business Partner).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Detalle del tercero.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(new GetBusinessPartnerQuery(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    // ── Creación ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Crea la identidad fiscal de un tercero.
    /// </summary>
    /// <remarks>
    /// Responde 201. Solo registra la identidad (tipo y número de identificación, tipo de persona, nombre legal y
    /// comercial, país); los roles y contactos se asignan en pasos posteriores. Aplica la validación del algoritmo
    /// de RUC y cédula ecuatorianos.
    /// </remarks>
    /// <param name="body">Identificación, tipo de entidad legal, nombre legal, nombre comercial y país.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Tercero creado.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersCreate}")]
    [ProducesResponseType(
        typeof(ApiResponse<BusinessPartnerSummaryDto>),
        StatusCodes.Status201Created
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBusinessPartnerRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var cmd = new CreateBusinessPartnerCommand(
            body.IdentificationType,
            body.IdentificationNumber,
            body.LegalEntityTypeCode,
            body.LegalName,
            body.TradeName,
            body.CountryCode
        );

        var result = await _mediator.Send(cmd, cancellationToken);
        return result.IsSuccess ? this.ApiCreated(result.Value!) : this.ToOkOrBadRequest(result);
    }

    // ── Actualizaciones ───────────────────────────────────────────────────────

    /// <summary>
    /// Actualiza el perfil de un tercero.
    /// </summary>
    /// <remarks>
    /// Actualiza nombre legal, nombre comercial, tipo de entidad legal y país. La identificación fiscal se cambia
    /// con <c>PATCH {id}/identification</c>.
    /// </remarks>
    /// <param name="id">Id de el tercero (Business Partner).</param>
    /// <param name="body">Nombre legal, nombre comercial, tipo de entidad legal y país.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Perfil actualizado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProfile(
        [FromRoute] Guid id,
        [FromBody] UpdateBusinessPartnerRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var cmd = new UpdateBusinessPartnerCommand(
            id,
            body.LegalName,
            body.LegalEntityTypeCode,
            body.TradeName,
            body.CountryCode
        );
        var result = await _mediator.Send(cmd, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Cambia la identificación fiscal de un tercero.
    /// </summary>
    /// <remarks>
    /// Operación de alto impacto: genera un evento de auditoría y valida el algoritmo de RUC y cédula ecuatorianos.
    /// Si el nuevo número ya existe responde 409.
    /// </remarks>
    /// <param name="id">Id de el tercero (Business Partner).</param>
    /// <param name="body">Nuevo tipo y número de identificación.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Identificación actualizada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPatch("{id:guid}/identification")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateIdentification(
        [FromRoute] Guid id,
        [FromBody] UpdateIdentificationRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var cmd = new UpdateBusinessPartnerIdentificationCommand(
            id,
            body.IdentificationType,
            body.IdentificationNumber
        );
        var result = await _mediator.Send(cmd, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    // ── Ciclo de vida ─────────────────────────────────────────────────────────

    /// <summary>
    /// Reactiva un tercero desactivado.
    /// </summary>
    /// <param name="id">Id de el tercero (Business Partner).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Tercero activado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new ActivateBusinessPartnerCommand(id),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Desactiva un tercero.
    /// </summary>
    /// <remarks>
    /// Baja lógica (el método DELETE no elimina el registro): conserva los roles y las ubicaciones del tercero.
    /// </remarks>
    /// <param name="id">Id de el tercero (Business Partner).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Tercero desactivado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersDisable}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new DeactivateBusinessPartnerCommand(id),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }
}
