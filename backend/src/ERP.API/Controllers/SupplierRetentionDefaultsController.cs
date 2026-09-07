using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.AddSupplierRetentionDefault;
using ERP.Application.MasterData.UseCases.GetSupplierRetentionDefaults;
using ERP.Application.MasterData.UseCases.SetSupplierRetentionDefaultState;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01 — lista dinámica de retenciones predeterminadas de un
/// PROVEEDOR en la empresa activa. Reemplaza los antiguos campos fijos
/// SupplierRoleConfig.DefaultRetentionVatCode/DefaultRetentionIncomeCode (un único código por
/// impuesto, tenant-wide) por N filas company-scoped, cada una con FK real a SriRetentionCode.
///
/// SCOPE: company-scoped — mismo criterio que CompanyBpPurchaseSettingsController (ADR-033):
/// Company A puede tener retenciones distintas de Company B para el mismo proveedor.
///
/// Permisos: reutiliza los mismos de configuración de proveedor por empresa
/// (BusinessPartnersView / BusinessPartnersConfigureCompany) — no se crea un permiso nuevo.
/// </summary>
[ApiController]
[Route("api/v1/master/business-partners/{bpId:guid}/retention-defaults")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData — Business Partners")]
public sealed class SupplierRetentionDefaultsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SupplierRetentionDefaultsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista todas las retenciones predeterminadas (activas e inactivas) del proveedor en la empresa activa.</summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<SupplierRetentionDefaultDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetRetentionDefaults(
        [FromRoute] Guid bpId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetSupplierRetentionDefaultsQuery(bpId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Agrega una retención predeterminada al proveedor en la empresa activa.</summary>
    [HttpPost]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersConfigureCompany}")]
    [ProducesResponseType(
        typeof(ApiResponse<SupplierRetentionDefaultDto>),
        StatusCodes.Status201Created
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddRetentionDefault(
        [FromRoute] Guid bpId,
        [FromBody] AddRetentionDefaultRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var cmd = new AddSupplierRetentionDefaultCommand(bpId, body.SriRetentionCodeId);
        var result = await _mediator.Send(cmd, cancellationToken);
        return result.IsSuccess ? this.ApiCreated(result.Value!) : this.ToOkOrBadRequest(result);
    }

    /// <summary>Activa/desactiva (soft-disable) y/o reordena una retención predeterminada existente.</summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersConfigureCompany}")]
    [ProducesResponseType(
        typeof(ApiResponse<SupplierRetentionDefaultDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetRetentionDefaultState(
        [FromRoute] Guid bpId,
        [FromRoute] Guid id,
        [FromBody] SetRetentionDefaultStateRequest body,
        CancellationToken cancellationToken = default
    )
    {
        _ = bpId; // el filtro de tenant+company garantiza que id pertenece al bp/company activos
        var cmd = new SetSupplierRetentionDefaultStateCommand(id, body.IsActive, body.DisplayOrder);
        var result = await _mediator.Send(cmd, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
