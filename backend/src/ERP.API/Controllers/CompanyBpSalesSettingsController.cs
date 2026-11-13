using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.GetCompanyBpSalesSettings;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpSalesSettings;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/master/business-partners/{bpId:guid}/sales-settings")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData — Business Partners")]
public sealed class CompanyBpSalesSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyBpSalesSettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Obtiene la condición de pago por defecto de un cliente en la empresa activa.
    /// </summary>
    /// <remarks>
    /// Configuración de ventas del tercero por empresa.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Configuración de ventas del cliente.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBpSalesSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(
        [FromRoute] Guid bpId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetCompanyBpSalesSettingsQuery(bpId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Crea o actualiza la condición de pago por defecto de un cliente en la empresa activa.
    /// </summary>
    /// <remarks>
    /// Configuración de ventas del tercero por empresa.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="body">Id de la condición de pago por defecto.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Configuración de ventas guardada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPut]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersConfigureCompany}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBpSalesSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertSettings(
        [FromRoute] Guid bpId,
        [FromBody] UpsertSalesSettingsRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var cmd = new UpsertCompanyBpSalesSettingsCommand(bpId, body.PaymentTermId);
        var result = await _mediator.Send(cmd, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
