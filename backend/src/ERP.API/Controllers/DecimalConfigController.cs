using ERP.API.Extensions;
using ERP.Application.Modules.Companies.UseCases.DecimalConfig;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/config/decimals")]
[Authorize]
[Produces("application/json")]
public sealed class DecimalConfigController : ControllerBase
{
    private readonly IMediator _mediator;
    public DecimalConfigController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetDecimalConfigQuery(), ct), "OK");

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateDecimalConfigCommand cmd, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(cmd, ct));
}
