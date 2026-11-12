using ERP.API.Attributes;
using ERP.API.Contracts;
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
/// (Caja y Bancos → Bancos → Cuentas bancarias). Alcance estricto: no posting, no caja, no
/// conciliación, no movimientos bancarios.
/// </summary>
[AppFeature(
    "Cuentas bancarias",
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

    /// <summary>Lista las cuentas bancarias de la empresa activa.</summary>
    /// <param name="isActive">Filtra por estado: <c>true</c> activas, <c>false</c> inactivas; sin valor devuelve todas.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Cuentas bancarias que cumplen el filtro (lista vacía si no hay).</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{TreasuryPermissions.BankAccountsView}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<CompanyBankAccountDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetList([FromQuery] bool? isActive, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCompanyBankAccountListQuery(isActive), ct),
            "OK"
        );

    /// <summary>Obtiene una cuenta bancaria de la empresa por su identificador.</summary>
    /// <param name="id">Identificador de la cuenta bancaria.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Cuenta bancaria encontrada.</response>
    /// <response code="404">La cuenta bancaria no existe.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{TreasuryPermissions.BankAccountsView}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBankAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCompanyBankAccountByIdQuery(id), ct),
            "OK"
        );

    /// <summary>Registra una cuenta bancaria de la empresa asociada a una cuenta contable de movimiento.</summary>
    /// <remarks>
    /// El banco debe existir y estar activo; la cuenta contable debe pertenecer a la empresa, estar activa y
    /// admitir movimientos. No puede repetirse la combinación banco + tipo + número de cuenta dentro de la
    /// empresa. La cuenta nace activa.
    /// </remarks>
    /// <param name="body">Banco, tipo y número de cuenta, nombre visible y cuenta contable asociada.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Cuenta bancaria creada.</response>
    /// <response code="404">El banco o la cuenta contable no existe (o la cuenta contable no es de esta empresa).</response>
    /// <response code="409">Ya existe una cuenta con el mismo banco, tipo y número.</response>
    /// <response code="422">Datos inválidos, banco inactivo, o cuenta contable inactiva o que no admite movimientos.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{TreasuryPermissions.BankAccountsCreate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBankAccountDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
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

    /// <summary>Actualiza el nombre visible y la cuenta contable asociada de una cuenta bancaria.</summary>
    /// <remarks>
    /// Banco, tipo y número de cuenta no se modifican. La nueva cuenta contable debe pertenecer a la empresa,
    /// estar activa y admitir movimientos.
    /// </remarks>
    /// <param name="id">Identificador de la cuenta bancaria.</param>
    /// <param name="body">Nuevo nombre visible y cuenta contable.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Cuenta bancaria actualizada.</response>
    /// <response code="404">La cuenta bancaria o la cuenta contable no existe.</response>
    /// <response code="422">Datos inválidos, o cuenta contable inactiva o que no admite movimientos.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{TreasuryPermissions.BankAccountsUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBankAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
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

    /// <summary>Reactiva una cuenta bancaria.</summary>
    /// <remarks>Baja lógica reversible: la cuenta nunca se elimina físicamente.</remarks>
    /// <param name="id">Identificador de la cuenta bancaria.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Cuenta bancaria activa.</response>
    /// <response code="404">La cuenta bancaria no existe.</response>
    [HttpPost("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{TreasuryPermissions.BankAccountsManage}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBankAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new SetCompanyBankAccountActiveCommand(id, true), ct)
        );

    /// <summary>Desactiva una cuenta bancaria.</summary>
    /// <remarks>Baja lógica reversible: la cuenta se conserva y puede reactivarse con <c>enable</c>.</remarks>
    /// <param name="id">Identificador de la cuenta bancaria.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Cuenta bancaria inactiva.</response>
    /// <response code="404">La cuenta bancaria no existe.</response>
    [HttpPost("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{TreasuryPermissions.BankAccountsManage}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBankAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
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
