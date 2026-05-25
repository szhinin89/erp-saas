using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Accounting.UseCases.CreateAccount;
using ERP.Application.Modules.Accounting.UseCases.DisableAccount;
using ERP.Application.Modules.Accounting.UseCases.EnableAccount;
using ERP.Application.Modules.Accounting.UseCases.GetAccounts;
using ERP.Application.Modules.Accounting.UseCases.GetAccountById;
using ERP.Application.Modules.Accounting.UseCases.UpdateAccount;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

[AppFeature("Contabilidad", "perm:finance.accounts.view", "📒", "/finance/accounts", null, 20)]
[ApiController]
[Route("api/finance/accounts")]
[Authorize]
[Produces("application/json")]
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:finance.accounts.view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AccountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAccountsQuery(pageNumber, pageSize), ct);
        if (!result.IsSuccess)
            return this.ApiBadRequest(result.Error ?? "Error");
        if (result.Value is null)
            return this.ApiUnprocessableEntity("Respuesta de paginación inválida.");

        return this.ApiOk(new PagedResponse<AccountDto>(
            Items: result.Value.Items,
            PageNumber: result.Value.PageNumber,
            PageSize: result.Value.PageSize,
            TotalCount: result.Value.TotalCount));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:finance.accounts.view")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAccountByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:finance.accounts.edit")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID de la ruta no coincide con el ID del comando.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:finance.accounts.edit")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAccount(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DisableAccountCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Deshabilitada");
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:finance.accounts.edit")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAccount(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new EnableAccountCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Habilitada");
    }

    [HttpPost]
    [Authorize(Policy = "perm:finance.accounts.create")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateAccountCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }
}
