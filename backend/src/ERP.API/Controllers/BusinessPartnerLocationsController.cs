using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Domain.Kernel.Permissions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.BpLocations;
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

    /// <summary>Lista las ubicaciones del BP. Por defecto solo activas.</summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BpLocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLocations(
        [FromRoute] Guid  bpId,
        [FromQuery] bool? onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetBpLocationsQuery(bpId, onlyActive), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Obtiene una ubicación por Id.</summary>
    [HttpGet("{locationId:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(typeof(ApiResponse<BpLocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLocation(
        [FromRoute] Guid bpId,
        [FromRoute] Guid locationId,
        CancellationToken cancellationToken = default)
    {
        _ = bpId;
        var result = await _mediator.Send(new GetBpLocationByIdQuery(locationId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Crea una nueva ubicación.
    /// Si IsPrimary=true, se quita la marca de primaria de la ubicación anterior automáticamente.
    /// Para LocationType=Other, OtherDescription es obligatorio.
    /// Purpose es un bitmask: Billing=1, Delivery=2, Fiscal=4, Correspondence=8 (combinables).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BpLocationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateLocation(
        [FromRoute] Guid                  bpId,
        [FromBody]  CreateLocationRequest body,
        CancellationToken cancellationToken = default)
    {
        var cmd = new CreateBpLocationCommand(
            bpId, body.Name, body.Type, body.Purpose, body.AddressLine,
            body.ProvinceCode, body.CantonCode, body.ParishCode,
            body.Phone, body.Email, body.IsPrimary, body.OtherDescription);

        var result = await _mediator.Send(cmd, cancellationToken);
        return result.IsSuccess
            ? this.ApiCreated(result.Value!)
            : this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza los datos de la ubicación. No cambia IsPrimary — usar set-primary.</summary>
    [HttpPut("{locationId:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BpLocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateLocation(
        [FromRoute] Guid                  bpId,
        [FromRoute] Guid                  locationId,
        [FromBody]  UpdateLocationRequest body,
        CancellationToken cancellationToken = default)
    {
        _ = bpId;
        var cmd = new UpdateBpLocationCommand(
            locationId, body.Name, body.Type, body.Purpose, body.AddressLine,
            body.ProvinceCode, body.CantonCode, body.ParishCode,
            body.Phone, body.Email, body.OtherDescription);

        var result = await _mediator.Send(cmd, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Marca esta ubicación como primaria del BP.
    /// La ubicación primaria anterior pierde el flag automáticamente.
    /// </summary>
    [HttpPatch("{locationId:guid}/set-primary")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetPrimary(
        [FromRoute] Guid bpId,
        [FromRoute] Guid locationId,
        CancellationToken cancellationToken = default)
    {
        _ = bpId;
        var result = await _mediator.Send(new SetPrimaryBpLocationCommand(locationId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Reactiva una ubicación previamente desactivada.</summary>
    [HttpPatch("{locationId:guid}/activate")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid bpId,
        [FromRoute] Guid locationId,
        CancellationToken cancellationToken = default)
    {
        _ = bpId;
        var result = await _mediator.Send(new ActivateBpLocationCommand(locationId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Desactiva la ubicación (soft delete).
    /// Devuelve 422 si es la ubicación primaria o tiene contactos activos.
    /// </summary>
    [HttpDelete("{locationId:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid bpId,
        [FromRoute] Guid locationId,
        CancellationToken cancellationToken = default)
    {
        _ = bpId;
        var result = await _mediator.Send(new DeactivateBpLocationCommand(locationId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
