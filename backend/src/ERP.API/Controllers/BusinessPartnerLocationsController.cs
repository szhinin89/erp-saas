using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.BpLocations;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Gestiona las ubicaciones físicas de un BusinessPartner.
///
/// SCOPE: tenant-scoped — las ubicaciones son datos maestros del tercero,
/// compartidos entre todas las empresas del tenant. Ver ADR-BP-02 (Fase 4).
///
/// INVARIANTES:
///   - Solo una ubicación puede ser primaria (IsPrimary=true) por BP.
///   - No se puede desactivar la ubicación primaria sin asignar otra primera.
///   - No se puede desactivar una ubicación con contactos activos referenciándola.
/// </summary>
[ApiController]
[Route("api/v1/master/business-partners/{bpId:guid}/locations")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData — Business Partners")]
public sealed class BusinessPartnerLocationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BusinessPartnerLocationsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista las ubicaciones de un tercero.
    /// </summary>
    /// <remarks>
    /// Por defecto solo las activas.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="onlyActive">Si es <c>true</c> (por defecto) lista solo los registros activos; con <c>false</c> incluye los desactivados.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ubicaciones del tercero.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<BpLocationDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetLocations(
        [FromRoute] Guid bpId,
        [FromQuery] bool? onlyActive = true,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetBpLocationsQuery(bpId, onlyActive),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Obtiene una ubicación de un tercero.
    /// </summary>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="locationId">Id de la ubicación.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ubicación.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{locationId:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(typeof(ApiResponse<BpLocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLocation(
        [FromRoute] Guid bpId,
        [FromRoute] Guid locationId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetBpLocationByIdQuery(bpId, locationId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Crea una ubicación de un tercero.
    /// </summary>
    /// <remarks>
    /// Responde 201. Con <c>isPrimary</c> en verdadero, la ubicación primaria anterior pierde la marca. Para el
    /// tipo <c>Other</c>, <c>otherDescription</c> es obligatorio. <c>purpose</c> es una máscara de bits combinable:
    /// facturación = 1, entrega = 2, fiscal = 4, correspondencia = 8.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="body">Datos de la ubicación.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Ubicación creada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BpLocationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateLocation(
        [FromRoute] Guid bpId,
        [FromBody] CreateLocationRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var cmd = new CreateBpLocationCommand(
            bpId,
            body.Name,
            body.Type,
            body.Purpose,
            body.AddressLine,
            body.ProvinceCode,
            body.CantonCode,
            body.ParishCode,
            body.Phone,
            body.Email,
            body.IsPrimary,
            body.OtherDescription
        );

        var result = await _mediator.Send(cmd, cancellationToken);
        return result.IsSuccess ? this.ApiCreated(result.Value!) : this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza una ubicación de un tercero.
    /// </summary>
    /// <remarks>
    /// No cambia la marca de primaria; para eso se usa <c>set-primary</c>.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="locationId">Id de la ubicación.</param>
    /// <param name="body">Datos de la ubicación.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ubicación actualizada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPut("{locationId:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BpLocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateLocation(
        [FromRoute] Guid bpId,
        [FromRoute] Guid locationId,
        [FromBody] UpdateLocationRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var cmd = new UpdateBpLocationCommand(
            bpId,
            locationId,
            body.Name,
            body.Type,
            body.Purpose,
            body.AddressLine,
            body.ProvinceCode,
            body.CantonCode,
            body.ParishCode,
            body.Phone,
            body.Email,
            body.OtherDescription
        );

        var result = await _mediator.Send(cmd, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Marca una ubicación como primaria del tercero.
    /// </summary>
    /// <remarks>
    /// La ubicación primaria anterior pierde la marca.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="locationId">Id de la ubicación.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ubicación marcada como primaria.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPatch("{locationId:guid}/set-primary")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPrimary(
        [FromRoute] Guid bpId,
        [FromRoute] Guid locationId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new SetPrimaryBpLocationCommand(bpId, locationId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Reactiva una ubicación desactivada.
    /// </summary>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="locationId">Id de la ubicación.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ubicación activada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{locationId:guid}/activate")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid bpId,
        [FromRoute] Guid locationId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new ActivateBpLocationCommand(bpId, locationId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Desactiva una ubicación de un tercero.
    /// </summary>
    /// <remarks>
    /// Baja lógica (el método DELETE no elimina el registro). No se puede desactivar la ubicación primaria ni una
    /// con contactos activos.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="locationId">Id de la ubicación.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ubicación desactivada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpDelete("{locationId:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid bpId,
        [FromRoute] Guid locationId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new DeactivateBpLocationCommand(bpId, locationId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }
}
