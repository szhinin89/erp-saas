using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.Queries;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Accounting.UseCases.Reports;

// ── Queries ─────────────────────────────────────────────────────────────

/// <summary>
/// ACCOUNTING-REPORTS-09: Libro Diario — todas las líneas de asientos Posted en el rango, solo
/// lectura, sin ningún recálculo desde documentos operativos (fuente única: JournalEntry/
/// JournalEntryLine ya contabilizados por el Posting Engine). Paginado porque el rango puede
/// crecer mucho (mismo criterio que GetJournalEntriesQuery).
/// </summary>
public sealed record GetGeneralJournalReportQuery(
    DateOnly FromDate,
    DateOnly ToDate,
    string? SourceModule = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 50
) : IRequest<Result<GetGeneralJournalReportResponse>>, ICompanyScopedRequest;

/// <summary>
/// ACCOUNTING-REPORTS-09: Libro Mayor — saldo inicial/movimiento/saldo final y detalle Kardex de
/// una o varias cuentas. Si <see cref="AccountId"/> se especifica, filtra a esa única cuenta
/// (ignora el rango de código); si no, filtra por rango de código
/// (<see cref="AccountCodeFrom"/>/<see cref="AccountCodeTo"/>, ambos opcionales); sin ningún
/// filtro, incluye todas las cuentas de la Company.
/// </summary>
public sealed record GetGeneralLedgerReportQuery(
    DateOnly FromDate,
    DateOnly ToDate,
    Guid? AccountId = null,
    string? AccountCodeFrom = null,
    string? AccountCodeTo = null
) : IRequest<Result<GetGeneralLedgerReportResponse>>, ICompanyScopedRequest;

/// <summary>
/// ACCOUNTING-REPORTS-09: Balance de Comprobación — saldo inicial/movimiento/saldo final por
/// cuenta, en convención deudora/acreedora. <see cref="IncludeZeroMovementAccounts"/> = false
/// (default) muestra solo cuentas con saldo inicial, movimiento o saldo final distinto de cero;
/// true agrega también las cuentas del Plan de Cuentas sin ninguna actividad en el rango.
/// </summary>
public sealed record GetTrialBalanceReportQuery(
    DateOnly FromDate,
    DateOnly ToDate,
    bool IncludeZeroMovementAccounts = false
) : IRequest<Result<GetTrialBalanceReportResponse>>, ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class GetGeneralJournalReportQueryValidator
    : AbstractValidator<GetGeneralJournalReportQuery>
{
    public GetGeneralJournalReportQueryValidator()
    {
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate);
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class GetGeneralLedgerReportQueryValidator
    : AbstractValidator<GetGeneralLedgerReportQuery>
{
    public GetGeneralLedgerReportQueryValidator()
    {
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate);
    }
}

public sealed class GetTrialBalanceReportQueryValidator
    : AbstractValidator<GetTrialBalanceReportQuery>
{
    public GetTrialBalanceReportQueryValidator()
    {
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate);
    }
}

// ── Query Handlers ──────────────────────────────────────────────────────

public sealed class GetGeneralJournalReportHandler
    : IRequestHandler<GetGeneralJournalReportQuery, Result<GetGeneralJournalReportResponse>>
{
    private readonly IJournalEntryRepository _repo;
    private readonly IAccountRepository _accountRepo;
    private readonly IJournalEntrySourceResolver _sourceResolver;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly IAccountingCompletenessEvaluator _completeness;

    public GetGeneralJournalReportHandler(
        IJournalEntryRepository repo,
        IAccountRepository accountRepo,
        IJournalEntrySourceResolver sourceResolver,
        ICurrentTenant t,
        ICurrentCompany c,
        IAccountingCompletenessEvaluator completeness
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _sourceResolver = sourceResolver;
        _t = t;
        _c = c;
        _completeness = completeness;
    }

    public async Task<Result<GetGeneralJournalReportResponse>> Handle(
        GetGeneralJournalReportQuery q,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var (items, totalCount) = await _repo.GetPostedEntriesPageAsync(
            tenantId,
            companyId,
            q.FromDate,
            q.ToDate,
            q.SourceModule,
            q.Search,
            q.PageNumber,
            q.PageSize,
            ct
        );

        var accounts = await _accountRepo.GetByCompanyAsync(tenantId, companyId, ct);
        var accountsById = accounts.ToDictionary(a => a.Id);

        var sources = await _sourceResolver.ResolveManyAsync(
            tenantId,
            companyId,
            items
                .Select(e => new JournalEntrySourceRequest(
                    e.Id,
                    e.SourceModule,
                    e.SourceEventType,
                    e.SourceEventId
                ))
                .ToList(),
            ct
        );

        var lines = new List<GeneralJournalLineDto>();
        foreach (var entry in items)
        {
            sources.TryGetValue(entry.Id, out var source);
            foreach (var line in entry.Lines.OrderBy(l => l.SortOrder))
            {
                accountsById.TryGetValue(line.AccountId, out var account);
                lines.Add(
                    new GeneralJournalLineDto(
                        entry.Id,
                        entry.EntryNumber,
                        entry.EntryDate,
                        entry.Description,
                        entry.SourceModule,
                        entry.SourceEventType,
                        entry.SourceEventId,
                        source?.SourceDocumentType,
                        source?.SourceDocumentNumber,
                        line.AccountId,
                        account?.Code.Value ?? "—",
                        account?.Name ?? "(cuenta no encontrada)",
                        line.Debit,
                        line.Credit
                    )
                );
            }
        }

        var response = new GetGeneralJournalReportResponse(
            lines,
            lines.Sum(l => l.Debit),
            lines.Sum(l => l.Credit),
            q.PageNumber,
            q.PageSize,
            totalCount,
            await _completeness.EvaluateAsync(_t.TenantId, _c.CompanyId, q.FromDate, q.ToDate, ct)
        );
        return Result<GetGeneralJournalReportResponse>.Success(response);
    }
}

public sealed class GetGeneralLedgerReportHandler
    : IRequestHandler<GetGeneralLedgerReportQuery, Result<GetGeneralLedgerReportResponse>>
{
    private readonly IJournalEntryRepository _repo;
    private readonly IAccountRepository _accountRepo;
    private readonly IJournalEntrySourceResolver _sourceResolver;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly IAccountingCompletenessEvaluator _completeness;

    public GetGeneralLedgerReportHandler(
        IJournalEntryRepository repo,
        IAccountRepository accountRepo,
        IJournalEntrySourceResolver sourceResolver,
        ICurrentTenant t,
        ICurrentCompany c,
        IAccountingCompletenessEvaluator completeness
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _sourceResolver = sourceResolver;
        _t = t;
        _c = c;
        _completeness = completeness;
    }

    public async Task<Result<GetGeneralLedgerReportResponse>> Handle(
        GetGeneralLedgerReportQuery q,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var allAccounts = await _accountRepo.GetByCompanyAsync(tenantId, companyId, ct);

        IReadOnlyList<Account> targetAccounts;
        if (q.AccountId is { } accountId)
            targetAccounts = allAccounts.Where(a => a.Id == accountId).ToList();
        else if (
            !string.IsNullOrWhiteSpace(q.AccountCodeFrom)
            || !string.IsNullOrWhiteSpace(q.AccountCodeTo)
        )
            // ACCOUNTING-REPORTS-HIERARCHY-SMOKE-01: el rango de código también debe comparar en
            // orden natural — con StringComparer.Ordinal, un rango "1.1.2".."1.1.9" excluía
            // incorrectamente "1.1.10" (ordinalmente "1.1.10" < "1.1.9" porque compara carácter a
            // carácter), aunque "1.1.10" sí cae dentro del rango numérico. Mismo comparador que el
            // orden de la lista, para que filtro y orden sean consistentes entre sí.
            targetAccounts = allAccounts
                .Where(a =>
                    (
                        string.IsNullOrWhiteSpace(q.AccountCodeFrom)
                        || AccountCodeComparer.Instance.Compare(a.Code.Value, q.AccountCodeFrom)
                            >= 0
                    )
                    && (
                        string.IsNullOrWhiteSpace(q.AccountCodeTo)
                        || AccountCodeComparer.Instance.Compare(a.Code.Value, q.AccountCodeTo) <= 0
                    )
                )
                .ToList();
        else
            targetAccounts = allAccounts;

        targetAccounts = targetAccounts
            .OrderBy(a => a.Code.Value, AccountCodeComparer.Instance)
            .ToList();

        if (targetAccounts.Count == 0)
            return Result<GetGeneralLedgerReportResponse>.Success(
                new GetGeneralLedgerReportResponse(
                    Array.Empty<GeneralLedgerAccountDto>(),
                    await _completeness.EvaluateAsync(_t.TenantId, _c.CompanyId, null, q.ToDate, ct)
                )
            );

        var accountIds = targetAccounts.Select(a => a.Id).ToList();

        var openingTotals = await _repo.GetAccountLineTotalsAsync(
            tenantId,
            companyId,
            null,
            q.FromDate.AddDays(-1),
            accountIds,
            ct
        );
        var periodTotals = await _repo.GetAccountLineTotalsAsync(
            tenantId,
            companyId,
            q.FromDate,
            q.ToDate,
            accountIds,
            ct
        );

        var accountDtos = new List<GeneralLedgerAccountDto>();
        foreach (var account in targetAccounts)
        {
            var isDebitNature =
                account.Nature == Domain.Modules.Accounting.Enums.AccountNature.Debit;

            openingTotals.TryGetValue(account.Id, out var opening);
            var openingBalance = isDebitNature
                ? opening.TotalDebit - opening.TotalCredit
                : opening.TotalCredit - opening.TotalDebit;

            var rows = await _repo.GetPostedLinesByAccountAsync(
                tenantId,
                companyId,
                account.Id,
                q.FromDate,
                q.ToDate,
                ct
            );

            var sources = await _sourceResolver.ResolveManyAsync(
                tenantId,
                companyId,
                rows.Select(r => new JournalEntrySourceRequest(
                        r.JournalEntryId,
                        r.SourceModule,
                        r.SourceEventType,
                        r.SourceEventId
                    ))
                    .DistinctBy(r => r.JournalEntryId)
                    .ToList(),
                ct
            );

            var runningBalance = openingBalance;
            var movements = new List<GeneralLedgerMovementDto>();
            foreach (var row in rows)
            {
                runningBalance += isDebitNature ? row.Debit - row.Credit : row.Credit - row.Debit;

                sources.TryGetValue(row.JournalEntryId, out var source);
                movements.Add(
                    new GeneralLedgerMovementDto(
                        row.JournalEntryId,
                        row.EntryNumber,
                        row.EntryDate,
                        row.Description,
                        row.SourceModule,
                        source?.SourceDocumentType,
                        source?.SourceDocumentNumber,
                        row.Debit,
                        row.Credit,
                        runningBalance
                    )
                );
            }

            periodTotals.TryGetValue(account.Id, out var period);

            accountDtos.Add(
                new GeneralLedgerAccountDto(
                    account.Id,
                    account.Code.Value,
                    account.Name,
                    account.AccountType.ToString(),
                    account.Nature.ToString(),
                    openingBalance,
                    period.TotalDebit,
                    period.TotalCredit,
                    runningBalance,
                    movements
                )
            );
        }

        return Result<GetGeneralLedgerReportResponse>.Success(
            new GetGeneralLedgerReportResponse(
                accountDtos,
                await _completeness.EvaluateAsync(_t.TenantId, _c.CompanyId, null, q.ToDate, ct)
            )
        );
    }
}

public sealed class GetTrialBalanceReportHandler
    : IRequestHandler<GetTrialBalanceReportQuery, Result<GetTrialBalanceReportResponse>>
{
    private readonly IJournalEntryRepository _repo;
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly IAccountingCompletenessEvaluator _completeness;

    public GetTrialBalanceReportHandler(
        IJournalEntryRepository repo,
        IAccountRepository accountRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        IAccountingCompletenessEvaluator completeness
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _t = t;
        _c = c;
        _completeness = completeness;
    }

    public async Task<Result<GetTrialBalanceReportResponse>> Handle(
        GetTrialBalanceReportQuery q,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var allAccounts = await _accountRepo.GetByCompanyAsync(tenantId, companyId, ct);
        var accountsById = allAccounts.ToDictionary(a => a.Id);

        var openingTotals = await _repo.GetAccountLineTotalsAsync(
            tenantId,
            companyId,
            null,
            q.FromDate.AddDays(-1),
            null,
            ct
        );
        var periodTotals = await _repo.GetAccountLineTotalsAsync(
            tenantId,
            companyId,
            q.FromDate,
            q.ToDate,
            null,
            ct
        );

        var accountIds = q.IncludeZeroMovementAccounts
            ? allAccounts.Select(a => a.Id)
            : openingTotals.Keys.Union(periodTotals.Keys);

        var lines = new List<TrialBalanceLineDto>();
        foreach (var accountId in accountIds.Distinct())
        {
            if (!accountsById.TryGetValue(accountId, out var account))
                continue; // Cuenta de otra Company/eliminada — nunca debería ocurrir (fail-closed), se omite por seguridad.

            openingTotals.TryGetValue(accountId, out var opening);
            periodTotals.TryGetValue(accountId, out var period);

            var openingNet = opening.TotalDebit - opening.TotalCredit;
            var closingNet = openingNet + period.TotalDebit - period.TotalCredit;

            if (
                !q.IncludeZeroMovementAccounts
                && openingNet == 0m
                && closingNet == 0m
                && period.TotalDebit == 0m
                && period.TotalCredit == 0m
            )
                continue;

            lines.Add(
                new TrialBalanceLineDto(
                    account.Id,
                    account.Code.Value,
                    account.Name,
                    account.AccountType.ToString(),
                    openingNet > 0 ? openingNet : 0m,
                    openingNet < 0 ? -openingNet : 0m,
                    period.TotalDebit,
                    period.TotalCredit,
                    closingNet > 0 ? closingNet : 0m,
                    closingNet < 0 ? -closingNet : 0m
                )
            );
        }

        lines = lines.OrderBy(l => l.AccountCode, AccountCodeComparer.Instance).ToList();

        var totalPeriodDebit = lines.Sum(l => l.PeriodDebit);
        var totalPeriodCredit = lines.Sum(l => l.PeriodCredit);

        var response = new GetTrialBalanceReportResponse(
            lines,
            lines.Sum(l => l.OpeningDebit),
            lines.Sum(l => l.OpeningCredit),
            totalPeriodDebit,
            totalPeriodCredit,
            lines.Sum(l => l.ClosingDebit),
            lines.Sum(l => l.ClosingCredit),
            totalPeriodDebit == totalPeriodCredit,
            await _completeness.EvaluateAsync(_t.TenantId, _c.CompanyId, null, q.ToDate, ct)
        );
        return Result<GetTrialBalanceReportResponse>.Success(response);
    }
}

/// <summary>
/// ACCOUNTING-FINANCIAL-STATEMENTS-10: Estado de Resultados — solo lectura, agrega Σ Debit/Σ
/// Credit por cuenta de Income/Cost/Expense en el rango (reutiliza
/// <see cref="IJournalEntryRepository.GetAccountLineTotalsAsync"/>, mismo mecanismo ya probado
/// para Libro Mayor/Balance de Comprobación — incluye reversos Posted como movimiento normal,
/// excluye Draft/Reversed por el filtro `Status == Posted` ya aplicado dentro del repositorio).
/// Sin saldo inicial: a diferencia de Balance General, las cuentas de resultados no arrastran
/// saldo entre rangos en este sistema.
/// </summary>
public sealed record GetIncomeStatementReportQuery(DateOnly FromDate, DateOnly ToDate)
    : IRequest<Result<GetIncomeStatementReportResponse>>,
        ICompanyScopedRequest;

/// <summary>
/// ACCOUNTING-FINANCIAL-STATEMENTS-10: Balance General — saldo acumulado (desde el inicio del
/// historial Posted) de cada cuenta de Asset/Liability/Equity hasta <see cref="AsOfDate"/>
/// inclusive, más los resultados no cerrados calculados (ACCOUNTING-11, ver
/// <see cref="GetBalanceSheetReportResponse"/>).
/// </summary>
public sealed record GetBalanceSheetReportQuery(DateOnly AsOfDate)
    : IRequest<Result<GetBalanceSheetReportResponse>>,
        ICompanyScopedRequest;

public sealed class GetIncomeStatementReportQueryValidator
    : AbstractValidator<GetIncomeStatementReportQuery>
{
    public GetIncomeStatementReportQueryValidator() =>
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate);
}

public sealed class GetBalanceSheetReportQueryValidator
    : AbstractValidator<GetBalanceSheetReportQuery>
{
    public GetBalanceSheetReportQueryValidator() => RuleFor(x => x.AsOfDate).NotEmpty();
}

public sealed class GetIncomeStatementReportHandler
    : IRequestHandler<GetIncomeStatementReportQuery, Result<GetIncomeStatementReportResponse>>
{
    private readonly IJournalEntryRepository _repo;
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly IAccountingCompletenessEvaluator _completeness;

    public GetIncomeStatementReportHandler(
        IJournalEntryRepository repo,
        IAccountRepository accountRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        IAccountingCompletenessEvaluator completeness
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _t = t;
        _c = c;
        _completeness = completeness;
    }

    public async Task<Result<GetIncomeStatementReportResponse>> Handle(
        GetIncomeStatementReportQuery q,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var allAccounts = await _accountRepo.GetByCompanyAsync(tenantId, companyId, ct);
        var relevantAccounts = allAccounts
            .Where(a =>
                a.AccountType is AccountType.Income or AccountType.Cost or AccountType.Expense
            )
            .OrderBy(a => a.Code.Value, AccountCodeComparer.Instance)
            .ToList();

        // ACCOUNTING-12D: el asiento de cierre anual (y su reverso) traslada el resultado a patrimonio;
        // no es un movimiento de resultados, así que no anula el Estado de Resultados del ejercicio.
        var totals = await _repo.GetAccountLineTotalsExcludingYearEndClosingAsync(
            tenantId,
            companyId,
            q.FromDate,
            q.ToDate,
            relevantAccounts.Select(a => a.Id).ToList(),
            ct
        );

        // ACCOUNTING-12A — signo por sección, igual que el Balance General: una cuenta correctora
        // (naturaleza opuesta) resta dentro de su sección y la utilidad neta = Map.NetResult.
        var incomeLines = Map.ToSectionLines(relevantAccounts, totals, AccountType.Income, AccountNature.Credit);
        var costLines = Map.ToSectionLines(relevantAccounts, totals, AccountType.Cost, AccountNature.Debit);
        var expenseLines = Map.ToSectionLines(relevantAccounts, totals, AccountType.Expense, AccountNature.Debit);

        var totalIncome = incomeLines.Sum(l => l.Amount);
        var totalCost = costLines.Sum(l => l.Amount);
        var grossProfit = totalIncome - totalCost;
        var totalExpense = expenseLines.Sum(l => l.Amount);
        var netProfit = grossProfit - totalExpense;

        return Result<GetIncomeStatementReportResponse>.Success(
            new GetIncomeStatementReportResponse(
                incomeLines,
                totalIncome,
                costLines,
                totalCost,
                grossProfit,
                expenseLines,
                totalExpense,
                netProfit,
                await _completeness.EvaluateAsync(_t.TenantId, _c.CompanyId, q.FromDate, q.ToDate, ct)
            )
        );
    }
}

public sealed class GetBalanceSheetReportHandler
    : IRequestHandler<GetBalanceSheetReportQuery, Result<GetBalanceSheetReportResponse>>
{
    private readonly IJournalEntryRepository _repo;
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly IAccountingCompletenessEvaluator _completeness;

    public GetBalanceSheetReportHandler(
        IJournalEntryRepository repo,
        IAccountRepository accountRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        IAccountingCompletenessEvaluator completeness
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _t = t;
        _c = c;
        _completeness = completeness;
    }

    public async Task<Result<GetBalanceSheetReportResponse>> Handle(
        GetBalanceSheetReportQuery q,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var allAccounts = await _accountRepo.GetByCompanyAsync(tenantId, companyId, ct);
        var relevantAccounts = allAccounts
            .Where(a =>
                a.AccountType is AccountType.Asset or AccountType.Liability or AccountType.Equity
            )
            .OrderBy(a => a.Code.Value, AccountCodeComparer.Instance)
            .ToList();

        var totals = await _repo.GetAccountLineTotalsAsync(
            tenantId,
            companyId,
            null,
            q.AsOfDate,
            relevantAccounts.Select(a => a.Id).ToList(),
            ct
        );

        // ACCOUNTING-11: signo de la sección (Activo Debe − Haber; Pasivo/Patrimonio Haber − Debe),
        // no la naturaleza de cada cuenta — una cuenta correctora (p. ej. depreciación acumulada)
        // resta dentro de su sección en vez de sumarse y fingir un descuadre.
        var assetLines = Map.ToSectionLines(relevantAccounts, totals, AccountType.Asset, AccountNature.Debit);
        var liabilityLines = Map.ToSectionLines(relevantAccounts, totals, AccountType.Liability, AccountNature.Credit);
        var equityLines = Map.ToSectionLines(relevantAccounts, totals, AccountType.Equity, AccountNature.Credit);

        // ACCOUNTING-11: resultados todavía presentes en cuentas nominales, calculados (nunca un
        // asiento persistido) desde los asientos reales hasta AsOfDate. El ejercicio es el año
        // calendario (accounting.md §22). Un asiento de cierre que deja las nominales en cero hace
        // que estas filas valgan cero: el patrimonio queda en sus cuentas, sin duplicarse.
        var nominalAccountIds = allAccounts
            .Where(a => a.AccountType is AccountType.Income or AccountType.Cost or AccountType.Expense)
            .Select(a => a.Id)
            .ToList();
        var fiscalYearStart = new DateOnly(q.AsOfDate.Year, 1, 1);
        var currentYearResult = Map.NetResult(
            await _repo.GetAccountLineTotalsAsync(tenantId, companyId, fiscalYearStart, q.AsOfDate, nominalAccountIds, ct),
            nominalAccountIds
        );
        var priorYearsUnclosedResult = Map.NetResult(
            await _repo.GetAccountLineTotalsAsync(tenantId, companyId, null, fiscalYearStart.AddDays(-1), nominalAccountIds, ct),
            nominalAccountIds
        );

        var totalAssets = assetLines.Sum(l => l.Amount);
        var totalLiabilities = liabilityLines.Sum(l => l.Amount);
        var totalEquity = equityLines.Sum(l => l.Amount) + currentYearResult + priorYearsUnclosedResult;
        var difference = totalAssets - (totalLiabilities + totalEquity);

        return Result<GetBalanceSheetReportResponse>.Success(
            new GetBalanceSheetReportResponse(
                assetLines,
                totalAssets,
                liabilityLines,
                totalLiabilities,
                equityLines,
                currentYearResult,
                priorYearsUnclosedResult,
                totalEquity,
                difference,
                difference == 0m,
                await _completeness.EvaluateAsync(_t.TenantId, _c.CompanyId, null, q.AsOfDate, ct)
            )
        );
    }
}

file static class Map
{
    /// <summary>
    /// ACCOUNTING-11/12A — Balance General y Estado de Resultados: monto con el signo de la sección,
    /// nunca con la naturaleza de la cuenta (una correctora resta dentro de su sección).
    /// </summary>
    public static IReadOnlyList<FinancialStatementLineDto> ToSectionLines(
        IReadOnlyList<Account> accounts,
        IReadOnlyDictionary<Guid, (decimal TotalDebit, decimal TotalCredit)> totals,
        AccountType type,
        AccountNature sectionSign
    ) =>
        accounts
            .Where(a => a.AccountType == type && totals.ContainsKey(a.Id))
            .Select(a =>
            {
                var t = totals[a.Id];
                var amount =
                    sectionSign == AccountNature.Debit
                        ? t.TotalDebit - t.TotalCredit
                        : t.TotalCredit - t.TotalDebit;
                return new FinancialStatementLineDto(a.Id, a.Code.Value, a.Name, amount);
            })
            .ToList();

    /// <summary>ACCOUNTING-11 — Ingresos − Costos − Gastos = Σ(Haber − Debe) de las cuentas nominales.</summary>
    public static decimal NetResult(
        IReadOnlyDictionary<Guid, (decimal TotalDebit, decimal TotalCredit)> totals,
        IReadOnlyCollection<Guid> nominalAccountIds
    ) =>
        totals
            .Where(t => nominalAccountIds.Contains(t.Key))
            .Sum(t => t.Value.TotalCredit - t.Value.TotalDebit);
}
