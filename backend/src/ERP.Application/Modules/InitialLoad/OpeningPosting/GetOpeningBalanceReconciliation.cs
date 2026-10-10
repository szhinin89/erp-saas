using System.Globalization;
using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.Interfaces;
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
}

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
    bool CanCloseImplementation,
    IReadOnlyList<OpeningReconciliationBlockerDto> Blockers
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

    private readonly IOperationalContext _ctx;
    private readonly IImportBatchRepository _batches;
    private readonly IOpeningBalancePostingRepository _postings;
    private readonly IOpeningBalanceConstraintsReader _openingBalance;
    private readonly IOpeningBalanceSourceReader _sources;
    private readonly IPostingRuleRepository _rules;
    private readonly IAccountRepository _accounts;
    private readonly IJournalEntryRepository _journal;

    public GetOpeningBalanceReconciliationHandler(
        IOperationalContext ctx,
        IImportBatchRepository batches,
        IOpeningBalancePostingRepository postings,
        IOpeningBalanceConstraintsReader openingBalance,
        IOpeningBalanceSourceReader sources,
        IPostingRuleRepository rules,
        IAccountRepository accounts,
        IJournalEntryRepository journal
    )
    {
        _ctx = ctx;
        _batches = batches;
        _postings = postings;
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

        var blockers = Blockers(cutoff, batchRows, typeRows, bridge);
        var overall = batchRows.Any(b => b.Status == OpeningReconciliationStatus.PendingPosting)
            || typeRows.Any(t => t.Status == OpeningReconciliationStatus.PendingPosting)
            ? OpeningReconciliationStatus.PendingPosting
            : batchRows.Any(b => b.Status == OpeningReconciliationStatus.Difference)
                || typeRows.Any(t => t.Status == OpeningReconciliationStatus.Difference)
                ? OpeningReconciliationStatus.Difference
                : OpeningReconciliationStatus.Reconciled;

        return Result<OpeningBalanceReconciliationDto>.Success(new OpeningBalanceReconciliationDto(
            cutoff,
            overall,
            batchRows.OrderBy(b => Array.IndexOf(BalanceTypes, b.ImportType)).ThenBy(b => b.ConfirmedAt).ToList(),
            typeRows,
            bridge,
            blockers.Count == 0,
            blockers));
    }

    /// <summary>
    /// Regla de cierre (IL-8): ningún lote Pending/Failed/sin asiento, ninguna diferencia de
    /// conciliación y la cuenta puente sin saldo (redondeo monetario vigente). Un saldo en la cuenta
    /// puente solo indica que falta reclasificarlo (<see cref="OpeningBridgeNotClearedCode"/>); no
    /// se afirma que falte un asiento manual concreto — aún no existe modelo de ASI de apertura.
    /// </summary>
    private static List<OpeningReconciliationBlockerDto> Blockers(
        DateOnly? cutoff,
        IReadOnlyList<OpeningReconciliationBatchDto> batches,
        IReadOnlyList<OpeningReconciliationTypeDto> types,
        OpeningBridgeAccountDto? bridge)
    {
        var blockers = new List<OpeningReconciliationBlockerDto>();
        if (cutoff is null)
            blockers.Add(new("OPENING_DATE_MISSING", "La empresa no tiene definida su fecha de apertura de saldos.", null));

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

        if (bridge is not null && OpeningBalancePosting.RoundAmount(bridge.PendingReclassification) != 0m)
            blockers.Add(new(OpeningBridgeNotClearedCode,
                "La cuenta de Saldos de apertura mantiene un saldo pendiente de reclasificación.",
                null));
        return blockers;
    }
}
