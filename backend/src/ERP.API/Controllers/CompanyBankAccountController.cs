using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Finance.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// TREASURY-BANK-ACCOUNTS-01: CRUD básico + activar/desactivar de cuentas bancarias de empresa
/// (Tesorería → Bancos → Cuentas bancarias). Alcance estricto: no posting, no caja, no
/// conciliación, no movimientos bancarios.
/// </summary>
[AppFeature(
    "Cuentas Bancarias",
    $"perm:{TreasuryPermissions.BankAccountsView}",
    "🏦",
    "/treasury/banks/accounts",
    null,
    46,
    IsVisibleInMenu = false
)]
[ApiController]
[Route("api/v1/treasury/bank-accounts")]
[Authorize]
[Produces("application/json")]
public sealed class CompanyBankAccountController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyBankAccountController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = $"perm:{TreasuryPermissions.BankAccountsView}")]
    public async Task<IActionResult> GetList([FromQuery] bool? isActive, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetCompanyBankAccountListQuery(isActive), ct), "OK");

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{TreasuryPermissions.BankAccountsView}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetCompanyBankAccountByIdQuery(id), ct), "OK");

    [HttpPost]
    [Authorize(Policy = $"perm:{TreasuryPermissions.BankAccountsCreate}")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCompanyBankAccountRequest body,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new CreateCompanyBankAccountCommand(
                    body.BankId,
                    body.AccountType,
                    body.AccountNumber,
                    body.DisplayName,
                    body.AccountingAccountId
                ),
                ct
            )
        );

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{TreasuryPermissions.BankAccountsUpdate}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCompanyBankAccountRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new UpdateCompanyBankAccountCommand(id, body.DisplayName, body.AccountingAccountId),
                ct
            )
        );

    [HttpPost("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{TreasuryPermissions.BankAccountsManage}")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new SetCompanyBankAccountActiveCommand(id, true), ct)
        );

    [HttpPost("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{TreasuryPermissions.BankAccountsManage}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new SetCompanyBankAccountActiveCommand(id, false), ct)
        );
}

public sealed record CreateCompanyBankAccountRequest(
    Guid BankId,
    BankAccountType AccountType,
    string AccountNumber,
    string DisplayName,
    Guid AccountingAccountId
);

public sealed record UpdateCompanyBankAccountRequest(string DisplayName, Guid AccountingAccountId);
