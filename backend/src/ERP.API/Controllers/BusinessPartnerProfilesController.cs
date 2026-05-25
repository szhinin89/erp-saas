using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.AddBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.GetCompanyBpSettings;
using ERP.Application.MasterData.UseCases.UpdateCustomerNotes;
using ERP.Application.MasterData.UseCases.UpdateSupplierProfile;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/master/business-partners")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData")]
public sealed class BusinessPartnerProfilesController : ControllerBase
{
    private readonly IMediator _mediator;

    public BusinessPartnerProfilesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id:guid}/company-settings")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.View}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBpSettingsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompanySettings([FromRoute] Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCompanyBpSettingsQuery(id), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/company-settings")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.ConfigureCompany}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertCompanySettings([FromRoute] Guid id, [FromBody] UpsertCompanyBpSettingsRequest body, CancellationToken ct = default)
    {
        var command = new UpsertCompanyBpSettingsCommand(id, body.CreditLimit, body.PaymentDays, body.IsBlocked);
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/add-role")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddRole([FromRoute] Guid id, [FromBody] AddBusinessPartnerRoleRequest body, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new AddBusinessPartnerRoleCommand(id, body.AsCustomer, body.AsSupplier), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/customer-notes")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCustomerNotes([FromRoute] Guid id, [FromBody] UpdateCustomerNotesRequest body, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateCustomerNotesCommand(id, body.Notes), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/supplier-profile")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSupplierProfile([FromRoute] Guid id, [FromBody] UpdateSupplierProfileRequest body, CancellationToken ct = default)
    {
        var command = new UpdateSupplierProfileCommand(
            id,
            body.DefaultTaxSupportCode,
            body.DefaultRetentionVatCode,
            body.DefaultRetentionIncomeCode,
            body.PaymentTerms);

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }
}
