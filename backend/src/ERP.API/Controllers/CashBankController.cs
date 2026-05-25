using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Contracts.Cash;
using ERP.API.Extensions;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Modules.Cash.UseCases;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

[AppFeature("Caja y bancos", "perm:cash.bank.view", "🏦", "/cash/bank", null, 60)]
[ApiController]
[Route("api/cash/bank")]
[Authorize]
[Produces("application/json")]
public sealed class CashBankController : ControllerBase
{
    private readonly IMediator _mediator;

    public CashBankController(IMediator mediator) => _mediator = mediator;

    [HttpGet("cuentas-bancarias")]
    [Authorize(Policy = "perm:cash.bank.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BankAccountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBankAccounts(CancellationToken ct)
    {
        var r = await _mediator.Send(new ListCuentasBancariasQuery(), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<BankAccountDto>());
    }

    [HttpPost("cuentas-bancarias")]
    [Authorize(Policy = "perm:cash.bank.create")]
    [ProducesResponseType(typeof(ApiResponse<BankAccountDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBankAccount([FromBody] CreateBankAccountRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(
            new CrearBankAccountCommand(
                body.Name,
                body.AccountNumber,
                body.AccountType,
                body.Currency,
                body.InitialBalance,
                body.LedgerAccountId),
            ct);
        return this.ToCreatedOrBadRequest(r, "Creado");
    }
}
