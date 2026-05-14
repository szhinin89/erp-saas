using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Contabilidad.UseCases.CreateAccount;
using ERP.Application.Modules.Contabilidad.UseCases.DisableAccount;
using ERP.Application.Modules.Contabilidad.UseCases.EnableAccount;
using ERP.Application.Modules.Contabilidad.UseCases.GetAccounts;
using ERP.Application.Modules.Contabilidad.UseCases.GetAccountById;
using ERP.Application.Modules.Contabilidad.UseCases.UpdateAccount;
using ERP.Application.Modules.Contabilidad.UseCases.VoidJournalEntry;
using CreateJournalEntryCommand = ERP.Application.Modules.Contabilidad.UseCases.CreateJournalEntry.CreateJournalEntryCommand;
using ERP.Application.Modules.Contabilidad.UseCases.GetJournalEntries;
using ERP.Application.Modules.Contabilidad.UseCases.GetJournalEntryById;
using ERP.Application.Modules.Contabilidad.DTOs;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Módulo de contabilidad: plan de cuentas y asientos contables del tenant autenticado.
/// Todos los endpoints filtran automáticamente por el tenant del JWT.
/// </summary>
[Modulo("Contabilidad", "perm:accounting.accounts.view", "📒", "/contabilidad", null, 20)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── Plan de cuentas ────────────────────────────────────────────

    /// <summary>Retorna el plan de cuentas completo del tenant, ordenado por código.</summary>
    /// <response code="200">Lista de cuentas contables (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [Authorize(Policy = "perm:accounting.accounts.view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AccountDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>Retorna una cuenta contable por su ID.</summary>
    /// <param name="id">ID de la cuenta (GUID).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Cuenta encontrada.</response>
    /// <response code="404">La cuenta no existe o no pertenece al tenant.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:accounting.accounts.view")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAccountByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    /// <summary>Actualiza una cuenta contable existente.</summary>
    /// <param name="id">ID de la cuenta (GUID).</param>
    /// <response code="200">Cuenta actualizada correctamente.</response>
    /// <response code="400">Datos inválidos o cuenta no encontrada.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:accounting.accounts.edit")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID de la ruta no coincide con el ID del comando.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Deshabilita una cuenta contable (no la elimina).</summary>
    /// <response code="200">Cuenta deshabilitada.</response>
    /// <response code="400">La cuenta ya está deshabilitada o no existe.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:accounting.accounts.edit")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DisableAccount(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DisableAccountCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Deshabilitada");
    }

    /// <summary>Habilita una cuenta contable previamente deshabilitada.</summary>
    /// <response code="200">Cuenta habilitada.</response>
    /// <response code="400">La cuenta ya está activa o no existe.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:accounting.accounts.edit")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EnableAccount(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new EnableAccountCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Habilitada");
    }

    /// <summary>Crea una nueva cuenta en el plan de cuentas del tenant.</summary>
    /// <remarks>
    /// El código de cuenta debe ser único por tenant.
    /// Los valores válidos para `type`: 0=Activo, 1=Pasivo, 2=Patrimonio, 3=Ingreso, 4=Gasto.
    /// Los valores válidos para `nature`: 0=Débito, 1=Crédito.
    /// </remarks>
    /// <response code="201">Cuenta creada correctamente.</response>
    /// <response code="400">El código ya existe en el tenant.</response>
    [HttpPost]
    [Authorize(Policy = "perm:accounting.accounts.create")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAccountCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    // ── Asientos contables ─────────────────────────────────────────

    /// <summary>Retorna todos los asientos contables del tenant, ordenados por fecha descendente.</summary>
    /// <response code="200">Lista de asientos contables con sus líneas.</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet("journal-entries")]
    [Authorize(Policy = "perm:accounting.journal.view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<JournalEntryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetJournalEntries([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetJournalEntriesQuery(pageNumber, pageSize), ct);
        if (!result.IsSuccess)
            return this.ApiBadRequest(result.Error ?? "Error");
        if (result.Value is null)
            return this.ApiUnprocessableEntity("Respuesta de paginación inválida.");

        return this.ApiOk(new PagedResponse<JournalEntryDto>(
                Items: result.Value.Items,
                PageNumber: result.Value.PageNumber,
                PageSize: result.Value.PageSize,
                TotalCount: result.Value.TotalCount));
    }

    /// <summary>Retorna un asiento contable con todas sus líneas.</summary>
    /// <param name="id">ID del asiento (GUID).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Asiento encontrado con líneas de débito/crédito.</response>
    /// <response code="404">El asiento no existe o no pertenece al tenant.</response>
    [HttpGet("journal-entries/{id:guid}")]
    [Authorize(Policy = "perm:accounting.journal.view")]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetJournalEntryById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetJournalEntryByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    /// <summary>Anula un asiento contable (Draft o Posted). No se puede revertir.</summary>
    /// <param name="id">ID del asiento (GUID).</param>
    /// <response code="200">Asiento anulado correctamente.</response>
    /// <response code="400">El asiento ya está anulado, no existe, o motivo vacío.</response>
    [HttpPatch("journal-entries/{id:guid}/void")]
    [Authorize(Policy = "perm:accounting.journal.edit")]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> VoidJournalEntry(Guid id, [FromBody] VoidJournalEntryCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID de la ruta no coincide con el ID del comando.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Anulado");
    }

    /// <summary>Crea un nuevo asiento contable.</summary>
    /// <remarks>
    /// El asiento debe estar cuadrado: la suma de débitos debe ser igual a la suma de créditos.
    /// El estado inicial es siempre `Draft` (0). Para contabilizarlo se requerirá un endpoint adicional.
    /// </remarks>
    /// <response code="201">Asiento creado en estado Borrador.</response>
    /// <response code="400">El asiento no está cuadrado u otro error de validación.</response>
    [HttpPost("journal-entries")]
    [Authorize(Policy = "perm:accounting.journal.create")]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateJournalEntry(
        [FromBody] CreateJournalEntryCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }
}
