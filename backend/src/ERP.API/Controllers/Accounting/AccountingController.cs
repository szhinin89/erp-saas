using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;
using ERP.Application.Modules.Accounting.UseCases.Accounts;
using ERP.Application.Modules.Accounting.UseCases.Continuity;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Accounting.UseCases.PostingRules;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Accounting.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Accounting;

/// <summary>
/// Administración del Plan de Cuentas / Períodos Contables / Reglas de Contabilización
/// (ADR-026). Desde ACCOUNTING-LEDGER-VISIBILITY-01 también expone lectura de JournalEntry
/// (listado paginado, detalle con líneas, búsqueda por documento de origen) — el Posting Engine
/// (§8) y el motor de contabilización ya existen y producen asientos reales (Post()/Reverse(),
/// Fase 5.2-5.4); esta fase solo agrega visibilidad, nunca Create/Update/Delete de JournalEntry.
/// Sin verbo DELETE — la baja lógica (Enable/Disable) usa PATCH, decisión deliberada frente al
/// patrón de otros módulos que sí usan DELETE para baja lógica (ver Architecture Review Board
/// Fase 2.2). Los reportes de solo lectura (Libro Diario/Mayor/Balance de Comprobación/Estado de
/// Resultados/Balance General) viven en <see cref="AccountingReportsController"/> desde
/// ACCOUNTING-FINANCIAL-STATEMENTS-10 — split mecánico para no seguir creciendo este archivo,
/// mismo patrón ya usado en Purchases.
/// </summary>
[AppFeature("Contabilidad", $"perm:{AccountingPermissions.View}", "📒", "/accounting", null, 60)]
[ApiController]
[Route("api/v1/accounting")]
[Authorize]
[Produces("application/json")]
public sealed class AccountingController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountingController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // ACCOUNTS (Chart of Accounts)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista todas las cuentas del Plan de Cuentas de la empresa activa (activas e inactivas).
    /// </summary>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Cuentas de la empresa.</response>
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<AccountDto>>),
        StatusCodes.Status200OK
    )]
    [HttpGet("accounts")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetAccounts(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetAccountsQuery(), ct), "OK");

    /// <summary>
    /// Obtiene una cuenta del Plan de Cuentas por su identificador.
    /// </summary>
    /// <param name="id">Identificador de la cuenta.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Cuenta encontrada.</response>
    /// <response code="404">La cuenta no existe en la empresa activa.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [HttpGet("accounts/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetAccountById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetAccountByIdQuery(id), ct));

    /// <summary>
    /// Obtiene una cuenta del Plan de Cuentas por su código contable.
    /// </summary>
    /// <param name="code">Código contable de la cuenta (único por empresa).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Cuenta encontrada.</response>
    /// <response code="404">No existe una cuenta con ese código en la empresa activa.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [HttpGet("accounts/by-code/{code}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetAccountByCode(string code, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetAccountByCodeQuery(code), ct));

    /// <summary>
    /// Crea una cuenta en el Plan de Cuentas de la empresa activa; la cuenta nace activa.
    /// </summary>
    /// <remarks>
    /// Se ejecuta en una transacción con el candado del Plan de Cuentas de la empresa. La jerarquía la fija el código contable: el padre indicado (o su ausencia) debe coincidir con el padre canónico implicado por el código. Un padre debe ser agrupador (no admitir movimiento), estar activo con toda su cadena y no tener asientos publicados.
    /// </remarks>
    /// <param name="command">Código, nombre, cuenta padre opcional, tipo, naturaleza y si admite movimiento.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Cuenta creada.</response>
    /// <response code="409">Ya existe una cuenta con ese código en la empresa, o el padre tiene un ancestro inactivo o asientos publicados.</response>
    /// <response code="422">Datos inválidos: código o nombre vacíos o demasiado largos, tipo o naturaleza fuera de rango, padre inexistente, padre que admite movimiento o jerarquía distinta de la implicada por el código.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountDto>),
        StatusCodes.Status201Created
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPost("accounts")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Create}")]
    public async Task<IActionResult> CreateAccount(
        [FromBody] CreateAccountCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// Edita el nombre, la cuenta padre y si admite movimiento de una cuenta existente.
    /// </summary>
    /// <remarks>
    /// El código, el tipo y la naturaleza son inmutables: no forman parte del contrato de edición. Se ejecuta en una transacción con el candado del Plan de Cuentas.
    /// </remarks>
    /// <param name="id">Identificador de la cuenta; debe coincidir con el del cuerpo.</param>
    /// <param name="command">Id, nombre, cuenta padre opcional y si admite movimiento.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Cuenta actualizada.</response>
    /// <response code="400">El identificador de la ruta no coincide con el del cuerpo.</response>
    /// <response code="404">La cuenta no existe en la empresa activa.</response>
    /// <response code="409">No se puede quitar «Permite movimiento» porque hay configuraciones vigentes que usan la cuenta.</response>
    /// <response code="422">Datos inválidos: nombre vacío, padre inexistente o la propia cuenta, ciclo en el árbol, jerarquía no canónica, o cuenta con subcuentas que no puede admitir movimiento.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPatch("accounts/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Update}")]
    public async Task<IActionResult> UpdateAccount(
        Guid id,
        [FromBody] UpdateAccountCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    /// <summary>
    /// Reactiva una cuenta deshabilitada (baja lógica reversible; el Plan de Cuentas no se elimina).
    /// </summary>
    /// <remarks>
    /// Bajo el candado del Plan de Cuentas. Una cuenta solo se reactiva si toda su cadena de ancestros está activa.
    /// </remarks>
    /// <param name="id">Identificador de la cuenta.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Cuenta activa.</response>
    /// <response code="404">La cuenta no existe en la empresa activa.</response>
    /// <response code="409">Un ancestro de la cuenta está inactivo: no se puede activar.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict
    )]
    [HttpPatch("accounts/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Update}")]
    public async Task<IActionResult> EnableAccount(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnableAccountCommand(id), ct));

    /// <summary>
    /// Deshabilita una cuenta (baja lógica; no existe DELETE). No altera asientos ya contabilizados.
    /// </summary>
    /// <remarks>
    /// Bajo el candado del Plan de Cuentas. No se bloquea por asientos ya contabilizados ni por borradores: son historial.
    /// </remarks>
    /// <param name="id">Identificador de la cuenta.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Cuenta deshabilitada.</response>
    /// <response code="404">La cuenta no existe en la empresa activa.</response>
    /// <response code="409">La cuenta tiene subcuentas activas o está en uso por configuraciones vigentes (por ejemplo, reglas de contabilización activas).</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict
    )]
    [HttpPatch("accounts/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Delete}")]
    public async Task<IActionResult> DisableAccount(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisableAccountCommand(id), ct));

    // ══════════════════════════════════════════════════════════════════════
    // ACCOUNTING PERIODS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista los períodos contables de la empresa activa.
    /// </summary>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Períodos contables.</response>
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<AccountingPeriodDto>>),
        StatusCodes.Status200OK
    )]
    [HttpGet("accounting-periods")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetAccountingPeriods(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetAccountingPeriodsQuery(), ct), "OK");

    /// <summary>
    /// Obtiene un período contable por su identificador.
    /// </summary>
    /// <param name="id">Identificador del período.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Período encontrado.</response>
    /// <response code="404">El período no existe en la empresa activa.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountingPeriodDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [HttpGet("accounting-periods/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetAccountingPeriodById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetAccountingPeriodByIdQuery(id), ct));

    // ACCOUNTING-CONTINUITY-06 — pre-cierre de solo lectura: mismo readiness que el cierre.
    /// <summary>
    /// Evalúa, sin modificar nada, si un período puede cerrarse y qué lo bloquea.
    /// </summary>
    /// <remarks>
    /// Es la misma evaluación que usa el cierre, pero sin candado: el resultado puede cambiar hasta que se cierre. Solo un período abierto es elegible; uno cerrado o bloqueado responde como no elegible.
    /// </remarks>
    /// <param name="id">Identificador del período.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de la evaluación de cierre.</response>
    /// <response code="404">El período no existe en la empresa activa.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountingPeriodClosureReadinessDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [HttpGet("accounting-periods/{id:guid}/closure-readiness")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetAccountingPeriodClosureReadiness(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetAccountingPeriodClosureReadinessQuery(id), ct));

    // ACCOUNTING-07: permisos dedicados — crear/generar con accounting.periods.create y
    // cerrar/bloquear con accounting.periods.close (ya no accounting.create/accounting.update).
    /// <summary>
    /// Crea un período contable de la empresa activa.
    /// </summary>
    /// <remarks>
    /// La estructura (solape, contigüidad, orden) se valida antes de guardar; la exclusión de solapes de la base de datos cierra la carrera de dos creaciones concurrentes.
    /// </remarks>
    /// <param name="command">Ejercicio, número de período, fecha de inicio y fecha de fin.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Período creado.</response>
    /// <response code="409">El período se solapa con otro existente, ya existe ese ejercicio y número, o hubo una creación concurrente en conflicto.</response>
    /// <response code="422">Datos inválidos: ejercicio, número de período fuera de rango, fin anterior al inicio, o estructura que rompe la contigüidad o el orden respecto de períodos cerrados o bloqueados.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountingPeriodDto>),
        StatusCodes.Status201Created
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPost("accounting-periods")]
    [Authorize(Policy = $"perm:{AccountingPermissions.PeriodsCreate}")]
    public async Task<IActionResult> CreateAccountingPeriod(
        [FromBody] CreateAccountingPeriodCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// Genera los 12 períodos mensuales de un ejercicio.
    /// </summary>
    /// <remarks>
    /// Idempotente: completa los meses que faltan al final de una estructura mensual compatible y no crea nada si ya existen los 12. Un ejercicio con estructura incompatible (por ejemplo, un período anual) no se divide ni se completa. Responde 200 aunque cree períodos.
    /// </remarks>
    /// <param name="command">Ejercicio a generar (entre 2000 y 2999).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ejercicio generado; indica cuántos períodos se crearon y la lista completa del ejercicio.</response>
    /// <response code="409">El ejercicio ya tiene una estructura incompatible con la generación mensual, o hay solape o concurrencia en conflicto.</response>
    /// <response code="422">Ejercicio fuera de rango o estructura inválida.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountingFiscalYearDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPost("accounting-periods/fiscal-years")]
    [Authorize(Policy = $"perm:{AccountingPermissions.PeriodsCreate}")]
    public async Task<IActionResult> GenerateAccountingFiscalYear(
        [FromBody] GenerateAccountingFiscalYearCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// Cierra un período contable ya terminado según la fecha de la empresa.
    /// </summary>
    /// <remarks>
    /// Corre en una transacción con candado exclusivo por empresa: ninguna obligación contable nueva puede confirmarse entre la evaluación y el cierre. Un período cerrado rechaza toda escritura y no existe reapertura. Consulte antes closure-readiness para conocer los bloqueos.
    /// </remarks>
    /// <param name="id">Identificador del período.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Período cerrado.</response>
    /// <response code="404">El período no existe en la empresa activa.</response>
    /// <response code="422">El período aún no termina según la fecha de la empresa, ya no está abierto, o la evaluación de cierre encontró información contable incompleta.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountingPeriodDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPatch("accounting-periods/{id:guid}/close")]
    [Authorize(Policy = $"perm:{AccountingPermissions.PeriodsClose}")]
    public async Task<IActionResult> CloseAccountingPeriod(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new CloseAccountingPeriodCommand(id), ct));

    /// <summary>
    /// Bloquea de forma definitiva un período que ya está cerrado.
    /// </summary>
    /// <remarks>
    /// Marca final administrativa e irreversible: no cambia lo que el período admite (el cerrado ya rechaza escrituras) y no existe desbloqueo.
    /// </remarks>
    /// <param name="id">Identificador del período.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Período bloqueado.</response>
    /// <response code="404">El período no existe en la empresa activa.</response>
    /// <response code="422">El período no está en estado Closed o ya está bloqueado.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountingPeriodDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPatch("accounting-periods/{id:guid}/lock")]
    [Authorize(Policy = $"perm:{AccountingPermissions.PeriodsClose}")]
    public async Task<IActionResult> LockAccountingPeriod(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new LockAccountingPeriodCommand(id), ct));

    // ACCOUNTING-12D — cierre anual: estado por ejercicio (accounting.view) y generación del Draft
    // (accounting.periods.close, parte del cierre). La publicación usa el flujo de manuales (accounting.manual.post).
    /// <summary>
    /// Lista el estado del cierre anual de cada ejercicio que tiene períodos.
    /// </summary>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Estado del cierre anual por ejercicio: NotGenerated, Draft o Posted.</response>
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<FiscalYearClosingDto>>),
        StatusCodes.Status200OK
    )]
    [HttpGet("accounting-periods/fiscal-year-closings")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetFiscalYearClosings(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetFiscalYearClosingsQuery(), ct), "OK");

    /// <summary>
    /// Genera en borrador el asiento de cierre anual de un ejercicio.
    /// </summary>
    /// <remarks>
    /// Solo genera el borrador: no lo publica. La publicación usa el flujo de asientos manuales. Se ejecuta en una transacción con el candado del ejercicio y vuelve a validar todo en la publicación. Los códigos de precondición no tienen entrada propia en el catálogo de mensajes, por lo que responden 400 y no 422.
    /// </remarks>
    /// <param name="fiscalYear">Ejercicio a cerrar (entre 2000 y 2999).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Asiento de cierre generado en estado Draft.</response>
    /// <response code="400">No se cumple una precondición del cierre anual; el código del envelope indica cuál (ejercicio sin períodos completos, último período no abierto o períodos anteriores abiertos, ejercicio no terminado, contabilidad no habilitada, información incompleta, borradores pendientes, asiento de cierre ya existente o ya contabilizado, sin resultado que cerrar, cuenta de resultado inválida).</response>
    /// <response code="422">Ejercicio fuera de rango, o regla de dominio violada al crear el asiento.</response>
    [ProducesResponseType(
        typeof(ApiResponse<FiscalYearClosingDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPost("accounting-periods/fiscal-years/{fiscalYear:int}/closing-entry")]
    [Authorize(Policy = $"perm:{AccountingPermissions.PeriodsClose}")]
    public async Task<IActionResult> GenerateFiscalYearClosingEntry(int fiscalYear, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GenerateFiscalYearClosingEntryCommand(fiscalYear), ct));

    // ══════════════════════════════════════════════════════════════════════
    // POSTING RULES
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista las reglas de contabilización de la empresa activa.
    /// </summary>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Reglas de contabilización.</response>
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<PostingRuleDto>>),
        StatusCodes.Status200OK
    )]
    [HttpGet("posting-rules")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetPostingRules(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetPostingRulesQuery(), ct), "OK");

    // ACCOUNTING-11: hechos soportados (PostingFactCatalog) con la regla de la empresa para cada uno.
    /// <summary>
    /// Lista los hechos contables soportados junto con la regla de la empresa para cada uno.
    /// </summary>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo de hechos; los que no tienen regla aún traen RuleId nulo.</response>
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<PostingFactCatalogItemDto>>),
        StatusCodes.Status200OK
    )]
    [HttpGet("posting-rules/catalog")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetPostingFactCatalog(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetPostingFactCatalogQuery(), ct), "OK");

    /// <summary>
    /// Obtiene una regla de contabilización por su identificador.
    /// </summary>
    /// <param name="id">Identificador de la regla.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Regla encontrada.</response>
    /// <response code="404">La regla no existe en la empresa activa.</response>
    [ProducesResponseType(
        typeof(ApiResponse<PostingRuleDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [HttpGet("posting-rules/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetPostingRuleById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPostingRuleByIdQuery(id), ct));

    /// <summary>
    /// Crea la regla de contabilización de un hecho soportado del catálogo.
    /// </summary>
    /// <remarks>
    /// Solo se crean reglas para hechos del catálogo (nunca una clave libre). Hay una regla por módulo, hecho y empresa.
    /// </remarks>
    /// <param name="command">Módulo de origen, tipo de hecho y líneas de la regla.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Regla creada.</response>
    /// <response code="409">Ya existe una regla para ese módulo y hecho en la empresa.</response>
    /// <response code="422">Datos inválidos: hecho no soportado por el catálogo, líneas que no respetan la forma del hecho, o cuentas inexistentes, inactivas o que no admiten movimiento.</response>
    [ProducesResponseType(
        typeof(ApiResponse<PostingRuleDto>),
        StatusCodes.Status201Created
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPost("posting-rules")]
    [Authorize(Policy = $"perm:{AccountingPermissions.RulesManage}")]
    public async Task<IActionResult> CreatePostingRule(
        [FromBody] CreatePostingRuleCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// Reemplaza por completo las líneas de una regla de contabilización.
    /// </summary>
    /// <remarks>
    /// Solo afecta a contabilizaciones futuras: los asientos ya publicados no se tocan. El módulo y el tipo de hecho no se editan.
    /// </remarks>
    /// <param name="id">Identificador de la regla; debe coincidir con el del cuerpo.</param>
    /// <param name="command">Id y nuevas líneas de la regla.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Regla actualizada.</response>
    /// <response code="400">El identificador de la ruta no coincide con el del cuerpo.</response>
    /// <response code="404">La regla no existe en la empresa activa.</response>
    /// <response code="422">El hecho ya no está soportado, o las líneas o cuentas son inválidas.</response>
    [ProducesResponseType(
        typeof(ApiResponse<PostingRuleDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPatch("posting-rules/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.RulesManage}")]
    public async Task<IActionResult> UpdatePostingRule(
        Guid id,
        [FromBody] UpdatePostingRuleCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    /// <summary>
    /// Reactiva una regla de contabilización deshabilitada.
    /// </summary>
    /// <remarks>
    /// Solo se reactiva una regla válida: hecho soportado y líneas vigentes con cuentas activas que admiten movimiento.
    /// </remarks>
    /// <param name="id">Identificador de la regla.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Regla activa.</response>
    /// <response code="404">La regla no existe en la empresa activa.</response>
    /// <response code="409">La regla ya está activa.</response>
    /// <response code="422">El hecho ya no está soportado o las líneas vigentes ya no son válidas (cuentas inactivas o sin movimiento).</response>
    [ProducesResponseType(
        typeof(ApiResponse<PostingRuleDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPatch("posting-rules/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{AccountingPermissions.RulesManage}")]
    public async Task<IActionResult> EnablePostingRule(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnablePostingRuleCommand(id), ct));

    /// <summary>
    /// Deshabilita una regla de contabilización (baja lógica; no existe DELETE).
    /// </summary>
    /// <param name="id">Identificador de la regla.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Regla deshabilitada.</response>
    /// <response code="404">La regla no existe en la empresa activa.</response>
    /// <response code="409">La regla ya está deshabilitada.</response>
    [ProducesResponseType(
        typeof(ApiResponse<PostingRuleDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict
    )]
    [HttpPatch("posting-rules/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{AccountingPermissions.RulesManage}")]
    public async Task<IActionResult> DisablePostingRule(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisablePostingRuleCommand(id), ct));

    // ══════════════════════════════════════════════════════════════════════
    // JOURNAL ENTRIES (solo lectura — ACCOUNTING-LEDGER-VISIBILITY-01)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista de forma paginada los asientos contables de la empresa activa con filtros opcionales.
    /// </summary>
    /// <param name="pageNumber">Número de página (base 1); un valor menor o igual que 0 se toma como 1.</param>
    /// <param name="pageSize">Tamaño de página; un valor menor o igual que 0 se toma como 20 y el máximo aceptado es 100.</param>
    /// <param name="status">Estado del asiento.</param>
    /// <param name="fromDate">Fecha contable inicial (yyyy-MM-dd).</param>
    /// <param name="toDate">Fecha contable final (yyyy-MM-dd).</param>
    /// <param name="sourceModule">Módulo de origen del asiento.</param>
    /// <param name="manual">true: solo asientos manuales; false: solo automáticos; omitido: todos.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Página de asientos con el total de coincidencias.</response>
    /// <response code="422">Parámetros inválidos, por ejemplo un tamaño de página mayor que 100 o un valor de estado desconocido.</response>
    [ProducesResponseType(
        typeof(ApiResponse<GetJournalEntriesResponse>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpGet("journal-entries")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetJournalEntries(
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        [FromQuery] JournalEntryStatus? status,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] string? sourceModule,
        [FromQuery] bool? manual,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetJournalEntriesQuery(
                    pageNumber <= 0 ? 1 : pageNumber,
                    pageSize <= 0 ? 20 : pageSize,
                    status,
                    fromDate,
                    toDate,
                    sourceModule,
                    manual
                ),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Obtiene un asiento contable con sus líneas.
    /// </summary>
    /// <param name="id">Identificador del asiento.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Asiento con líneas y documento de origen resuelto.</response>
    /// <response code="404">El asiento no existe en la empresa activa.</response>
    [ProducesResponseType(
        typeof(ApiResponse<JournalEntryDetailDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [HttpGet("journal-entries/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetJournalEntryById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetJournalEntryByIdQuery(id), ct));

    /// <summary>
    /// Lista los asientos originados por un documento de origen concreto.
    /// </summary>
    /// <param name="sourceModule">Módulo de origen del documento.</param>
    /// <param name="sourceDocumentId">Identificador del documento de origen.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Asientos del documento; la lista es vacía si no tiene ninguno.</response>
    /// <response code="422">El módulo de origen o el identificador del documento están vacíos.</response>
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<JournalEntryListItemDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpGet("journal-entries/by-source/{sourceModule}/{sourceDocumentId:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetJournalEntriesBySource(
        string sourceModule,
        Guid sourceDocumentId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetJournalEntriesBySourceQuery(sourceModule, sourceDocumentId),
                ct
            ),
            "OK"
        );

    // ══════════════════════════════════════════════════════════════════════
    // CONTINUIDAD CONTABLE (ACCOUNTING-CONTINUITY-01)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Obtiene la política operativa contable vigente de la empresa activa.
    /// </summary>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Política operativa vigente.</response>
    /// <response code="404">La empresa no existe.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountingOperationalPolicyDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [HttpGet("operational-policy")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetOperationalPolicy(CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetAccountingOperationalPolicyQuery(), ct));

    /// <summary>
    /// Cambia el modo operativo de Contabilidad de la empresa a partir de una fecha de vigencia.
    /// </summary>
    /// <remarks>
    /// Corre en una transacción con candado exclusivo por empresa. Cierra la versión abierta justo antes de la nueva fecha de vigencia y registra una versión nueva; la obligación fiscal sigue siendo la de la empresa (IsAccountingReq).
    /// </remarks>
    /// <param name="command">Modo, fecha de vigencia y motivo opcional.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Política resultante con su historial de versiones.</response>
    /// <response code="404">La empresa no existe.</response>
    /// <response code="422">El modo ya es el vigente, la fecha o el modo incumplen las precondiciones del cambio, o hay datos inválidos.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountingOperationalPolicyDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPut("operational-policy")]
    [Authorize(Policy = $"perm:{AccountingPermissions.SettingsUpdate}")]
    public async Task<IActionResult> SetOperationalMode(
        [FromBody] SetAccountingOperationalModeCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// Lista las obligaciones de contabilización de la empresa activa con filtros opcionales.
    /// </summary>
    /// <param name="status">Estado de la obligación.</param>
    /// <param name="sourceModule">Módulo de origen (máximo 60 caracteres).</param>
    /// <param name="sourceEventId">Identificador del evento de origen.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Obligaciones de contabilización; se devuelven como máximo 200 (límite no configurable desde este endpoint).</response>
    /// <response code="422">Estado desconocido o módulo de origen demasiado largo.</response>
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<AccountingPostingDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpGet("postings")]
    [Authorize(Policy = $"perm:{AccountingPermissions.PostingsView}")]
    public async Task<IActionResult> GetPostings(
        [FromQuery] AccountingPostingStatus? status,
        [FromQuery] string? sourceModule,
        [FromQuery] Guid? sourceEventId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetAccountingPostingsQuery(status, sourceModule, sourceEventId),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Obtiene una obligación de contabilización con su detalle e historial de intentos.
    /// </summary>
    /// <param name="id">Identificador de la obligación.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Detalle de la obligación.</response>
    /// <response code="404">La obligación no existe en la empresa activa.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountingPostingDetailDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [HttpGet("postings/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.PostingsView}")]
    public async Task<IActionResult> GetPostingById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetAccountingPostingByIdQuery(id), ct));

    /// <summary>
    /// Reprocesa manualmente una obligación de contabilización.
    /// </summary>
    /// <remarks>
    /// Un intento manual explícito, con la configuración contable vigente y los datos económicos persistidos, en su propia transacción con el asiento. Un error contable no se traduce en un HTTP de error: la obligación queda Failed y la respuesta es 200. Si ya estaba Posted o Canceled no se ejecuta intento y devuelve su estado actual.
    /// </remarks>
    /// <param name="id">Identificador de la obligación.</param>
    /// <param name="request">Motivo del reproceso (obligatorio).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado del intento; el estado Posted o Failed de la obligación viaja en el cuerpo.</response>
    /// <response code="400">No pudo registrarse el resultado del intento: la obligación conserva su estado previo.</response>
    /// <response code="404">La obligación no existe en la empresa activa.</response>
    /// <response code="422">El motivo falta o supera el largo máximo.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountingPostingDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPost("postings/{id:guid}/reprocess")]
    [Authorize(Policy = $"perm:{AccountingPermissions.PostingsReprocess}")]
    public async Task<IActionResult> ReprocessPosting(
        Guid id,
        [FromBody] ReprocessAccountingPostingRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new ReprocessAccountingPostingCommand(id, request.Reason), ct)
        );

    /// <summary>
    /// ACCOUNTING-CONTINUITY-02 — reproceso manual en lote por IDs explícitos (máx. 100). Responde 200
    /// con el resultado individual de cada obligación, aunque alguna falle.
    /// </summary>
    /// <remarks>
    /// Los identificadores repetidos se procesan una sola vez. Un fallo individual no detiene el lote ni cambia el 200.
    /// </remarks>
    /// <param name="command">Identificadores de las obligaciones (1 a 100) y motivo obligatorio.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado individual de cada obligación del lote (intento ejecutado, sin intento, inexistente o cancelado).</response>
    /// <response code="422">Lista de identificadores vacía o con más de 100, identificador vacío, o motivo faltante o demasiado largo.</response>
    [ProducesResponseType(
        typeof(ApiResponse<AccountingPostingBatchReprocessDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status422UnprocessableEntity
    )]
    [HttpPost("postings/reprocess")]
    [Authorize(Policy = $"perm:{AccountingPermissions.PostingsReprocess}")]
    public async Task<IActionResult> ReprocessPostings(
        [FromBody] ReprocessAccountingPostingsCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct));
}
