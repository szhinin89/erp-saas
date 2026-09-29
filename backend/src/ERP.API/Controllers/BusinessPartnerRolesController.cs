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
    /// Lista todos los roles del BP. Por defecto solo activos.
    /// Usar onlyActive=null para incluir roles revocados (historial).
    /// </summary>
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
    /// Asigna un rol al BP. Semántica UPSERT:
    ///   - Si el rol no existe → crea nuevo
    ///   - Si el rol existe revocado → lo reactiva
    ///   - Si el rol ya está activo → devuelve 422
    /// Las configs (SupplierConfig, CarrierConfig) son opcionales en la asignación.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(
        typeof(ApiResponse<BusinessPartnerRoleDto>),
        StatusCodes.Status201Created
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
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
    /// Revoca el rol. El registro histórico se conserva (is_active = false).
    /// ATENCIÓN: el sistema verificará documentos activos en futuras versiones (ADR-BP-14).
    /// </summary>
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

    /// <summary>Actualiza defaults SRI del rol Supplier (códigos de retención, sustento, plazo).</summary>
    [HttpPatch("{roleId:guid}/supplier-config")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
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

    /// <summary>Actualiza datos de transporte del rol Carrier (número autorización, capacidad).</summary>
    [HttpPatch("{roleId:guid}/carrier-config")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
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
    /// Actualiza la configuración Customer: categoría, segmento, zona, rating, fidelización.
    /// CRM-ready — soporta segmentación comercial, scoring y automatización documental.
    /// Solo aplica a roles con RoleType = Customer.
    /// </summary>
    [HttpPatch("{roleId:guid}/customer-config")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
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

    /// <summary>Actualiza las notas internas de cualquier rol.</summary>
    [HttpPatch("{roleId:guid}/notes")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
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
            : new(r.DefaultTaxSupportCode, r.DefaultPaymentMethodCode, r.RefundProviderTypeCode, r.IsRetentionExempt, r.IsRequiredToKeepAccounting);

    private static CarrierRoleConfigDto? ToDto(CarrierConfigRequest? r) =>
        r is null ? null : new(r.TransportAuthorizationNumber, r.VehicleCapacityTons);

    private static CustomerRoleConfigDto? ToDto(CustomerConfigRequest? r) =>
        r is null
            ? null
            : new(r.CustomerCategory, r.CustomerSegment, r.SalesZone, r.CreditRating, r.LoyaltyTier, r.PreferredInvoiceFormat, r.CustomerClassification);
}
