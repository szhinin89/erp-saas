using System.Security.Claims;
using ERP.API.Extensions;
using ERP.Application.Modules.Integration;
using ERP.Application.Modules.Integration.UseCases;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Integration;

[ApiController]
[Route("api/integration/v1")]
[Authorize(Policy = "IntegrationApi")]
[Produces("application/json")]
public sealed class IntegrationController : ControllerBase
{
    private readonly IMediator _mediator;

    public IntegrationController(IMediator mediator) => _mediator = mediator;

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant(
        [FromBody] IntegrationTenantCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new CreateIntegrationTenantCommand(request, ResolveActorId()),
            cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    [HttpGet("tenants/{id:guid}/status")]
    public async Task<IActionResult> GetTenantStatus(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetIntegrationTenantStatusQuery(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("tenants/{id:guid}/activate")]
    public async Task<IActionResult> ActivateTenant(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ActivateIntegrationTenantCommand(id, ResolveActorId()),
            cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("tenants/{id:guid}/suspend")]
    public async Task<IActionResult> SuspendTenant(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new SuspendIntegrationTenantCommand(id, ResolveActorId()),
            cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("companies")]
    public async Task<IActionResult> CreateCompany(
        [FromBody] IntegrationCompanyCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateIntegrationCompanyCommand(request), cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    [HttpGet("companies/{id:guid}/status")]
    public async Task<IActionResult> GetCompanyStatus(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetIntegrationCompanyStatusQuery(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("companies/{id:guid}/activate")]
    public async Task<IActionResult> ActivateCompany(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ActivateIntegrationCompanyCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("companies/{id:guid}/suspend")]
    public async Task<IActionResult> SuspendCompany(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SuspendIntegrationCompanyCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    private Guid ResolveActorId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? User.FindFirstValue("user_id");
        return Guid.TryParse(raw, out var actorId) ? actorId : Guid.Empty;
    }
}
