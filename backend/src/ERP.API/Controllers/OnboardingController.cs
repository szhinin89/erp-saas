using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Onboarding.CompleteCompanyOnboarding;
using ERP.Application.Modules.Onboarding.GetOnboardingContext;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Company onboarding wizard API.
/// Accessible BEFORE onboarding is complete (exempt from CompanyOnboardingMiddleware).
/// The tenant admin uses this to complete the initial ERP setup.
/// </summary>
[ApiController]
[Route("api/onboarding")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
public sealed class OnboardingController : ControllerBase
{
    private readonly IMediator _mediator;
    public OnboardingController(IMediator mediator) => _mediator = mediator;

    // ── GET /api/onboarding/company — load prefill data for the wizard ─────────

    /// <summary>
    /// Returns the current company data and subscriber name for autofill in the onboarding wizard.
    /// Can be called before onboarding is complete (exempt path).
    /// </summary>
    [HttpGet("company")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingContextDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContext(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOnboardingContextQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ── POST /api/onboarding/company/complete — finalize onboarding ──────────

    /// <summary>
    /// Completes the company onboarding wizard:
    /// 1. Updates Company with real fiscal data
    /// 2. Bootstraps ERP infrastructure (Branch, Warehouse, Establishment, EmissionPoint)
    /// 3. Marks OnboardingCompleted = true
    /// After this call, the user can access the ERP dashboard.
    /// </summary>
    [HttpPost("company/complete")]
    [ProducesResponseType(typeof(ApiResponse<CompanyOnboardingResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Complete(
        [FromBody] CompleteOnboardingRequest body,
        CancellationToken ct)
    {
        var command = new CompleteCompanyOnboardingCommand(
            UseSubscriberData:   body.UseSubscriberData,
            TaxId:               body.TaxId,
            LegalName:           body.LegalName,
            TradeName:           body.TradeName,
            MainAddress:         body.MainAddress,
            Phone:               body.Phone,
            Email:               body.Email,
            CreateDefaultBranch: body.CreateDefaultBranch);

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Onboarding completado.");
    }
}

public sealed record CompleteOnboardingRequest(
    bool    UseSubscriberData   = false,
    string  TaxId               = "",
    string  LegalName           = "",
    string? TradeName           = null,
    string  MainAddress         = "",
    string? Phone               = null,
    string? Email               = null,
    bool    CreateDefaultBranch = true);
