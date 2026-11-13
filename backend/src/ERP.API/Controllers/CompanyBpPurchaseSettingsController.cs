using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.GetCompanyBpPurchaseSettings;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpPurchaseSettings;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// ADR-033, Fase 3d — default de condición de pago de un PROVEEDOR en la empresa activa.
///
/// SCOPE: company-scoped — el default es específico por empresa.
///   Company A puede tener Crédito 30 días con el proveedor X.
///   Company B del mismo tenant puede tener Contado con el mismo proveedor.
///
/// Distinto de SupplierRoleConfig.PaymentTermId (tenant-wide, config SRI general del proveedor,
/// conservado solo por compatibilidad — ver BusinessPartnerRolesController) y de
/// CompanyBpSalesSettingsController (equivalente para CLIENTE, condición predeterminada de ventas).
///
/// OPERACIONES:
///   - GET: obtiene el default actual (valores por defecto con hasCustomConfiguration=false si
///     no existe fila — nunca 404).
///   - PUT: crea o actualiza (upsert). PaymentTermId null limpia el default.
/// </summary>
[ApiController]
[Route("api/v1/master/business-partners/{bpId:guid}/purchase-settings")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData — Business Partners")]
public sealed class CompanyBpPurchaseSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyBpPurchaseSettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Obtiene la condición de pago por defecto de un proveedor en la empresa activa.
    /// </summary>
    /// <remarks>
    /// Si no existe configuración devuelve valores por defecto con <c>hasCustomConfiguration</c> en falso.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Configuración de compras del proveedor.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(
        typeof(ApiResponse<CompanyBpPurchaseSettingsDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetSettings(
        [FromRoute] Guid bpId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetCompanyBpPurchaseSettingsQuery(bpId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Crea o actualiza la condición de pago por defecto de un proveedor en la empresa activa.
    /// </summary>
    /// <remarks>
    /// Con <c>paymentTermId</c> nulo se limpia el valor por defecto: Compras y Gastos exigirán entonces una
    /// selección explícita.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="body">Id de la condición de pago por defecto, o nulo para limpiarla.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Configuración de compras guardada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPut]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersConfigureCompany}")]
    [ProducesResponseType(
        typeof(ApiResponse<CompanyBpPurchaseSettingsDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertSettings(
        [FromRoute] Guid bpId,
        [FromBody] UpsertPurchaseSettingsRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var cmd = new UpsertCompanyBpPurchaseSettingsCommand(bpId, body.PaymentTermId);
        var result = await _mediator.Send(cmd, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
