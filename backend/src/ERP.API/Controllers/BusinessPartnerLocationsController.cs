using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.BpContacts;
using ERP.Application.MasterData.UseCases.BpLocations;
using ERP.Domain.MasterData.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Gestiona ubicaciones y contactos operativos de un BusinessPartner.
/// Ambos son company-scoped: cada empresa registra sus propias ubicaciones/contactos por cliente/proveedor.
/// </summary>
[ApiController]
[Route("api/master/business-partners/{bpId:guid}")]
[Authorize]
public sealed class BusinessPartnerLocationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BusinessPartnerLocationsController(IMediator mediator) => _mediator = mediator;

    // â”€â”€ LOCATIONS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [HttpGet("locations")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetLocations(Guid bpId, [FromQuery] bool? onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBpLocationsQuery(bpId, onlyActive), ct);
        return result.IsSuccess ? this.ApiOk(result.Value!) : this.ApiBadRequest(result.Error);
    }

    [HttpGet("locations/{locationId:guid}")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetLocation(Guid bpId, Guid locationId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBpLocationByIdQuery(locationId), ct);
        return result.IsSuccess ? this.ApiOk(result.Value!) : this.ApiNotFound(result.Error);
    }

    [HttpPost("locations")]
    [ProducesResponseType(typeof(object), 201)]
    public async Task<IActionResult> CreateLocation(Guid bpId, [FromBody] CreateLocationRequest req, CancellationToken ct = default)
    {
        var cmd = new CreateBpLocationCommand(bpId, req.Name, req.Type, req.Address,
            req.ProvinceCode, req.CantonCode, req.ParishCode, req.Phone, req.Email, req.IsPrimary);
        var result = await _mediator.Send(cmd, ct);
        return result.IsSuccess
            ? this.ApiCreated(result.Value!)
            : this.ApiBadRequest(result.Error);
    }

    [HttpPut("locations/{locationId:guid}")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> UpdateLocation(Guid bpId, Guid locationId, [FromBody] UpdateLocationRequest req, CancellationToken ct = default)
    {
        var cmd = new UpdateBpLocationCommand(locationId, req.Name, req.Type, req.Address,
            req.ProvinceCode, req.CantonCode, req.ParishCode, req.Phone, req.Email);
        var result = await _mediator.Send(cmd, ct);
        return result.IsSuccess ? this.ApiOk(result.Value!) : this.ApiBadRequest(result.Error);
    }

    [HttpPatch("locations/{locationId:guid}/set-primary")]
    public async Task<IActionResult> SetPrimaryLocation(Guid bpId, Guid locationId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SetPrimaryBpLocationCommand(locationId), ct);
        return result.IsSuccess ? this.ApiOk(result.Value!) : this.ApiBadRequest(result.Error);
    }

    [HttpDelete("locations/{locationId:guid}")]
    public async Task<IActionResult> DeactivateLocation(Guid bpId, Guid locationId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeactivateBpLocationCommand(locationId), ct);
        return result.IsSuccess ? this.ApiOk(result.Value!) : this.ApiBadRequest(result.Error);
    }

    // â”€â”€ CONTACTS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [HttpGet("contacts")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetContacts(Guid bpId, [FromQuery] bool? onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBpContactsQuery(bpId, onlyActive), ct);
        return result.IsSuccess ? this.ApiOk(result.Value!) : this.ApiBadRequest(result.Error);
    }

    [HttpGet("contacts/{contactId:guid}")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetContact(Guid bpId, Guid contactId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBpContactByIdQuery(contactId), ct);
        return result.IsSuccess ? this.ApiOk(result.Value!) : this.ApiNotFound(result.Error);
    }

    [HttpPost("contacts")]
    [ProducesResponseType(typeof(object), 201)]
    public async Task<IActionResult> CreateContact(Guid bpId, [FromBody] CreateContactRequest req, CancellationToken ct = default)
    {
        var cmd = new CreateBpContactCommand(bpId, req.FirstName, req.Role,
            req.LocationId, req.LastName, req.Position,
            req.Phone, req.Mobile, req.Email, req.Notes, req.IsPrimary);
        var result = await _mediator.Send(cmd, ct);
        return result.IsSuccess
            ? this.ApiCreated(result.Value!)
            : this.ApiBadRequest(result.Error);
    }

    [HttpPut("contacts/{contactId:guid}")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> UpdateContact(Guid bpId, Guid contactId, [FromBody] UpdateContactRequest req, CancellationToken ct = default)
    {
        var cmd = new UpdateBpContactCommand(contactId, req.FirstName, req.Role,
            req.LocationId, req.LastName, req.Position,
            req.Phone, req.Mobile, req.Email, req.Notes);
        var result = await _mediator.Send(cmd, ct);
        return result.IsSuccess ? this.ApiOk(result.Value!) : this.ApiBadRequest(result.Error);
    }

    [HttpPatch("contacts/{contactId:guid}/set-primary")]
    public async Task<IActionResult> SetPrimaryContact(Guid bpId, Guid contactId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SetPrimaryBpContactCommand(contactId), ct);
        return result.IsSuccess ? this.ApiOk(result.Value!) : this.ApiBadRequest(result.Error);
    }

    [HttpDelete("contacts/{contactId:guid}")]
    public async Task<IActionResult> DeactivateContact(Guid bpId, Guid contactId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeactivateBpContactCommand(contactId), ct);
        return result.IsSuccess ? this.ApiOk(result.Value!) : this.ApiBadRequest(result.Error);
    }
}

// â”€â”€ Request models â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record CreateLocationRequest(
    string       Name,
    LocationType Type,
    string       Address,
    string?      ProvinceCode,
    string?      CantonCode,
    string?      ParishCode,
    string?      Phone,
    string?      Email,
    bool         IsPrimary = false);

public sealed record UpdateLocationRequest(
    string       Name,
    LocationType Type,
    string       Address,
    string?      ProvinceCode,
    string?      CantonCode,
    string?      ParishCode,
    string?      Phone,
    string?      Email);

public sealed record CreateContactRequest(
    string      FirstName,
    ContactRole Role,
    Guid?       LocationId,
    string?     LastName,
    string?     Position,
    string?     Phone,
    string?     Mobile,
    string?     Email,
    string?     Notes,
    bool        IsPrimary = false);

public sealed record UpdateContactRequest(
    string      FirstName,
    ContactRole Role,
    Guid?       LocationId,
    string?     LastName,
    string?     Position,
    string?     Phone,
    string?     Mobile,
    string?     Email,
    string?     Notes);
