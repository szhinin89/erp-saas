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

                    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(
        typeof(ApiResponse<CompanyBpSalesSettingsDto>),
        StatusCodes.Status200OK
    )]
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

                    [HttpPut]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersConfigureCompany}")]
    [ProducesResponseType(
        typeof(ApiResponse<CompanyBpSalesSettingsDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
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
