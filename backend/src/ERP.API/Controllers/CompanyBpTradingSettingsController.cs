using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Domain.Kernel.Permissions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.BlockBusinessPartner;
using ERP.Application.MasterData.UseCases.GetCompanyBpTradingSettings;
using ERP.Application.MasterData.UseCases.UnblockBusinessPartner;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpTradingSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Gestiona la configuración comercial de un BP en la empresa activa.
///
/// SCOPE: company-scoped — la configuración es específica por empresa.
///   Company A puede tener crédito $50.000 con el proveedor X.
///   Company B del mismo tenant puede tenerlo bloqueado.
///
/// OPERACIONES:
///   - GET: obtiene la configuración actual (404 si no existe)
///   - PUT: crea o actualiza la configuración (upsert)
///   - PATCH /block: bloquea operativamente al BP (requiere motivo)
///   - PATCH /unblock: desbloquea el BP
///
/// BLOQUEO: un BP bloqueado no puede emitir ni recibir documentos en esta empresa.
/// El motivo, fecha y responsable quedan registrados en el sistema.
/// </summary>
[ApiController]
[Route("api/v1/master/business-partners/{bpId:guid}/trading-settings")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData — Business Partners")]
public sealed class CompanyBpTradingSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyBpTradingSettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Obtiene la configuración comercial del BP en la empresa activa.
    /// Si no existe configuración, retorna DTO con valores por defecto (hasCustomConfiguration=false).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBpTradingSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(
        [FromRoute] Guid bpId, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCompanyBpTradingSettingsQuery(bpId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Crea o actualiza la configuración comercial del BP en la empresa activa.
    /// Si no existe → crea. Si ya existe → actualiza (merge semántico).
    /// El bloqueo NO se modifica con este endpoint — usar /block o /unblock.
    /// </summary>
    [HttpPut]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersConfigureCompany}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBpTradingSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertSettings(
        [FromRoute] Guid                       bpId,
        [FromBody]  UpsertTradingSettingsRequest body,
        CancellationToken cancellationToken = default)
    {
        var cmd = new UpsertCompanyBpTradingSettingsCommand(
            bpId, body.CreditLimit, body.PaymentDays, body.CreditCurrencyCode);
        var result = await _mediator.Send(cmd, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Bloquea operativamente al BP en la empresa activa.
    /// Un BP bloqueado no puede emitir facturas, órdenes de compra ni ningún otro documento.
    /// Se registra quién bloqueó, cuándo y por qué. Motivo obligatorio (máx 500 chars).
    /// La configuración comercial debe existir previamente (crear con PUT).
    /// </summary>
    [HttpPatch("block")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersConfigureCompany}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Block(
        [FromRoute] Guid         bpId,
        [FromBody]  BlockRequest body,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new BlockBusinessPartnerCommand(bpId, body.Reason), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Desbloquea al BP en la empresa activa.
    /// Limpia el motivo, fecha y responsable del bloqueo anterior.
    /// </summary>
    [HttpPatch("unblock")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersConfigureCompany}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Unblock(
        [FromRoute] Guid bpId, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new UnblockBusinessPartnerCommand(bpId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
