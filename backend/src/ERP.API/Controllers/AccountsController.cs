using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Application.Accounting.UseCases.CreateAccount;
using ERP.Application.Accounting.UseCases.GetAccounts;
using ERP.Application.Accounting.UseCases.GetAccountById;
using ERP.Application.Modules.Accounting.UseCases.CreateJournalEntry;
using CreateJournalEntryCommand = ERP.Application.Accounting.UseCases.CreateJournalEntry.CreateJournalEntryCommand;
using ERP.Application.Accounting.UseCases.GetJournalEntries;
using ERP.Application.Accounting.UseCases.GetJournalEntryById;
using ERP.Application.Accounting.DTOs;

namespace ERP.API.Controllers;

/// <summary>
/// Módulo de contabilidad: plan de cuentas y asientos contables del tenant autenticado.
/// Todos los endpoints filtran automáticamente por el tenant del JWT.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class AccountsController : ControllerBase
{
    private readonly CreateAccountHandler       _createAccountHandler;
    private readonly GetAccountsHandler         _getAccountsHandler;
    private readonly GetAccountByIdHandler      _getAccountByIdHandler;
    private readonly CreateJournalEntryHandler  _createJournalEntryHandler;
    private readonly GetJournalEntriesHandler   _getJournalEntriesHandler;
    private readonly GetJournalEntryByIdHandler _getJournalEntryByIdHandler;

    public AccountsController(
        CreateAccountHandler createAccountHandler,
        GetAccountsHandler getAccountsHandler,
        GetAccountByIdHandler getAccountByIdHandler,
        CreateJournalEntryHandler createJournalEntryHandler,
        GetJournalEntriesHandler getJournalEntriesHandler,
        GetJournalEntryByIdHandler getJournalEntryByIdHandler)
    {
        _createAccountHandler       = createAccountHandler;
        _getAccountsHandler         = getAccountsHandler;
        _getAccountByIdHandler      = getAccountByIdHandler;
        _createJournalEntryHandler  = createJournalEntryHandler;
        _getJournalEntriesHandler   = getJournalEntriesHandler;
        _getJournalEntryByIdHandler = getJournalEntryByIdHandler;
    }

    // ── Plan de cuentas ────────────────────────────────────────────

    /// <summary>Retorna el plan de cuentas completo del tenant, ordenado por código.</summary>
    /// <response code="200">Lista de cuentas contables (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _getAccountsHandler.HandleAsync(ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : StatusCode(StatusCodes.Status500InternalServerError, new { error = result.Error });
    }

    /// <summary>Retorna una cuenta contable por su ID.</summary>
    /// <param name="id">ID de la cuenta (GUID).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Cuenta encontrada.</response>
    /// <response code="404">La cuenta no existe o no pertenece al tenant.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getAccountByIdHandler.HandleAsync(id, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
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
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAccountCommand command,
        CancellationToken ct)
    {
        var result = await _createAccountHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { error = result.Error });
    }

    // ── Asientos contables ─────────────────────────────────────────

    /// <summary>Retorna todos los asientos contables del tenant, ordenados por fecha descendente.</summary>
    /// <response code="200">Lista de asientos contables con sus líneas.</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet("journal-entries")]
    [ProducesResponseType(typeof(IReadOnlyList<JournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetJournalEntries(CancellationToken ct)
    {
        var result = await _getJournalEntriesHandler.HandleAsync(ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : StatusCode(StatusCodes.Status500InternalServerError, new { error = result.Error });
    }

    /// <summary>Retorna un asiento contable con todas sus líneas.</summary>
    /// <param name="id">ID del asiento (GUID).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Asiento encontrado con líneas de débito/crédito.</response>
    /// <response code="404">El asiento no existe o no pertenece al tenant.</response>
    [HttpGet("journal-entries/{id:guid}")]
    [ProducesResponseType(typeof(JournalEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetJournalEntryById(Guid id, CancellationToken ct)
    {
        var result = await _getJournalEntryByIdHandler.HandleAsync(id, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    /// <summary>Crea un nuevo asiento contable.</summary>
    /// <remarks>
    /// El asiento debe estar cuadrado: la suma de débitos debe ser igual a la suma de créditos.
    /// El estado inicial es siempre `Draft` (0). Para contabilizarlo se requerirá un endpoint adicional.
    /// </remarks>
    /// <response code="201">Asiento creado en estado Borrador.</response>
    /// <response code="400">El asiento no está cuadrado u otro error de validación.</response>
    [HttpPost("journal-entries")]
    [ProducesResponseType(typeof(JournalEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateJournalEntry(
        [FromBody] CreateJournalEntryCommand command,
        CancellationToken ct)
    {
        var result = await _createJournalEntryHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetJournalEntryById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { error = result.Error });
    }
}
