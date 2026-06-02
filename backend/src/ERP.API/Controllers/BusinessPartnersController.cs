using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.ActivateBusinessPartner;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.DisableBusinessPartner;
using ERP.Application.MasterData.UseCases.GetBusinessPartner;
using ERP.Application.MasterData.UseCases.SearchBusinessPartners;
using ERP.Application.MasterData.UseCases.UpdateBusinessPartner;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/master/business-partners")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData")]
public sealed class BusinessPartnersController : ControllerBase
{
    private readonly IMediator _mediator;

    public BusinessPartnersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.View}")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BusinessPartnerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? q = null,
        [FromQuery] bool? isActive = true,
        [FromQuery] bool? isCustomer = null,
        [FromQuery] bool? isSupplier = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SearchBusinessPartnersQuery(q, isActive, isCustomer, isSupplier, skip, take), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.View}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBusinessPartnerQuery(id), ct);
        if (!result.IsSuccess)
            return this.ApiNotFound(result.Error ?? "BusinessPartner no encontrado.");
        return this.ApiOk(result.Value!);
    }

    [HttpPost]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Create}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateBusinessPartnerRequest body, CancellationToken ct = default)
    {
        var command = new CreateBusinessPartnerCommand(
            body.IdentificationType,
            body.IdentificationNumber,
            body.LegalName,
            body.TradeName,
            body.LegalRepresentativeName,
            body.Email,
            body.Phone,
            body.CountryCode,
            body.AsCustomer,
            body.AsSupplier);

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateBusinessPartnerRequest body, CancellationToken ct = default)
    {
        var command = new UpdateBusinessPartnerCommand(
            id,
            body.IdentificationType,
            body.IdentificationNumber,
            body.LegalName,
            body.TradeName,
            body.LegalRepresentativeName,
            body.Email,
            body.Phone,
            body.CountryCode);

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Disable}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Disable([FromRoute] Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableBusinessPartnerCommand(id), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate([FromRoute] Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ActivateBusinessPartnerCommand(id), ct);
        return this.ToOkOrBadRequest(result);
    }
}
