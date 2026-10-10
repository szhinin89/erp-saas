using System.Globalization;
using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.InitialLoad.Constants;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.OpeningPosting;

/// <summary>IL-7C — estado de conciliación de un lote, de un tipo de saldo o del conjunto.</summary>
public enum OpeningReconciliationStatus
{
    /// <summary>Contabilizado y submayor = mayor.</summary>
    Reconciled = 1,

    /// <summary>Contabilizado (o sin nada pendiente) pero submayor ≠ mayor.</summary>
    Difference = 2,

    /// <summary>Sin asiento todavía: sin estado, Pending o Failed (ver error del intento).</summary>
    PendingPosting = 3,

    /// <summary>
    /// IL-8C — lotes contabilizados y conciliados, pero sin versión vigente del ASI de apertura
    /// publicada (nunca publicado, Pending/Failed o reversado sin reemplazo).
    /// </summary>
    OpeningJournalPending = 4,
}

/// <summary>
/// IL-8C — estado del ASI de apertura, derivado SOLO de <see cref="OpeningJournalEntryPosting"/>
/// (nunca del saldo de la cuenta puente).
/// </summary>
public enum OpeningJournalEntryState
{
    /// <summary>La empresa nunca tuvo ninguna versión del ASI.</summary>
    Missing = 1,

    /// <summary>Versión vigente aún sin intento resuelto (defensivo: Publish la deja Failed o Posted).</summary>
    Pending = 2,

    /// <summary>Versión vigente con el último intento fallido (reintentable).</summary>
    Failed = 3,

    /// <summary>Versión vigente publicada: la única que cuenta para el cierre.</summary>
    Posted = 4,

    /// <summary>Sin versión vigente: la última publicada fue reversada y no se publicó otra.</summary>
    ReversedNotReplaced = 5,
}

/// <summary>
/// IL-8C — ASI de apertura dentro de la conciliación. Los datos de la versión
/// (<see cref="PostingId"/>…<see cref="ErrorMessage"/>) son los de la vigente; null si no hay vigente.
/// <see cref="LastSupersededVersion"/>/<see cref="LastSupersededAt"/> describen la última versión
/// reversada (historial), si existe.
/// </summary>
public sealed record OpeningReconciliationJournalEntryDto(
    OpeningJournalEntryState State,
    Guid? PostingId,
    int? Version,
    OpeningBalancePostingStatus? PostingStatus,
    DateOnly? EntryDate,
    decimal? TotalAmount,
    int? LineCount,
    Guid? JournalEntryId,
    int? JournalEntryNumber,
    DateTime? PostedAt,
    int Attempts,
    string? ErrorCode,
    string? ErrorMessage,
    int VersionCount,
    int? LastSupersededVersion,
    DateTime? LastSupersededAt
);

public sealed record OpeningReconciliationBatchDto(
    Guid ImportBatchId,
    ImportType ImportType,
    string FactType,
    string? Label,
    ImportStatus BatchStatus,
    DateTime? ConfirmedAt,
    decimal OperationalAmount,
    decimal AccountingAmount,
    decimal Difference,
    OpeningReconciliationStatus Status,
    OpeningBalancePostingStatus? PostingStatus,
    Guid? JournalEntryId,
    int? JournalEntryNumber,
    string? ErrorCode,
    string? ErrorMessage,
    bool CanPost
);

/// <summary>
/// Submayor (Σ montos operativos confirmados de los lotes del tipo) contra el saldo del mayor de la
/// cuenta de control al corte. <see cref="LedgerBalance"/> es null si la cuenta no se puede
/// resolver (sin regla <c>InitialLoad/FactType</c>) o falta la fecha de corte.
/// </summary>
public sealed record OpeningReconciliationTypeDto(
    ImportType ImportType,
    string FactType,
    Guid? AccountId,
    string? AccountCode,
    string? AccountName,
    int BatchCount,
    decimal OperationalAmount,
    decimal? LedgerBalance,
    decimal? Difference,
    OpeningReconciliationStatus Status
);

/// <summary>
/// Cuenta puente "Saldos de apertura" (contrapartida de las reglas <c>InitialLoad/*</c>), en saldo
/// acreedor. <see cref="FromOpeningPostings"/> es lo que aportaron los asientos de apertura;
/// <see cref="PendingReclassification"/> = <see cref="CurrentBalance"/>: lo que aún debe moverse a
/// patrimonio definitivo (bloquea el cierre mientras sea distinto de cero).
/// </summary>
public sealed record OpeningBridgeAccountDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal? BalanceAtCutoff,
    decimal CurrentBalance,
    decimal FromOpeningPostings,
    decimal PendingReclassification
);

public sealed record OpeningReconciliationBlockerDto(string Code, string Message, Guid? ImportBatchId);

public sealed record OpeningBalanceReconciliationDto(
    DateOnly? CutoffDate,
    OpeningReconciliationStatus Status,
    IReadOnlyList<OpeningReconciliationBatchDto> Batches,
    IReadOnlyList<OpeningReconciliationTypeDto> Types,
    OpeningBridgeAccountDto? BridgeAccount,
    OpeningReconciliationJournalEntryDto OpeningJournalEntry,
    bool CanCloseImplementation,
    IReadOnlyList<OpeningReconciliationBlockerDto> Blockers,
    bool IsClosed,
    DateTime? ClosedAt,
    Guid? ClosedBy,
    string? ClosedByName
);

/// <summary>
/// IL-7C — conciliación de la apertura contable contra los saldos operativos confirmados de la
/// empresa activa, al corte <c>Company.OpeningBalanceDate</c>. SOLO LECTURA: no abre transacción,
/// no usa unidad de trabajo ni guarda nada.
/// <list type="bullet">
/// <item>Monto operativo por lote: <see cref="IOpeningBalanceSourceReader"/> (Kardex
/// <c>InitialBalance</c>, CxC/CxP <c>InitialBalance</c> confirmadas; nunca staging/Excel), con el
/// redondeo monetario de <see cref="OpeningBalancePosting.RoundAmount"/>.</item>
/// <item>Monto contable por lote: líneas de la cuenta de control en los asientos
/// <c>InitialLoad</c> del lote (Posted/Reversed, mismo criterio neto que Mayor/Balance).</item>
/// <item>Mayor por tipo y cuenta puente: <see cref="IJournalEntryRepository.GetAccountLineTotalsAsync"/>,
/// la misma fuente de Libro Mayor / Balance de Comprobación.</item>
/// </list>
/// Cuenta de control y cuenta puente salen de la <c>PostingRule InitialLoad/FactType</c> de la
/// empresa (nunca códigos fijos): la línea del lado de control (Debe para inventario/CxC, Haber para
/// CxP) y su contrapartida. Diferencia = submayor − mayor. <see cref="OpeningBalanceReconciliationDto.Blockers"/>
/// es el contrato que IL-8 consume para impedir el cierre de la implementación.
/// </summary>
public sealed record GetOpeningBalanceReconciliationQuery
    : IRequest<Result<OpeningBalanceReconciliationDto>>,
        ICompanyScopedRequest;

public sealed class GetOpeningBalanceReconciliationHandler
    : IRequestHandler<GetOpeningBalanceReconciliationQuery, Result<OpeningBalanceReconciliationDto>>
{
    private static readonly ImportType[] BalanceTypes =
    [
        ImportType.InitialStock,
        ImportType.InitialReceivables,
        ImportType.InitialPayables,
    ];

    // Un PartiallyCompleted también dejó saldos confirmados en el dominio (el submayor los incluye),
    // aunque el preflight no lo deje contabilizar.
    private static readonly ImportStatus[] ConfirmedStatuses =
    [
        ImportStatus.Completed,
        ImportStatus.PartiallyCompleted,
    ];

    public const string OpeningBridgeNotClearedCode = "OPENING_BRIDGE_NOT_CLEARED";
    public const string OpeningAsiMissingCode = "OPENING_ASI_MISSING";
    public const string OpeningAsiReversedNotReplacedCode = "OPENING_ASI_REVERSED_NOT_REPLACED";
    public const string OpeningAsiFailedCode = "OPENING_ASI_FAILED";
    public const string OpeningAsiPendingCode = "OPENING_ASI_PENDING";
    public const string OpeningBatchInProgressCode = "OPENING_BATCH_IN_PROGRESS";

    /// <summary>
    /// IL-8E — lotes de saldos en curso (con archivo, en validación, validados o confirmándose): podrían
    /// confirmarse después; deben confirmarse o cancelarse antes del cierre. Draft (sin archivo),
    /// Failed y Cancelled no dejaron saldos y quedan bloqueados por el cierre.
    /// </summary>
    private static readonly ImportStatus[] InProgressStatuses =
    [
        ImportStatus.Uploaded,
        ImportStatus.Validating,
        ImportStatus.Validated,
        ImportStatus.Confirming,
    ];

    private readonly IOperationalContext _ctx;
    private readonly IImportBatchRepository _batches;
    private readonly IOpeningBalancePostingRepository _postings;
    private readonly IOpeningJournalEntryPostingRepository _openingJournal;
    private readonly IOpeningBalanceConstraintsReader _openingBalance;
    private readonly IOpeningBalanceSourceReader _sources;
    private readonly IPostingRuleRepository _rules;
    private readonly IAccountRepository _accounts;
    private readonly IJournalEntryRepository _journal;
    private readonly IAccessRepository _access;

    public GetOpeningBalanceReconciliationHandler(
        IOperationalContext ctx,
        IImportBatchRepository batches,
        IOpeningBalancePostingRepository postings,
        IOpeningJournalEntryPostingRepository openingJournal,
        IOpeningBalanceConstraintsReader openingBalance,
        IOpeningBalanceSourceReader sources,
        IPostingRuleRepository rules,
        IAccountRepository accounts,
        IJournalEntryRepository journal,
        IAccessRepository access
    )
    {
        _access = access;
        _ctx = ctx;
        _batches = batches;
        _postings = postings;
        _openingJournal = openingJournal;
        _openingBalance = openingBalance;
        _sources = sources;
        _rules = rules;
        _accounts = accounts;
        _journal = journal;
    }

    /// <summary>Lado de la cuenta de control en la regla de cada hecho de apertura.</summary>
    private static AccountNature ControlSide(string factType) =>
        factType == OpeningBalancePostingFacts.OpeningPayables ? AccountNature.Credit : AccountNature.Debit;

    private static decimal Natural(AccountNature side, decimal debit, decimal credit) =>
        side == AccountNature.Debit ? debit - credit : credit - debit;

    private static string Money(decimal? value) =>
        (value ?? 0m).ToString("F2", CultureInfo.InvariantCulture);

    private sealed record RuleAccounts(Guid? ControlAccountId, Guid? BridgeAccountId);

    public async Task<Result<OpeningBalanceReconciliationDto>> Handle(
        GetOpeningBalanceReconciliationQuery query,
        CancellationToken ct
    )
    {
        var tenantId = _ctx.TenantId;
        var companyId = _ctx.CompanyId;

        var cutoff = await _openingBalance.GetOpeningBalanceDateAsync(ct);
        var batches = await _batches.ListAsync(tenantId, companyId, BalanceTypes, ConfirmedStatuses, ct);
        var postings = (await _postings.ListByCompanyAsync(tenantId, companyId, ct))
            .ToDictionary(p => p.ImportBatchId);
        var accountsById = (await _accounts.GetByCompanyAsync(tenantId, companyId, ct))
            .ToDictionary(a => a.Id);

        var ruleAccounts = new Dictionary<string, RuleAccounts>(StringComparer.Ordinal);
        foreach (var type in BalanceTypes)
        {
            var factType = OpeningBalancePostingFacts.ForImportType(type)!;
            var rule = await _rules.FindByKeyAsync(
                tenantId, companyId, OpeningBalancePostingFacts.SourceModule, factType, ct);
            var side = ControlSide(factType);
            ruleAccounts[factType] = new RuleAccounts(
                rule?.Lines.Where(l => l.Nature == side).Select(l => (Guid?)l.AccountId).FirstOrDefault(),
                rule?.Lines.Where(l => l.Nature != side).Select(l => (Guid?)l.AccountId).FirstOrDefault());
        }

        var bridgeAccountId = ruleAccounts.Values
            .Select(r => r.BridgeAccountId)
            .FirstOrDefault(id => id is not null);

        var batchRows = new List<OpeningReconciliationBatchDto>();
        var bridgeFromPostings = 0m;
        foreach (var batch in batches)
        {
            var factType = OpeningBalancePostingFacts.ForImportType(batch.ImportType)!;
            var side = ControlSide(factType);
            var controlId = ruleAccounts[factType].ControlAccountId;
            postings.TryGetValue(batch.Id, out var posting);

            var source = await _sources.GetConfirmedSourceAsync(batch.Id, batch.ImportType, ct);
            var operational = OpeningBalancePosting.RoundAmount(source.RawAmount);

            var entries = (await _journal.GetBySourceAsync(
                    tenantId, companyId, OpeningBalancePostingFacts.SourceModule, batch.Id, ct))
                .Where(e => e.Status is JournalEntryStatus.Posted or JournalEntryStatus.Reversed)
                .ToList();
            var lines = entries.SelectMany(e => e.Lines).ToList();
            var accounting = lines
                .Where(l => l.AccountId == controlId)
                .Sum(l => Natural(side, l.Debit, l.Credit));
            if (bridgeAccountId is not null)
                bridgeFromPostings += lines
                    .Where(l => l.AccountId == bridgeAccountId)
                    .Sum(l => l.Credit - l.Debit);

            var isPosted = posting?.Status == OpeningBalancePostingStatus.Posted;
            var difference = operational - accounting;
            var status = !isPosted
                ? OpeningReconciliationStatus.PendingPosting
                : difference == 0m
                    ? OpeningReconciliationStatus.Reconciled
                    : OpeningReconciliationStatus.Difference;
            var journalEntry = posting?.JournalEntryId is { } jeId
                ? entries.FirstOrDefault(e => e.Id == jeId)
                : null;

            batchRows.Add(new OpeningReconciliationBatchDto(
                batch.Id,
                batch.ImportType,
                factType,
                batch.Label,
                batch.Status,
                batch.ConfirmedAt,
                operational,
                accounting,
                difference,
                status,
                posting?.Status,
                posting?.JournalEntryId,
                journalEntry?.EntryNumber,
                posting?.Status == OpeningBalancePostingStatus.Failed ? posting.ErrorCode : null,
                posting?.Status == OpeningBalancePostingStatus.Failed ? posting.ErrorMessage : null,
                CanPost: !isPosted && batch.Status == ImportStatus.Completed));
        }

        var controlIds = ruleAccounts.Values
            .Select(r => r.ControlAccountId)
            .Append(bridgeAccountId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var atCutoff = cutoff is null || controlIds.Count == 0
            ? null
            : await _journal.GetAccountLineTotalsAsync(tenantId, companyId, null, cutoff, controlIds, ct);

        var typeRows = new List<OpeningReconciliationTypeDto>();
        foreach (var type in BalanceTypes)
        {
            var factType = OpeningBalancePostingFacts.ForImportType(type)!;
            var side = ControlSide(factType);
            var controlId = ruleAccounts[factType].ControlAccountId;
            var ofType = batchRows.Where(b => b.ImportType == type).ToList();
            var operational = ofType.Sum(b => b.OperationalAmount);

            decimal? ledger = null;
            if (controlId is { } id && atCutoff is not null)
            {
                atCutoff.TryGetValue(id, out var totals);
                ledger = Natural(side, totals.TotalDebit, totals.TotalCredit);
            }
            var difference = operational - ledger;
            var status = ofType.Any(b => b.Status == OpeningReconciliationStatus.PendingPosting)
                ? OpeningReconciliationStatus.PendingPosting
                : ofType.Count == 0 && ledger is null
                    ? OpeningReconciliationStatus.Reconciled
                    : difference == 0m
                        ? OpeningReconciliationStatus.Reconciled
                        : OpeningReconciliationStatus.Difference;

            accountsById.TryGetValue(controlId ?? Guid.Empty, out var account);
            typeRows.Add(new OpeningReconciliationTypeDto(
                type, factType, controlId, account?.Code.Value, account?.Name,
                ofType.Count, operational, ledger, difference, status));
        }

        OpeningBridgeAccountDto? bridge = null;
        if (bridgeAccountId is { } bridgeId && accountsById.TryGetValue(bridgeId, out var bridgeAccount))
        {
            var current = await _journal.GetAccountLineTotalsAsync(
                tenantId, companyId, null, null, [bridgeId], ct);
            current.TryGetValue(bridgeId, out var now);
            decimal? balanceAtCutoff = null;
            if (atCutoff is not null)
            {
                atCutoff.TryGetValue(bridgeId, out var cut);
                balanceAtCutoff = cut.TotalCredit - cut.TotalDebit;
            }
            var currentBalance = now.TotalCredit - now.TotalDebit;
            bridge = new OpeningBridgeAccountDto(
                bridgeId, bridgeAccount.Code.Value, bridgeAccount.Name,
                balanceAtCutoff, currentBalance, bridgeFromPostings, currentBalance);
        }

        var openingJournal = await OpeningJournalAsync(tenantId, companyId, ct);
        var inProgress = await _batches.ListAsync(tenantId, companyId, BalanceTypes, InProgressStatuses, ct);
        var blockers = Blockers(cutoff, batchRows, typeRows, bridge, openingJournal, inProgress);
        var closure = await _openingBalance.GetInitialLoadClosureAsync(ct);
        string? closedByName = null;
        if (closure?.ClosedBy is { } closedBy)
            closedByName = (await _access.GetUsersByIdsAsync([closedBy], ct)).FirstOrDefault()?.FullName;
        var overall = batchRows.Any(b => b.Status == OpeningReconciliationStatus.PendingPosting)
            || typeRows.Any(t => t.Status == OpeningReconciliationStatus.PendingPosting)
            ? OpeningReconciliationStatus.PendingPosting
            : batchRows.Any(b => b.Status == OpeningReconciliationStatus.Difference)
                || typeRows.Any(t => t.Status == OpeningReconciliationStatus.Difference)
                ? OpeningReconciliationStatus.Difference
                : openingJournal.State != OpeningJournalEntryState.Posted
                    ? OpeningReconciliationStatus.OpeningJournalPending
                    : OpeningReconciliationStatus.Reconciled;

        return Result<OpeningBalanceReconciliationDto>.Success(new OpeningBalanceReconciliationDto(
            cutoff,
            overall,
            batchRows.OrderBy(b => Array.IndexOf(BalanceTypes, b.ImportType)).ThenBy(b => b.ConfirmedAt).ToList(),
            typeRows,
            bridge,
            openingJournal,
            closure is null && blockers.Count == 0,
            blockers,
            closure is not null,
            closure?.ClosedAt,
            closure?.ClosedBy,
            closedByName));
    }

    /// <summary>
    /// IL-8C — estado del ASI de apertura desde <see cref="OpeningJournalEntryPosting"/> (SSOT): la
    /// versión vigente define el estado; sin vigente, el historial distingue "nunca publicado" de
    /// "reversado sin reemplazo". Una versión histórica nunca cuenta como vigente.
    /// </summary>
    private async Task<OpeningReconciliationJournalEntryDto> OpeningJournalAsync(
        Guid tenantId, Guid companyId, CancellationToken ct)
    {
        var versions = await _openingJournal.ListByCompanyAsync(tenantId, companyId, ct);
        var current = versions.FirstOrDefault(v => v.IsCurrent);
        var superseded = versions.FirstOrDefault(v => !v.IsCurrent);
        var state = current?.Status switch
        {
            OpeningBalancePostingStatus.Posted => OpeningJournalEntryState.Posted,
            OpeningBalancePostingStatus.Failed => OpeningJournalEntryState.Failed,
            OpeningBalancePostingStatus.Pending => OpeningJournalEntryState.Pending,
            _ => superseded is null ? OpeningJournalEntryState.Missing : OpeningJournalEntryState.ReversedNotReplaced,
        };
        var entry = current?.JournalEntryId is { } entryId
            ? await _journal.GetByIdAsync(tenantId, companyId, entryId, ct)
            : null;
        var failed = state == OpeningJournalEntryState.Failed;

        return new OpeningReconciliationJournalEntryDto(
            state,
            current?.Id,
            current?.Version,
            current?.Status,
            current?.EntryDate,
            current?.TotalAmount,
            current?.LineCount,
            current?.JournalEntryId,
            entry?.EntryNumber,
            current?.PostedAt,
            current?.Attempts ?? 0,
            failed ? current!.ErrorCode : null,
            failed ? current!.ErrorMessage : null,
            versions.Count,
            superseded?.Version,
            superseded?.SupersededAt);
    }

    /// <summary>
    /// Regla de cierre (IL-8): ningún lote de saldos en curso (IL-8E), ningún lote Pending/Failed/sin asiento, ninguna diferencia de
    /// conciliación, versión vigente del ASI de apertura Posted (IL-8C, derivado solo de
    /// <see cref="OpeningJournalEntryPosting"/>) y la cuenta puente sin saldo (redondeo monetario
    /// vigente). <see cref="OpeningBridgeNotClearedCode"/> significa SOLO puente distinto de cero:
    /// nunca se usa para inferir el estado del ASI, y ambos blockers pueden coexistir.
    /// </summary>
    private static List<OpeningReconciliationBlockerDto> Blockers(
        DateOnly? cutoff,
        IReadOnlyList<OpeningReconciliationBatchDto> batches,
        IReadOnlyList<OpeningReconciliationTypeDto> types,
        OpeningBridgeAccountDto? bridge,
        OpeningReconciliationJournalEntryDto openingJournal,
        IReadOnlyList<ImportBatch> inProgress)
    {
        var blockers = new List<OpeningReconciliationBlockerDto>();
        if (cutoff is null)
            blockers.Add(new("OPENING_DATE_MISSING", "La empresa no tiene definida su fecha de apertura de saldos.", null));

        foreach (var b in inProgress)
            blockers.Add(new(OpeningBatchInProgressCode,
                $"El lote de saldos{(b.Label is { Length: > 0 } label ? $" «{label}»" : "")} está en curso sin "
                    + "confirmar: confírmelo o cancélelo antes de cerrar la carga inicial.",
                b.Id));

        foreach (var b in batches.Where(b => b.Status == OpeningReconciliationStatus.PendingPosting))
            blockers.Add(b.PostingStatus == OpeningBalancePostingStatus.Failed
                ? new("OPENING_POSTING_FAILED",
                    $"La contabilización de apertura del lote falló: {b.ErrorMessage}", b.ImportBatchId)
                : new("OPENING_POSTING_PENDING",
                    "El lote tiene saldos confirmados sin asiento de apertura.", b.ImportBatchId));

        foreach (var b in batches.Where(b => b.Status == OpeningReconciliationStatus.Difference))
            blockers.Add(new("RECONCILIATION_DIFFERENCE",
                $"Diferencia de {Money(b.Difference)} entre el saldo operativo y el asiento del lote.", b.ImportBatchId));

        foreach (var t in types.Where(t => t.Status == OpeningReconciliationStatus.Difference))
            blockers.Add(new("RECONCILIATION_DIFFERENCE",
                t.LedgerBalance is null
                    ? $"No se puede conciliar {t.FactType}: falta la regla contable o la fecha de corte."
                    : $"Diferencia de {Money(t.Difference)} entre el submayor y la cuenta {t.AccountCode} al corte.",
                null));

        var asiBlocker = openingJournal.State switch
        {
            OpeningJournalEntryState.Missing => new OpeningReconciliationBlockerDto(OpeningAsiMissingCode,
                "No se ha publicado el asiento de apertura (ASI) de la empresa.", null),
            OpeningJournalEntryState.ReversedNotReplaced => new OpeningReconciliationBlockerDto(
                OpeningAsiReversedNotReplacedCode,
                $"El asiento de apertura (versión {openingJournal.LastSupersededVersion}) fue reversado y no se ha "
                    + "publicado una versión nueva: la apertura está incompleta.", null),
            OpeningJournalEntryState.Failed => new OpeningReconciliationBlockerDto(OpeningAsiFailedCode,
                $"La publicación del asiento de apertura (versión {openingJournal.Version}) falló: {openingJournal.ErrorMessage}",
                null),
            OpeningJournalEntryState.Pending => new OpeningReconciliationBlockerDto(OpeningAsiPendingCode,
                $"El asiento de apertura (versión {openingJournal.Version}) está pendiente de publicación.", null),
            _ => null,
        };
        if (asiBlocker is not null)
            blockers.Add(asiBlocker);

        if (bridge is not null && OpeningBalancePosting.RoundAmount(bridge.PendingReclassification) != 0m)
            blockers.Add(new(OpeningBridgeNotClearedCode,
                "La cuenta de Saldos de apertura mantiene un saldo pendiente de reclasificación.",
                null));
        return blockers;
    }
}
