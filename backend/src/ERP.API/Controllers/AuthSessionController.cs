using ERP.API.Auth;
using ERP.API.Controllers.Platform;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.ListMyCompanies;
using ERP.Application.Auth.UseCases.SwitchCompany;
using ERP.Application.Auth.UseCases.SwitchSubscriber;
using AccessibleCompanyDto = ERP.Application.Auth.DTOs.AccessibleCompanyDto;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/Auth")]
[Produces("application/json")]
public sealed class AuthSessionController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthSessionController(IMediator mediator) => _mediator = mediator;

    [HttpGet("my-companies")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AccessibleCompanyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMyCompanies(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListMyCompaniesQuery(), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("switch-company")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SwitchCompany([FromBody] SwitchCompanyRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SwitchCompanyCommand(request.CompanyId), ct);
        if (result.IsSuccess)
        {
            if (result.Value?.RefreshToken is not null && result.Value.RefreshTokenExpiry is not null)
                AuthRefreshCookieHelper.SetRefreshCookie(HttpContext, result.Value.RefreshToken, result.Value.RefreshTokenExpiry.Value);

            return this.ApiOk(result.Value);
        }

        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("switch-subscriber")]
    [Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SwitchSubscriber([FromBody] SwitchSubscriberCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (result.IsSuccess)
        {
            if (result.Value?.RefreshToken is not null && result.Value.RefreshTokenExpiry is not null)
                AuthRefreshCookieHelper.SetRefreshCookie(HttpContext, result.Value.RefreshToken, result.Value.RefreshTokenExpiry.Value);

            return this.ApiOk(result.Value);
        }

        return this.ToOkOrBadRequest(result);
    }
}
