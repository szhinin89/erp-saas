using System.Text.Json;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Companies;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;

// ── Contratos ───────────────────────────────────────────────────────────

/// <summary>
/// ACCOUNTING-12D — estado del cierre anual de un ejercicio. <see cref="Status"/>: NotGenerated (sin
/// asiento vigente), Draft (generado, pendiente de publicar) o Posted (contabilizado y sin reverso).
/// <see cref="Result"/> = Ingresos − Costos − Gastos trasladado a la cuenta de resultado.
/// </summary>
public sealed record FiscalYearClosingDto(
    int FiscalYear,
    string Status,
    Guid? JournalEntryId,
    int? EntryNumber,
    DateOnly? EntryDate,
    decimal? Result,
    Guid? LastPeriodId,
    string? LastPeriodStatus
);

/// <summary>
/// ACCOUNTING-12D — genera el asiento de cierre anual del ejercicio en Draft. No lo publica: el usuario
/// lo revisa y lo contabiliza con el flujo de publicación de asientos manuales (accounting.md §26).
/// </summary>
public sealed record GenerateFiscalYearClosingEntryCommand(int FiscalYear)
    : IRequest<Result<FiscalYearClosingDto>>,
        ICompanyScopedRequest;

/// <summary>ACCOUNTING-12D — estado del cierre anual de cada ejercicio con períodos.</summary>
public sealed record GetFiscalYearClosingsQuery()
    : IRequest<Result<IReadOnlyList<FiscalYearClosingDto>>>,
        ICompanyScopedRequest;

public sealed class GenerateFiscalYearClosingEntryCommandValidator
    : AbstractValidator<GenerateFiscalYearClosingEntryCommand>
{
    public GenerateFiscalYearClosingEntryCommandValidator() =>
        RuleFor(x => x.FiscalYear).InclusiveBetween(2000, 2999);
}

public static class FiscalYearClosingCodes
{
    public const string PeriodsIncomplete = "FISCAL_YEAR_PERIODS_INCOMPLETE";
    public const string LastPeriodNotOpen = "FISCAL_YEAR_LAST_PERIOD_NOT_OPEN";
    public const string PreviousPeriodsOpen = "FISCAL_YEAR_PREVIOUS_PERIODS_OPEN";
    public const string NotEnded = "FISCAL_YEAR_NOT_ENDED";
    public const string NotEnabled = "ACCOUNTING_NOT_ENABLED";
    public const string AccountingIncomplete = "FISCAL_YEAR_ACCOUNTING_INCOMPLETE";
    public const string DraftEntriesPending = "FISCAL_YEAR_DRAFT_ENTRIES";
    public const string DraftExists = "YEAR_END_CLOSING_DRAFT_EXISTS";
    public const string AlreadyPosted = "YEAR_END_CLOSING_ALREADY_POSTED";
    public const string NothingToClose = "YEAR_END_CLOSING_NOTHING_TO_CLOSE";
    public const string ResultAccountInvalid = "YEAR_END_RESULT_ACCOUNT_INVALID";
    public const string Outdated = "YEAR_END_CLOSING_OUTDATED";
    public const string NotEditable = "YEAR_END_CLOSING_NOT_EDITABLE";
}

/// <summary>Asiento calculado del cierre: fecha (31-dic), período destino, líneas y resultado.</summary>
public sealed record FiscalYearClosingPlan(
    AccountingPeriod LastPeriod,
    DateOnly EntryDate,
    IReadOnlyList<ManualJournalLine> Lines,
    decimal Result
);

// ── Servicio ────────────────────────────────────────────────────────────

/// <summary>
/// ACCOUNTING-12D — precondiciones y cálculo del cierre anual, compartidos por la generación y por la
/// publicación (que vuelve a validar todo bajo el mismo lock del ejercicio). Fail-closed: cualquier
/// condición no verificable rechaza la operación sin escribir.
/// </summary>
public sealed class FiscalYearClosingService(
    IJournalEntryRepository journal,
    IAccountRepository accounts,
    IAccountingPeriodRepository periods,
    IAccountingCompletenessEvaluator completeness,
    IAccountingOperationalPolicyRepository policies,
    ICompanyClock clock,
    ICompanyPrecisionPolicyProvider precision,
    ILogger<PostingEngine> postingLogger
)
{
    private const string LockModule = "Accounting.FiscalYearClosing";

    /// <summary>
    /// Serializa generación y publicación del cierre de un mismo ejercicio (advisory lock de la transacción
    /// ambiente); evita dos Draft/Posted vigentes ante solicitudes concurrentes.
    /// </summary>
    public Task AcquireLockAsync(Guid companyId, int fiscalYear, CancellationToken ct) =>
        journal.AcquireIdempotencyLockAsync(companyId, LockModule, new Guid(fiscalYear, 0, 0, new byte[8]), "Mutation", ct);

    /// <summary>
    /// <paramref name="publishing"/>: Draft del cierre que se está publicando (se excluye de los
    /// controles de duplicado y de borradores pendientes); null al generar.
    /// </summary>
    public async Task<Result<FiscalYearClosingPlan>> PrepareAsync(
        Guid tenantId,
        Guid companyId,
        int fiscalYear,
        JournalEntry? publishing,
        CancellationToken ct
    )
    {
        var (yearStart, yearEnd) = FiscalYearClosingRules.YearRange(fiscalYear);
        var yearPeriods = (await periods.GetByCompanyAsync(tenantId, companyId, ct))
            .Where(p => p.FiscalYear == fiscalYear)
            .OrderBy(p => p.StartDate)
            .ToList();
        var last = yearPeriods.LastOrDefault();
        if (yearPeriods.Count == 0 || yearPeriods[0].StartDate != yearStart || last!.EndDate != yearEnd)
            return Fail(FiscalYearClosingCodes.PeriodsIncomplete,
                $"El ejercicio {fiscalYear} no tiene períodos que cubran del {yearStart:yyyy-MM-dd} al {yearEnd:yyyy-MM-dd}.");
        if (last.Status != PeriodStatus.Open)
            return Fail(FiscalYearClosingCodes.LastPeriodNotOpen,
                $"El último período del ejercicio {fiscalYear} ({last.FiscalYear}-{last.PeriodNumber}) no está abierto: el cierre anual ya no puede registrarse.");
        var openBefore = yearPeriods.Count(p => p.Id != last.Id && p.Status == PeriodStatus.Open);
        if (openBefore > 0)
            return Fail(FiscalYearClosingCodes.PreviousPeriodsOpen,
                $"Cierre primero los períodos anteriores del ejercicio {fiscalYear} ({openBefore} abierto(s)).");

        var today = await clock.TodayAsync(companyId, tenantId, ct);
        if (today <= yearEnd)
            return Fail(FiscalYearClosingCodes.NotEnded,
                $"El ejercicio {fiscalYear} termina el {yearEnd:yyyy-MM-dd}: el cierre anual se genera a partir del día siguiente (hoy es {today:yyyy-MM-dd} según la fecha de la empresa).");
        if (await new AccountingOperationalDecision(policies).ResolveAsync(tenantId, companyId, yearEnd, ct)
            != AccountingApplicability.Applicable)
            return Fail(FiscalYearClosingCodes.NotEnabled,
                $"Contabilidad debe estar habilitada el {yearEnd:yyyy-MM-dd} para registrar el cierre anual.");

        // Obligaciones Pending/Failed, apertura, costo pendiente y orígenes sin contabilizar del ejercicio:
        // el mismo readiness único del cierre de período y de los reportes (§21).
        var state = await completeness.EvaluateAsync(tenantId, companyId, yearStart, yearEnd, ct);
        if (!state.IsComplete)
            return Fail(FiscalYearClosingCodes.AccountingIncomplete,
                $"El ejercicio {fiscalYear} tiene información contable incompleta ("
                + string.Join(", ", state.Reasons.Select(r => $"{r.Code}: {r.Count}"))
                + "). Resuelva los pendientes antes del cierre anual.");

        foreach (var (entry, reversed) in await journal.GetFiscalYearClosingsAsync(tenantId, companyId, fiscalYear, ct))
        {
            if (publishing is not null && entry.Id == publishing.Id)
                continue;
            if (entry.Status == JournalEntryStatus.Draft)
                return Fail(FiscalYearClosingCodes.DraftExists,
                    $"Ya existe un asiento de cierre del ejercicio {fiscalYear} en borrador: publíquelo o descártelo.");
            if (entry.Status == JournalEntryStatus.Posted && !reversed)
                return Fail(FiscalYearClosingCodes.AlreadyPosted,
                    $"El resultado del ejercicio {fiscalYear} ya fue cerrado con el asiento N° {entry.EntryNumber}.");
        }

        var drafts = (await journal.GetClosureCountsAsync(tenantId, companyId, last.Id, ct)).DraftOrNonFinalEntries
            - (publishing is { Status: JournalEntryStatus.Draft } && publishing.AccountingPeriodId == last.Id ? 1 : 0);
        if (drafts > 0)
            return Fail(FiscalYearClosingCodes.DraftEntriesPending,
                $"El último período del ejercicio {fiscalYear} tiene {drafts} asiento(s) sin publicar: publíquelos o descártelos antes del cierre anual.");

        var resultAccount = await accounts.FindByCodeAsync(tenantId, companyId, FiscalYearClosingRules.ResultAccountCode, ct);
        if (resultAccount is null || !resultAccount.IsActive || !resultAccount.AllowsPosting
            || resultAccount.AccountType != AccountType.Equity)
            return Fail(FiscalYearClosingCodes.ResultAccountInvalid,
                $"La cuenta {FiscalYearClosingRules.ResultAccountCode} Resultado del ejercicio debe existir en la empresa, estar activa, admitir movimientos y ser de Patrimonio.");

        var nominal = (await accounts.GetByCompanyAsync(tenantId, companyId, ct))
            .Where(FiscalYearClosingRules.IsNominal)
            .ToList();
        var totals = await journal.GetAccountLineTotalsExcludingYearEndClosingAsync(
            tenantId, companyId, yearStart, yearEnd, nominal.Select(a => a.Id).ToList(), ct);
        var decimals = (await precision.GetEffectiveAsync(ct)).AccountingDecimals;
        var (lines, result) = FiscalYearClosingRules.BuildLines(nominal, totals, resultAccount.Id, fiscalYear, decimals);
        if (lines.Count == 0)
            return Fail(FiscalYearClosingCodes.NothingToClose,
                $"Las cuentas de resultados del ejercicio {fiscalYear} no tienen saldo: no hay resultado que cerrar.");

        var accountGate = await new PostingAccountGuard(accounts, postingLogger)
            .EnsureAccountsPostableAsync(tenantId, companyId, lines.Select(l => l.AccountId), ct);
        if (!accountGate.IsSuccess)
            return Result<FiscalYearClosingPlan>.ValidationFailure(accountGate.Error!, accountGate.Code);

        return Result<FiscalYearClosingPlan>.Success(new(last, yearEnd, lines, result));
    }

    public async Task<IReadOnlyList<FiscalYearClosingDto>> ListAsync(Guid tenantId, Guid companyId, CancellationToken ct)
    {
        var resultAccount = await accounts.FindByCodeAsync(tenantId, companyId, FiscalYearClosingRules.ResultAccountCode, ct);
        var closings = await journal.GetFiscalYearClosingsAsync(tenantId, companyId, null, ct);
        return (await periods.GetByCompanyAsync(tenantId, companyId, ct))
            .GroupBy(p => p.FiscalYear)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var last = g.OrderBy(p => p.StartDate).Last();
                var lastPeriod = FiscalYearClosingRules.IsLastPeriodOfYear(last) ? last : null;
                var current = closings
                    .Where(c => c.Entry.FiscalYear == g.Key
                        && (c.Entry.Status == JournalEntryStatus.Draft
                            || (c.Entry.Status == JournalEntryStatus.Posted && !c.Reversed)))
                    .Select(c => c.Entry)
                    .LastOrDefault();
                if (current is null)
                    return new FiscalYearClosingDto(g.Key, "NotGenerated", null, null, null, null,
                        lastPeriod?.Id, lastPeriod?.Status.ToString());
                var result = current.Lines
                    .Where(l => l.AccountId == resultAccount?.Id)
                    .Sum(l => l.Credit - l.Debit);
                return new FiscalYearClosingDto(g.Key, current.Status.ToString(), current.Id, current.EntryNumber,
                    current.EntryDate, result, lastPeriod?.Id, lastPeriod?.Status.ToString());
            })
            .ToList();
    }

    private static Result<FiscalYearClosingPlan> Fail(string code, string message) =>
        Result<FiscalYearClosingPlan>.ValidationFailure(message, code);
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GenerateFiscalYearClosingEntryHandler(
    IUnitOfWork uow,
    IJournalEntryRepository journal,
    IAccountingOperationalPolicyRepository policies,
    FiscalYearClosingService closing,
    ICompanyPrecisionPolicyProvider precision,
    ICurrentTenant tenant,
    ICurrentCompany company,
    ICurrentUser user,
    IAuditService audit,
    IAuditContext auditContext
) : IRequestHandler<GenerateFiscalYearClosingEntryCommand, Result<FiscalYearClosingDto>>
{
    public async Task<Result<FiscalYearClosingDto>> Handle(GenerateFiscalYearClosingEntryCommand cmd, CancellationToken ct)
    {
        if (tenant.TenantId == Guid.Empty || company.CompanyId == Guid.Empty || user.UserId == Guid.Empty)
            return Result<FiscalYearClosingDto>.Forbidden("Se requiere contexto autenticado de empresa.");

        await uow.BeginTransactionAsync(ct);
        try
        {
            // Lock compartido por Company (compatible con el cierre exclusivo de período) + lock del ejercicio.
            await policies.AcquireSharedLockAsync(company.CompanyId, ct);
            await closing.AcquireLockAsync(company.CompanyId, cmd.FiscalYear, ct);

            var plan = await closing.PrepareAsync(tenant.TenantId, company.CompanyId, cmd.FiscalYear, null, ct);
            if (!plan.IsSuccess)
            {
                await uow.RollbackAsync(ct);
                return Result<FiscalYearClosingDto>.ValidationFailure(plan.Error!, plan.Code);
            }

            var p = plan.Value!;
            var decimals = (await precision.GetEffectiveAsync(ct)).AccountingDecimals;
            var entry = JournalEntry.CreateFiscalYearClosing(tenant.TenantId, company.CompanyId, Guid.NewGuid(),
                p.EntryDate, p.LastPeriod.Id, p.LastPeriod.FiscalYear,
                $"Asiento de cierre del ejercicio {cmd.FiscalYear}", p.Lines, decimals, user.UserId);
            await journal.AddAsync(entry, ct);
            await audit.RecordAsync(JournalEntryAudit.Create(auditContext.Actor, entry, "Created",
                JsonSerializer.Serialize(new { entry.Id, entry.EntryDate, entry.Description, entry.Version,
                    Status = entry.Status.ToString(), entry.Lines, p.Result }), null), ct);
            await uow.SaveChangesAsync(ct);
            await uow.CommitAsync(ct);

            return Result<FiscalYearClosingDto>.Success(new(cmd.FiscalYear, entry.Status.ToString(), entry.Id,
                null, entry.EntryDate, p.Result, p.LastPeriod.Id, p.LastPeriod.Status.ToString()));
        }
        catch (DomainRuleViolationException ex)
        {
            await uow.RollbackAsync(CancellationToken.None);
            uow.ClearChangeTracker();
            return Result<FiscalYearClosingDto>.FromDomainRule(ex);
        }
        catch
        {
            await uow.RollbackAsync(CancellationToken.None);
            uow.ClearChangeTracker();
            throw;
        }
    }
}

public sealed class GetFiscalYearClosingsHandler(
    FiscalYearClosingService closing,
    ICurrentTenant tenant,
    ICurrentCompany company
) : IRequestHandler<GetFiscalYearClosingsQuery, Result<IReadOnlyList<FiscalYearClosingDto>>>
{
    public async Task<Result<IReadOnlyList<FiscalYearClosingDto>>> Handle(GetFiscalYearClosingsQuery q, CancellationToken ct) =>
        Result<IReadOnlyList<FiscalYearClosingDto>>.Success(
            await closing.ListAsync(tenant.TenantId, company.CompanyId, ct));
}
