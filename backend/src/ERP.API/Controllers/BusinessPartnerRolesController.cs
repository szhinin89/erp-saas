using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Domain.Kernel.Permissions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.GetBusinessPartnerRoles;
using ERP.Application.MasterData.UseCases.RevokeBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.UpdateRoleConfig;
using ERP.Domain.MasterData.ValueObjects;
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
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BusinessPartnerRoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(
        [FromRoute] Guid  bpId,
        [FromQuery] bool? onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetBusinessPartnerRolesQuery(bpId, onlyActive), cancellationToken);
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
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerRoleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignRole(
        [FromRoute] Guid              bpId,
        [FromBody]  AssignRoleRequest body,
        CancellationToken cancellationToken = default)
    {
        var supplierConfig = body.SupplierConfig is not null
            ? SupplierRoleConfig.Create(
                body.SupplierConfig.PaymentTermId,
                body.SupplierConfig.DefaultTaxSupportCode,
                body.SupplierConfig.DefaultRetentionVatCode,
                body.SupplierConfig.DefaultRetentionIncomeCode,
                defaultPaymentMethodCode: body.SupplierConfig.DefaultPaymentMethodCode,
                refundProviderTypeCode: body.SupplierConfig.RefundProviderTypeCode,
                isRetentionExempt: body.SupplierConfig.IsRetentionExempt)
            : null;

        var carrierConfig = body.CarrierConfig is not null
            ? CarrierRoleConfig.Create(
                body.CarrierConfig.TransportAuthorizationNumber,
                body.CarrierConfig.VehicleCapacityTons)
            : null;

        var customerConfig = body.CustomerConfig is not null
            ? CustomerRoleConfig.Create(
                body.CustomerConfig.CustomerCategory,
                body.CustomerConfig.CustomerSegment,
                body.CustomerConfig.SalesZone,
                body.CustomerConfig.CreditRating,
                body.CustomerConfig.LoyaltyTier,
                body.CustomerConfig.PreferredInvoiceFormat,
                body.CustomerConfig.CustomerClassification)
            : null;

        SupplierClassificationConfig? classificationConfig = null;
        if (body.SupplierClassification is not null)
        {
            try
            {
                classificationConfig = SupplierClassificationConfig.Create(
                    body.SupplierClassification.SupplierCategory,
                    body.SupplierClassification.SupplierType,
                    body.SupplierClassification.SupplierRisk,
                    body.SupplierClassification.SupplierRating,
                    body.SupplierClassification.PrimaryGoodType,
                    body.SupplierClassification.SupplierSegment,
                    body.SupplierClassification.PaymentMethodPreference);
            }
            catch (ArgumentException ex) { return this.ApiBadRequest(ex.Message); }
        }

        var cmd = new AssignBusinessPartnerRoleCommand(
            bpId, body.RoleType, supplierConfig, carrierConfig, customerConfig, classificationConfig);

        var result = await _mediator.Send(cmd, cancellationToken);
        return result.IsSuccess
            ? this.ApiCreated(result.Value!)
            : this.ToOkOrBadRequest(result);
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
        CancellationToken cancellationToken = default)
    {
        _ = bpId; // el filtro de tenant garantiza que roleId pertenece al tenant
        var result = await _mediator.Send(new RevokeBusinessPartnerRoleCommand(roleId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    // ── Actualización de configs ───────────────────────────────────────────────

    /// <summary>Actualiza defaults SRI del rol Supplier (códigos de retención, sustento, plazo).</summary>
    [HttpPatch("{roleId:guid}/supplier-config")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateSupplierConfig(
        [FromRoute] Guid                 bpId,
        [FromRoute] Guid                 roleId,
        [FromBody]  SupplierConfigRequest body,
        CancellationToken cancellationToken = default)
    {
        _ = bpId;
        SupplierRoleConfig config;
        try
        {
            config = SupplierRoleConfig.Create(
                body.PaymentTermId,
                body.DefaultTaxSupportCode,
                body.DefaultRetentionVatCode,
                body.DefaultRetentionIncomeCode,
                defaultPaymentMethodCode: body.DefaultPaymentMethodCode,
                refundProviderTypeCode: body.RefundProviderTypeCode,
                isRetentionExempt: body.IsRetentionExempt);
        }
        catch (ArgumentException ex) { return this.ApiBadRequest(ex.Message); }

        var result = await _mediator.Send(new UpdateSupplierRoleConfigCommand(roleId, config), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza la clasificación estratégica del rol Supplier.
    /// Categoría, tipo, riesgo, rating, tipo de bien, segmento, preferencia de pago operativo.
    /// Solo aplica a roles con RoleType = Supplier.
    /// </summary>
    [HttpPatch("{roleId:guid}/supplier-classification")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateSupplierClassification(
        [FromRoute] Guid                          bpId,
        [FromRoute] Guid                          roleId,
        [FromBody]  SupplierClassificationRequest body,
        CancellationToken cancellationToken = default)
    {
        _ = bpId;
        SupplierClassificationConfig config;
        try
        {
            config = SupplierClassificationConfig.Create(
                body.SupplierCategory,
                body.SupplierType,
                body.SupplierRisk,
                body.SupplierRating,
                body.PrimaryGoodType,
                body.SupplierSegment,
                body.PaymentMethodPreference);
        }
        catch (ArgumentException ex) { return this.ApiBadRequest(ex.Message); }

        var result = await _mediator.Send(new UpdateSupplierClassificationConfigCommand(roleId, config), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza datos de transporte del rol Carrier (número autorización, capacidad).</summary>
    [HttpPatch("{roleId:guid}/carrier-config")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCarrierConfig(
        [FromRoute] Guid                bpId,
        [FromRoute] Guid                roleId,
        [FromBody]  CarrierConfigRequest body,
        CancellationToken cancellationToken = default)
    {
        _ = bpId;
        CarrierRoleConfig config;
        try
        {
            config = CarrierRoleConfig.Create(
                body.TransportAuthorizationNumber,
                body.VehicleCapacityTons);
        }
        catch (ArgumentException ex) { return this.ApiBadRequest(ex.Message); }

        var result = await _mediator.Send(new UpdateCarrierRoleConfigCommand(roleId, config), cancellationToken);
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
        [FromRoute] Guid                 bpId,
        [FromRoute] Guid                 roleId,
        [FromBody]  CustomerConfigRequest body,
        CancellationToken cancellationToken = default)
    {
        _ = bpId;
        CustomerRoleConfig config;
        try
        {
            config = CustomerRoleConfig.Create(
                body.CustomerCategory,
                body.CustomerSegment,
                body.SalesZone,
                body.CreditRating,
                body.LoyaltyTier,
                body.PreferredInvoiceFormat,
                body.CustomerClassification);
        }
        catch (ArgumentException ex) { return this.ApiBadRequest(ex.Message); }

        var result = await _mediator.Send(new UpdateCustomerRoleConfigCommand(roleId, config), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza las notas internas de cualquier rol.</summary>
    [HttpPatch("{roleId:guid}/notes")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateNotes(
        [FromRoute] Guid                  bpId,
        [FromRoute] Guid                  roleId,
        [FromBody]  UpdateRoleNotesRequest body,
        CancellationToken cancellationToken = default)
    {
        _ = bpId;
        var result = await _mediator.Send(new UpdateRoleNotesCommand(roleId, body.Notes), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
