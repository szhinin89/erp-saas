using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.UseCases.GetBalanceComprobacion;
using ERP.Application.Modules.Accounting.UseCases.GetMayorGeneral;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/finance/accounts")]
[Authorize]
[Produces("application/json")]
public sealed class AccountingReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountingReportsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id:guid}/mayor")]
    [Authorize(Policy = "perm:finance.journal.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MayorGeneralLineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMayorGeneral(Guid id, [FromQuery] string desde, [FromQuery] string hasta, CancellationToken ct = default)
    {
        if (!AccountingQueryParameters.TryParseReportDateRange(desde, hasta, out var d, out var h))
            return this.ApiBadRequest("Parámetros 'desde' y 'hasta' requeridos en formato YYYY-MM-DD.");

        var result = await _mediator.Send(new GetMayorGeneralQuery(id, d, h), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<MayorGeneralLineDto>());
    }

    [HttpGet("balance-comprobacion")]
    [Authorize(Policy = "perm:finance.journal.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BalanceComprobacionLineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBalanceComprobacion([FromQuery] string desde, [FromQuery] string hasta, CancellationToken ct = default)
    {
        if (!AccountingQueryParameters.TryParseReportDateRange(desde, hasta, out var d, out var h))
            return this.ApiBadRequest("Parámetros 'desde' y 'hasta' requeridos en formato YYYY-MM-DD.");

        var result = await _mediator.Send(new GetBalanceComprobacionQuery(d, h), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<BalanceComprobacionLineDto>());
    }
}
