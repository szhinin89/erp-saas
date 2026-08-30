using ERP.Application.Common;
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

public sealed class GetTrialBalanceReportQueryValidator : AbstractValidator<GetTrialBalanceReportQuery>
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

    public GetGeneralJournalReportHandler(
        IJournalEntryRepository repo,
        IAccountRepository accountRepo,
        IJournalEntrySourceResolver sourceResolver,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _sourceResolver = sourceResolver;
        _t = t;
        _c = c;
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
            items.Select(e => new JournalEntrySourceRequest(
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
            totalCount
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

    public GetGeneralLedgerReportHandler(
        IJournalEntryRepository repo,
        IAccountRepository accountRepo,
        IJournalEntrySourceResolver sourceResolver,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _sourceResolver = sourceResolver;
        _t = t;
        _c = c;
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
        else if (!string.IsNullOrWhiteSpace(q.AccountCodeFrom) || !string.IsNullOrWhiteSpace(q.AccountCodeTo))
            // ACCOUNTING-REPORTS-HIERARCHY-SMOKE-01: el rango de código también debe comparar en
            // orden natural — con StringComparer.Ordinal, un rango "1.1.2".."1.1.9" excluía
            // incorrectamente "1.1.10" (ordinalmente "1.1.10" < "1.1.9" porque compara carácter a
            // carácter), aunque "1.1.10" sí cae dentro del rango numérico. Mismo comparador que el
            // orden de la lista, para que filtro y orden sean consistentes entre sí.
            targetAccounts = allAccounts
                .Where(a =>
                    (string.IsNullOrWhiteSpace(q.AccountCodeFrom)
                        || AccountCodeComparer.Instance.Compare(a.Code.Value, q.AccountCodeFrom) >= 0)
                    && (string.IsNullOrWhiteSpace(q.AccountCodeTo)
                        || AccountCodeComparer.Instance.Compare(a.Code.Value, q.AccountCodeTo) <= 0)
                )
                .ToList();
        else
            targetAccounts = allAccounts;

        targetAccounts = targetAccounts.OrderBy(a => a.Code.Value, AccountCodeComparer.Instance).ToList();

        if (targetAccounts.Count == 0)
            return Result<GetGeneralLedgerReportResponse>.Success(
                new GetGeneralLedgerReportResponse(Array.Empty<GeneralLedgerAccountDto>())
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
            var isDebitNature = account.Nature == Domain.Modules.Accounting.Enums.AccountNature.Debit;

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
                runningBalance += isDebitNature
                    ? row.Debit - row.Credit
                    : row.Credit - row.Debit;

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
            new GetGeneralLedgerReportResponse(accountDtos)
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

    public GetTrialBalanceReportHandler(
        IJournalEntryRepository repo,
        IAccountRepository accountRepo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _t = t;
        _c = c;
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

            if (!q.IncludeZeroMovementAccounts && openingNet == 0m && closingNet == 0m && period.TotalDebit == 0m && period.TotalCredit == 0m)
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
            totalPeriodDebit == totalPeriodCredit
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
    : IRequest<Result<GetIncomeStatementReportResponse>>, ICompanyScopedRequest;

/// <summary>
/// ACCOUNTING-FINANCIAL-STATEMENTS-10: Balance General — saldo acumulado (desde el inicio del
/// historial Posted) de cada cuenta de Asset/Liability/Equity hasta <see cref="AsOfDate"/>
/// inclusive. Ver <see cref="GetBalanceSheetReportResponse"/> para la nota sobre por qué
/// <c>IsBalanced</c> puede legítimamente ser <c>false</c> sin cierre contable.
/// </summary>
public sealed record GetBalanceSheetReportQuery(DateOnly AsOfDate)
    : IRequest<Result<GetBalanceSheetReportResponse>>, ICompanyScopedRequest;

public sealed class GetIncomeStatementReportQueryValidator
    : AbstractValidator<GetIncomeStatementReportQuery>
{
    public GetIncomeStatementReportQueryValidator() =>
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate);
}

public sealed class GetBalanceSheetReportQueryValidator : AbstractValidator<GetBalanceSheetReportQuery>
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

    public GetIncomeStatementReportHandler(
        IJournalEntryRepository repo,
        IAccountRepository accountRepo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _t = t;
        _c = c;
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
            .Where(a => a.AccountType is AccountType.Income or AccountType.Cost or AccountType.Expense)
            .OrderBy(a => a.Code.Value, AccountCodeComparer.Instance)
            .ToList();

        var totals = await _repo.GetAccountLineTotalsAsync(
            tenantId,
            companyId,
            q.FromDate,
            q.ToDate,
            relevantAccounts.Select(a => a.Id).ToList(),
            ct
        );

        var incomeLines = Map.ToStatementLines(relevantAccounts, totals, AccountType.Income);
        var costLines = Map.ToStatementLines(relevantAccounts, totals, AccountType.Cost);
        var expenseLines = Map.ToStatementLines(relevantAccounts, totals, AccountType.Expense);

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
                netProfit
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

    public GetBalanceSheetReportHandler(
        IJournalEntryRepository repo,
        IAccountRepository accountRepo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _t = t;
        _c = c;
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
            .Where(a => a.AccountType is AccountType.Asset or AccountType.Liability or AccountType.Equity)
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

        var assetLines = Map.ToStatementLines(relevantAccounts, totals, AccountType.Asset);
        var liabilityLines = Map.ToStatementLines(relevantAccounts, totals, AccountType.Liability);
        var equityLines = Map.ToStatementLines(relevantAccounts, totals, AccountType.Equity);

        var totalAssets = assetLines.Sum(l => l.Amount);
        var totalLiabilities = liabilityLines.Sum(l => l.Amount);
        var totalEquity = equityLines.Sum(l => l.Amount);
        var difference = totalAssets - (totalLiabilities + totalEquity);

        return Result<GetBalanceSheetReportResponse>.Success(
            new GetBalanceSheetReportResponse(
                assetLines,
                totalAssets,
                liabilityLines,
                totalLiabilities,
                equityLines,
                totalEquity,
                difference,
                difference == 0m
            )
        );
    }
}

file static class Map
{
    /// <summary>
    /// Filtra las cuentas del tipo pedido y convierte su Σ Debit/Σ Credit al monto en convención
    /// natural según <see cref="Account.Nature"/> (Debit: TotalDebit − TotalCredit; Credit: al
    /// revés) — mismo criterio ya usado en Libro Mayor/Balance de Comprobación. Cuentas sin
    /// ningún movimiento en el rango se omiten (no aparecen en <paramref name="totals"/>), igual
    /// que el default de Balance de Comprobación.
    /// </summary>
    public static IReadOnlyList<FinancialStatementLineDto> ToStatementLines(
        IReadOnlyList<Account> accounts,
        IReadOnlyDictionary<Guid, (decimal TotalDebit, decimal TotalCredit)> totals,
        AccountType type
    ) =>
        accounts
            .Where(a => a.AccountType == type)
            .Where(a => totals.ContainsKey(a.Id))
            .Select(a =>
            {
                var t = totals[a.Id];
                var amount =
                    a.Nature == AccountNature.Debit ? t.TotalDebit - t.TotalCredit : t.TotalCredit - t.TotalDebit;
                return new FinancialStatementLineDto(a.Id, a.Code.Value, a.Name, amount);
            })
            .ToList();
}
