using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Finance.UseCases.CreditTerms;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Condiciones de Crédito", $"perm:{FinancePermissions.View}", "📋", "/finance/credit-terms", null, 55)]
[ApiController]
[Route("api/v1/finance/credit-terms")]
[Authorize]
[Produces("application/json")]
public sealed class CreditTermsController : ControllerBase
{
    private readonly IMediator _mediator;
    public CreditTermsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = $"perm:{FinancePermissions.View}")]
    public async Task<IActionResult> GetCreditTerms(
        [FromQuery] bool? isActive = null, [FromQuery] string? search = null,
        CancellationToken ct = default)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetCreditTermsQuery(isActive, search), ct), "OK");

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{FinancePermissions.View}")]
    public async Task<IActionResult> GetCreditTermById(Guid id, CancellationToken ct)
        => this.ToOkOrNotFound(await _mediator.Send(new GetCreditTermByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Policy = $"perm:{FinancePermissions.Create}")]
    public async Task<IActionResult> CreateCreditTerm(
        [FromBody] CreateCreditTermCommand command, CancellationToken ct)
        => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{FinancePermissions.Update}")]
    public async Task<IActionResult> UpdateCreditTerm(
        Guid id, [FromBody] UpdateCreditTermCommand command, CancellationToken ct)
    {
        if (id != command.Id) return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{FinancePermissions.Update}")]
    public async Task<IActionResult> EnableCreditTerm(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableCreditTermCommand(id), ct));

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{FinancePermissions.Update}")]
    public async Task<IActionResult> DisableCreditTerm(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableCreditTermCommand(id), ct));
}
