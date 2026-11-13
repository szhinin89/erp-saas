using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.GetBusinessPartnerRoles;
using ERP.Application.MasterData.UseCases.RevokeBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.UpdateRoleConfig;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Gestiona los roles de un BusinessPartner (Customer, Supplier, Carrier, etc.).
///
/// DISEÑO:
///   Un BP puede tener múltiples roles simultáneos. Los roles son extensibles sin cambios de esquema.
///   UPSERT: asignar un rol ya existente (aunque revocado) lo reactiva en lugar de crear duplicado.
/// </summary>
[ApiController]
[Route("api/v1/master/business-partners/{bpId:guid}/roles")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData — Business Partners")]
public sealed class BusinessPartnerRolesController : ControllerBase
{
    private readonly IMediator _mediator;

    public BusinessPartnerRolesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista los roles de un tercero.
    /// </summary>
    /// <remarks>
    /// Por defecto solo los activos; con <c>onlyActive</c> nulo o falso se incluyen los roles revocados
    /// (historial).
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="onlyActive">Si es <c>true</c> (por defecto) lista solo los registros activos; con <c>false</c> incluye los desactivados.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Roles del tercero.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<BusinessPartnerRoleDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetRoles(
        [FromRoute] Guid bpId,
        [FromQuery] bool? onlyActive = true,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetBusinessPartnerRolesQuery(bpId, onlyActive),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Asigna un rol a un tercero.
    /// </summary>
    /// <remarks>
    /// Responde 201. Semántica de alta o reactivación: si el rol no existe se crea; si existe revocado se reactiva;
    /// si ya está activo la solicitud se rechaza. Las configuraciones de proveedor, transportista y cliente son
    /// opcionales en la asignación.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="body">Tipo de rol y configuraciones opcionales por rol.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Rol asignado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(
        typeof(ApiResponse<BusinessPartnerRoleDto>),
        StatusCodes.Status201Created
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(
        [FromRoute] Guid bpId,
        [FromBody] AssignRoleRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var cmd = new AssignBusinessPartnerRoleCommand(
            bpId,
            body.RoleType,
            ToDto(body.SupplierConfig),
            ToDto(body.CarrierConfig),
            ToDto(body.CustomerConfig)
        );

        var result = await _mediator.Send(cmd, cancellationToken);
        return result.IsSuccess ? this.ApiCreated(result.Value!) : this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Revoca un rol de un tercero.
    /// </summary>
    /// <remarks>
    /// Baja lógica (el método DELETE no elimina el registro): el rol queda inactivo y se conserva en el historial.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="roleId">Id del rol del tercero.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Rol revocado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpDelete("{roleId:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RevokeRole(
        [FromRoute] Guid bpId,
        [FromRoute] Guid roleId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new RevokeBusinessPartnerRoleCommand(bpId, roleId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    // ── Actualización de configs ───────────────────────────────────────────────

    /// <summary>
    /// Actualiza la configuración de proveedor de un rol.
    /// </summary>
    /// <remarks>
    /// Defaults SRI del rol Supplier: códigos de retención, sustento y plazo.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="roleId">Id del rol del tercero.</param>
    /// <param name="body">Configuración de proveedor.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Configuración de proveedor actualizada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPatch("{roleId:guid}/supplier-config")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSupplierConfig(
        [FromRoute] Guid bpId,
        [FromRoute] Guid roleId,
        [FromBody] SupplierConfigRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new UpdateSupplierRoleConfigCommand(bpId, roleId, ToDto(body)!),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza la configuración de transportista de un rol.
    /// </summary>
    /// <remarks>
    /// Datos de transporte del rol Carrier: número de autorización y capacidad.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="roleId">Id del rol del tercero.</param>
    /// <param name="body">Configuración de transportista.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Configuración de transportista actualizada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPatch("{roleId:guid}/carrier-config")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCarrierConfig(
        [FromRoute] Guid bpId,
        [FromRoute] Guid roleId,
        [FromBody] CarrierConfigRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new UpdateCarrierRoleConfigCommand(bpId, roleId, ToDto(body)!),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza la configuración de cliente de un rol.
    /// </summary>
    /// <remarks>
    /// Categoría, segmento, zona, calificación y fidelización. Solo aplica a roles con tipo Customer.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="roleId">Id del rol del tercero.</param>
    /// <param name="body">Configuración de cliente.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Configuración de cliente actualizada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPatch("{roleId:guid}/customer-config")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCustomerConfig(
        [FromRoute] Guid bpId,
        [FromRoute] Guid roleId,
        [FromBody] CustomerConfigRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new UpdateCustomerRoleConfigCommand(bpId, roleId, ToDto(body)!),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza las notas internas de un rol.
    /// </summary>
    /// <remarks>
    /// Aplica a cualquier tipo de rol.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="roleId">Id del rol del tercero.</param>
    /// <param name="body">Notas internas.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Notas del rol actualizadas.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{roleId:guid}/notes")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateNotes(
        [FromRoute] Guid bpId,
        [FromRoute] Guid roleId,
        [FromBody] UpdateRoleNotesRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new UpdateRoleNotesCommand(bpId, roleId, body.Notes),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    // ── Request → datos primitivos del command (sin value objects de Domain) ──────

    private static SupplierRoleConfigDto? ToDto(SupplierConfigRequest? r) =>
        r is null
            ? null
            : new(
                r.DefaultTaxSupportCode,
                r.DefaultPaymentMethodCode,
                r.RefundProviderTypeCode,
                r.IsRetentionExempt,
                r.IsRequiredToKeepAccounting
            );

    private static CarrierRoleConfigDto? ToDto(CarrierConfigRequest? r) =>
        r is null ? null : new(r.TransportAuthorizationNumber, r.VehicleCapacityTons);

    private static CustomerRoleConfigDto? ToDto(CustomerConfigRequest? r) =>
        r is null
            ? null
            : new(
                r.CustomerCategory,
                r.CustomerSegment,
                r.SalesZone,
                r.CreditRating,
                r.LoyaltyTier,
                r.PreferredInvoiceFormat,
                r.CustomerClassification
            );
}
