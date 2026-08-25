namespace ERP.Application.Modules.Accounting.DTOs;

/// <summary>
/// ACCOUNTING-CHART-OF-ACCOUNTS-02: ParentAccountCode/ParentAccountName/Level se resuelven en
/// Application contra el resto del Plan de Cuentas de la Company — nunca desnormalizados en la
/// entidad (mismo criterio ya usado para AccountCode/AccountName en JournalEntryLineDto). Level
/// es 0 para una cuenta raíz (sin padre), incrementa por cada ancestro.
/// </summary>
public sealed record AccountDto(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentAccountId,
    string? ParentAccountCode,
    string? ParentAccountName,
    int Level,
    string AccountType,
    string Nature,
    bool AllowsPosting,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record AccountingPeriodDto(
    Guid Id,
    int FiscalYear,
    int PeriodNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    DateTime? ClosedAtUtc,
    Guid? ClosedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record PostingRuleDto(
    Guid Id,
    string SourceModule,
    string FactType,
    Guid? DebitAccountId,
    Guid? CreditAccountId,
    string? TaxCode,
    bool IsActive,
    IReadOnlyList<PostingRuleLineDto> Lines,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// Fase 5.6.2 — expone las líneas reales que consume JournalFactory (rule.Lines), a diferencia de
/// los campos planos legacy DebitAccountId/CreditAccountId. <c>Nature</c> es la dirección
/// (Debit/Credit) de ESTA línea dentro del asiento — no confundir con
/// <c>AccountNature</c>, la naturaleza contable propia de la cuenta referenciada (mismo criterio
/// de resolución que <see cref="JournalEntryLineDto"/>: Account* se resuelve en Application
/// contra el Plan de Cuentas de la Company, nunca se desnormaliza en <c>PostingRuleLine</c>).
/// ACCOUNTING-POSTING-RULES-UI-12: Account*/AccountIsActive/AccountAllowsPosting agregados para
/// que la UI pueda mostrar, antes de emitir un documento real, si una línea referencia una cuenta
/// que <c>PostingAccountGuard</c> rechazaría en tiempo de ejecución (inactiva o que no admite
/// asientos) — sin este dato la única forma de descubrirlo era que el primer hecho contable real
/// fallara con POSTING_ACCOUNT_INVALID.
/// </summary>
public sealed record PostingRuleLineDto(
    Guid Id,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    string AccountNature,
    bool AccountIsActive,
    bool AccountAllowsPosting,
    string Nature,
    string AmountKind,
    short SortOrder
);

/// <summary>Fase 5.4 — expone el resultado de ReverseJournalEntryCommand.</summary>
public sealed record JournalEntryDto(
    Guid Id,
    DateOnly EntryDate,
    Guid AccountingPeriodId,
    int FiscalYear,
    string SourceModule,
    string SourceEventType,
    Guid SourceEventId,
    string Description,
    string Status,
    int? EntryNumber,
    DateTime? PostedAtUtc,
    Guid? OriginalJournalEntryId,
    Guid? ReverseJournalEntryId,
    DateTime? ReversedAtUtc,
    string? ReverseReason
);

/// <summary>
/// ACCOUNTING-LEDGER-VISIBILITY-01 — fila del listado de asientos (solo lectura). TotalDebit/
/// TotalCredit se calculan a partir de <c>JournalEntry.Lines</c> (siempre balanceados en un
/// asiento Posted/Reversed por invariante de dominio, ver EnsureBalanced); en un asiento Draft
/// sin líneas aún ambos totales son 0.
/// </summary>
/// <summary>
/// ACCOUNTING-SOURCE-TRACEABILITY-04 — todos nulos si <c>IJournalEntrySourceResolver</c> no pudo
/// resolver el origen (módulo/FactType sin resolver dedicado, o documento ya no existe) — el
/// consumidor debe seguir mostrando SourceModule/SourceEventType/SourceEventId (técnicos) en ese
/// caso, nunca ocultar el asiento ni el dato crudo.
/// </summary>
public sealed record JournalEntryListItemDto(
    Guid Id,
    int? EntryNumber,
    DateOnly EntryDate,
    string SourceModule,
    string SourceEventType,
    Guid SourceEventId,
    string Description,
    decimal TotalDebit,
    decimal TotalCredit,
    string Status,
    DateTime CreatedAt,
    string? SourceDocumentType,
    string? SourceDocumentNumber,
    DateOnly? SourceDocumentDate,
    string? SourcePartyName,
    string? SourceStatus,
    string? SourceRoute
);

/// <summary>ACCOUNTING-LEDGER-VISIBILITY-01 — respuesta paginada de GetJournalEntriesQuery.</summary>
public sealed record GetJournalEntriesResponse(
    IReadOnlyList<JournalEntryListItemDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount
);

/// <summary>
/// ACCOUNTING-LEDGER-VISIBILITY-01 — línea de un asiento en el detalle. AccountCode/AccountName
/// se resuelven contra el Plan de Cuentas de la Company (mismo criterio de resolución de lookups
/// ya usado en Items para UOM/ItemType) — nunca se guardan desnormalizados en JournalEntryLine.
/// </summary>
public sealed record JournalEntryLineDto(
    Guid Id,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string? Description,
    decimal Debit,
    decimal Credit,
    short SortOrder
);

/// <summary>
/// ACCOUNTING-LEDGER-VISIBILITY-01 — detalle completo de un asiento, con líneas.
/// ACCOUNTING-REVERSALS-05: OriginalJournalEntry*/ReverseJournalEntry* (Number/Date) resuelven el
/// par de reverso para que el frontend pueda mostrar "Ver asiento relacionado" sin una segunda
/// llamada — nunca se persisten en JournalEntry, se resuelven en Application contra
/// OriginalJournalEntryId/ReverseJournalEntryId (ya existentes) en tiempo de lectura.
/// </summary>
public sealed record JournalEntryDetailDto(
    Guid Id,
    int? EntryNumber,
    DateOnly EntryDate,
    Guid AccountingPeriodId,
    int FiscalYear,
    string SourceModule,
    string SourceEventType,
    Guid SourceEventId,
    string Description,
    string Status,
    DateTime? PostedAtUtc,
    Guid? OriginalJournalEntryId,
    int? OriginalJournalEntryNumber,
    DateOnly? OriginalJournalEntryDate,
    Guid? ReverseJournalEntryId,
    int? ReverseJournalEntryNumber,
    DateOnly? ReverseJournalEntryDate,
    DateTime? ReversedAtUtc,
    string? ReverseReason,
    IReadOnlyList<JournalEntryLineDto> Lines,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    DateTime CreatedAt,
    string? SourceDocumentType,
    string? SourceDocumentNumber,
    DateOnly? SourceDocumentDate,
    string? SourcePartyName,
    string? SourceStatus,
    string? SourceRoute
);

// ── ACCOUNTING-REPORTS-09: Libro Diario / Libro Mayor / Balance de Comprobación ────────────

/// <summary>
/// Libro Diario — una línea de un asiento Posted, con la cuenta ya resuelta a Código/Nombre y el
/// origen documental resuelto vía <c>IJournalEntrySourceResolver</c> (null si no se pudo
/// resolver — el consumidor cae a SourceModule/SourceEventType/SourceEventId técnicos).
/// </summary>
public sealed record GeneralJournalLineDto(
    Guid JournalEntryId,
    int? EntryNumber,
    DateOnly EntryDate,
    string Description,
    string SourceModule,
    string SourceEventType,
    Guid SourceEventId,
    string? SourceDocumentType,
    string? SourceDocumentNumber,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal Debit,
    decimal Credit
);

public sealed record GetGeneralJournalReportResponse(
    IReadOnlyList<GeneralJournalLineDto> Lines,
    decimal TotalDebit,
    decimal TotalCredit,
    int PageNumber,
    int PageSize,
    int TotalCount
);

/// <summary>Libro Mayor — un movimiento individual dentro del detalle de una cuenta, con saldo acumulado (Kardex).</summary>
public sealed record GeneralLedgerMovementDto(
    Guid JournalEntryId,
    int? EntryNumber,
    DateOnly EntryDate,
    string Description,
    string SourceModule,
    string? SourceDocumentType,
    string? SourceDocumentNumber,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance
);

/// <summary>Libro Mayor — una cuenta con su saldo inicial/movimiento/saldo final y el detalle de movimientos del período.</summary>
public sealed record GeneralLedgerAccountDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    string Nature,
    decimal OpeningBalance,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingBalance,
    IReadOnlyList<GeneralLedgerMovementDto> Movements
);

public sealed record GetGeneralLedgerReportResponse(IReadOnlyList<GeneralLedgerAccountDto> Accounts);

/// <summary>
/// Balance de Comprobación — una fila por cuenta. Saldo inicial/final se expresan en convención
/// contable (deudor XOR acreedor, nunca ambos): si Σ Debit - Σ Credit es positivo va a la columna
/// deudora, si es negativo (en valor absoluto) va a la acreedora.
/// </summary>
public sealed record TrialBalanceLineDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit
);

public sealed record GetTrialBalanceReportResponse(
    IReadOnlyList<TrialBalanceLineDto> Lines,
    decimal TotalOpeningDebit,
    decimal TotalOpeningCredit,
    decimal TotalPeriodDebit,
    decimal TotalPeriodCredit,
    decimal TotalClosingDebit,
    decimal TotalClosingCredit,
    bool IsBalanced
);

// ── ACCOUNTING-FINANCIAL-STATEMENTS-10: Estado de Resultados / Balance General ─────────────

/// <summary>Estado de Resultados / Balance General — una línea es siempre una cuenta con su monto en convención natural (positivo si aporta a la sección, según <c>Account.Nature</c>).</summary>
public sealed record FinancialStatementLineDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal Amount
);

/// <summary>
/// Estado de Resultados — solo período (<c>FromDate</c>..<c>ToDate</c>), sin saldo inicial: las
/// cuentas de Ingresos/Costos/Gastos no arrastran saldo entre períodos en este sistema (no existe
/// cierre contable todavía, ver brecha documentada en el entregable). UtilidadBruta = TotalIncome
/// − TotalCost; UtilidadNeta = UtilidadBruta − TotalExpense.
/// </summary>
public sealed record GetIncomeStatementReportResponse(
    IReadOnlyList<FinancialStatementLineDto> IncomeLines,
    decimal TotalIncome,
    IReadOnlyList<FinancialStatementLineDto> CostLines,
    decimal TotalCost,
    decimal GrossProfit,
    IReadOnlyList<FinancialStatementLineDto> ExpenseLines,
    decimal TotalExpense,
    decimal NetProfit
);

/// <summary>
/// Balance General — saldo acumulado de cada cuenta de Activo/Pasivo/Patrimonio desde el inicio
/// del historial Posted hasta <c>AsOfDate</c> inclusive. <c>IsBalanced</c> refleja el estado real
/// del libro mayor — sin cierre contable (fuera de alcance, ver entregable) la utilidad/pérdida
/// del ejercicio en curso todavía no se traslada a Patrimonio, así que puede legítimamente mostrar
/// <c>false</c> mientras exista actividad de Ingresos/Costos/Gastos sin cerrar; eso es un estado
/// esperado del sistema hoy, no un error de cálculo de este reporte.
/// </summary>
public sealed record GetBalanceSheetReportResponse(
    IReadOnlyList<FinancialStatementLineDto> AssetLines,
    decimal TotalAssets,
    IReadOnlyList<FinancialStatementLineDto> LiabilityLines,
    decimal TotalLiabilities,
    IReadOnlyList<FinancialStatementLineDto> EquityLines,
    decimal TotalEquity,
    decimal Difference,
    bool IsBalanced
);
