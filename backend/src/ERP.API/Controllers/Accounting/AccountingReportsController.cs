using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.UseCases.Reports;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Accounting;

/// <summary>
/// Reportes contables de solo lectura (ACCOUNTING-REPORTS-09 / ACCOUNTING-FINANCIAL-
/// STATEMENTS-10) — Libro Diario, Libro Mayor, Balance de Comprobación, Estado de Resultados,
/// Balance General. Extraído de <see cref="AccountingController"/> (que ya excedía el umbral de
/// advertencia del guardrail de arquitectura) siguiendo el mismo patrón de split ya establecido
/// para Purchases (<c>PurchasesController</c> + <c>PurchaseCreditNoteController</c> +
/// <c>PurchaseReturnController</c>) — mismo prefijo de ruta base, sin <c>[AppFeature]</c> propio
/// (el nav ya lo registra <see cref="AccountingController"/>, evitar una entrada duplicada). Solo
/// lectura sobre <c>JournalEntry</c>/<c>JournalEntryLine</c> ya <c>Posted</c> — nunca recalcula
/// desde Ventas/Compras/Inventario/Finanzas, nunca modifica asientos históricos.
/// </summary>
[ApiController]
[Route("api/v1/accounting/reports")]
[Authorize]
[Produces("application/json")]
public sealed class AccountingReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountingReportsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Libro Diario: líneas de los asientos contabilizados (<c>Posted</c>) del rango, paginadas.</summary>
    /// <remarks>
    /// Solo lectura sobre asientos ya contabilizados de la empresa activa; no recalcula desde documentos
    /// operativos. La respuesta incluye totales de Debe/Haber del conjunto filtrado y la evaluación de
    /// completitud contable (<c>completeness</c>). Si <c>pageNumber</c> o <c>pageSize</c> son 0 o negativos se
    /// usan 1 y 50; <c>pageSize</c> admite de 1 a 200.
    /// </remarks>
    /// <param name="fromDate">Fecha inicial inclusive, formato <c>YYYY-MM-DD</c>.</param>
    /// <param name="toDate">Fecha final inclusive, formato <c>YYYY-MM-DD</c>; no puede ser anterior a <c>fromDate</c>.</param>
    /// <param name="sourceModule">Filtro opcional por módulo de origen del asiento.</param>
    /// <param name="search">Filtro de texto opcional.</param>
    /// <param name="pageNumber">Página (base 1).</param>
    /// <param name="pageSize">Tamaño de página (1 a 200; por defecto 50).</param>
    /// <response code="200">Página del Libro Diario.</response>
    /// <response code="422">`toDate` anterior a `fromDate`, `pageSize` fuera de 1–200 o parámetro no enlazable.</response>
    [HttpGet("general-journal")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<GetGeneralJournalReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetGeneralJournalReport(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] string? sourceModule,
        [FromQuery] string? search,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetGeneralJournalReportQuery(
                    fromDate,
                    toDate,
                    sourceModule,
                    search,
                    pageNumber <= 0 ? 1 : pageNumber,
                    pageSize <= 0 ? 50 : pageSize
                ),
                ct
            ),
            "OK"
        );

    /// <summary>Libro Mayor: saldo inicial, movimiento y saldo final con el detalle acumulado (Kardex) de una o varias cuentas.</summary>
    /// <remarks>
    /// Solo lectura sobre asientos <c>Posted</c>. Si se indica <c>accountId</c> se filtra a esa cuenta y se ignora el
    /// rango de código; si no, se filtra por <c>accountCodeFrom</c>/<c>accountCodeTo</c> (ambos opcionales); sin ningún
    /// filtro se incluyen todas las cuentas de la empresa. El saldo inicial es el acumulado hasta el día anterior a
    /// <c>fromDate</c>. Si ninguna cuenta coincide responde 200 con la lista vacía.
    /// </remarks>
    /// <param name="fromDate">Fecha inicial inclusive, formato <c>YYYY-MM-DD</c>.</param>
    /// <param name="toDate">Fecha final inclusive, formato <c>YYYY-MM-DD</c>; no puede ser anterior a <c>fromDate</c>.</param>
    /// <param name="accountId">Id de una cuenta contable; tiene prioridad sobre el rango de código.</param>
    /// <param name="accountCodeFrom">Código de cuenta inicial del rango (opcional).</param>
    /// <param name="accountCodeTo">Código de cuenta final del rango (opcional).</param>
    /// <response code="200">Libro Mayor del rango.</response>
    /// <response code="422">`toDate` anterior a `fromDate`, parámetro de consulta no enlazable o fuera de rango.</response>
    [HttpGet("general-ledger")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<GetGeneralLedgerReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetGeneralLedgerReport(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] Guid? accountId,
        [FromQuery] string? accountCodeFrom,
        [FromQuery] string? accountCodeTo,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetGeneralLedgerReportQuery(
                    fromDate,
                    toDate,
                    accountId,
                    accountCodeFrom,
                    accountCodeTo
                ),
                ct
            ),
            "OK"
        );

    /// <summary>Balance de Comprobación: saldo inicial, movimiento y saldo final por cuenta en convención deudora/acreedora.</summary>
    /// <remarks>
    /// Solo lectura sobre asientos <c>Posted</c>. Con <c>includeZeroMovementAccounts</c> en <c>false</c> (valor por
    /// defecto) solo se muestran las cuentas con saldo inicial, movimiento o saldo final distinto de cero; con
    /// <c>true</c> se agregan las cuentas del Plan de Cuentas sin actividad. La respuesta incluye totales, si el
    /// movimiento del período cuadra y la evaluación de completitud contable.
    /// </remarks>
    /// <param name="fromDate">Fecha inicial inclusive, formato <c>YYYY-MM-DD</c>.</param>
    /// <param name="toDate">Fecha final inclusive, formato <c>YYYY-MM-DD</c>; no puede ser anterior a <c>fromDate</c>.</param>
    /// <param name="includeZeroMovementAccounts">Incluye también las cuentas sin actividad en el rango.</param>
    /// <response code="200">Balance de Comprobación del rango.</response>
    /// <response code="422">`toDate` anterior a `fromDate`, parámetro de consulta no enlazable o fuera de rango.</response>
    [HttpGet("trial-balance")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<GetTrialBalanceReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetTrialBalanceReport(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] bool includeZeroMovementAccounts,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetTrialBalanceReportQuery(fromDate, toDate, includeZeroMovementAccounts),
                ct
            ),
            "OK"
        );

    /// <summary>Estado de Resultados: totales por cuenta de Ingreso, Costo y Gasto en el rango.</summary>
    /// <remarks>
    /// Solo lectura: suma Debe y Haber de los asientos <c>Posted</c> del rango (los reversos contabilizados cuentan
    /// como movimiento normal; los borradores y descartados se excluyen). No arrastra saldo inicial entre rangos.
    /// </remarks>
    /// <param name="fromDate">Fecha inicial inclusive, formato <c>YYYY-MM-DD</c>.</param>
    /// <param name="toDate">Fecha final inclusive, formato <c>YYYY-MM-DD</c>; no puede ser anterior a <c>fromDate</c>.</param>
    /// <response code="200">Estado de Resultados del rango.</response>
    /// <response code="422">`toDate` anterior a `fromDate`, parámetro de consulta no enlazable o fuera de rango.</response>
    [HttpGet("income-statement")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<GetIncomeStatementReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetIncomeStatementReport(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetIncomeStatementReportQuery(fromDate, toDate), ct),
            "OK"
        );

    /// <summary>Balance General: saldo acumulado de las cuentas de Activo, Pasivo y Patrimonio a una fecha.</summary>
    /// <remarks>
    /// Solo lectura. Acumula desde el inicio del historial <c>Posted</c> hasta <c>asOfDate</c> inclusive y añade, como
    /// valores calculados (nunca un asiento persistido), el resultado del ejercicio en curso (año calendario) y los
    /// resultados de ejercicios anteriores aún no cerrados en cuentas nominales; la respuesta expone la diferencia
    /// entre Activo y Pasivo más Patrimonio.
    /// </remarks>
    /// <param name="asOfDate">Fecha de corte inclusive, formato <c>YYYY-MM-DD</c>; es obligatoria.</param>
    /// <response code="200">Balance General a la fecha.</response>
    /// <response code="422">`asOfDate` ausente o con valor vacío, o no enlazable.</response>
    [HttpGet("balance-sheet")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<GetBalanceSheetReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetBalanceSheetReport(
        [FromQuery] DateOnly asOfDate,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetBalanceSheetReportQuery(asOfDate), ct),
            "OK"
        );
}
