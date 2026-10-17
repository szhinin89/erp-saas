using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;

// ── Commands ────────────────────────────────────────────────────────────

public sealed record CreateAccountingPeriodCommand(
    int FiscalYear,
    int PeriodNumber,
    DateOnly StartDate,
    DateOnly EndDate
) : IRequest<Result<AccountingPeriodDto>>, ICompanyScopedRequest;

/// <summary>
/// ACCOUNTING-07 — genera los 12 períodos mensuales naturales del ejercicio. Idempotente: completa los
/// meses faltantes al final de una estructura mensual compatible y no crea nada si ya están los 12;
/// rechaza un ejercicio con estructura incompatible (p. ej. el período anual 2026).
/// </summary>
public sealed record GenerateAccountingFiscalYearCommand(int FiscalYear)
    : IRequest<Result<AccountingFiscalYearDto>>,
        ICompanyScopedRequest;

public sealed record CloseAccountingPeriodCommand(Guid Id)
    : IRequest<Result<AccountingPeriodDto>>,
        ICompanyScopedRequest;

public sealed record LockAccountingPeriodCommand(Guid Id)
    : IRequest<Result<AccountingPeriodDto>>,
        ICompanyScopedRequest;

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetAccountingPeriodsQuery()
    : IRequest<Result<IReadOnlyList<AccountingPeriodDto>>>,
        ICompanyScopedRequest;

public sealed record GetAccountingPeriodByIdQuery(Guid Id)
    : IRequest<Result<AccountingPeriodDto>>,
        ICompanyScopedRequest;

/// <summary>
/// ACCOUNTING-CONTINUITY-06 — pre-cierre de solo lectura: exactamente la misma evaluación que usa
/// <see cref="CloseAccountingPeriodCommand"/> (sin lock: el resultado puede cambiar hasta el cierre).
/// ACCOUNTING-07: solo un período Open es elegible; Closed/Locked responden explícitamente no elegibles.
/// </summary>
public sealed record GetAccountingPeriodClosureReadinessQuery(Guid Id)
    : IRequest<Result<AccountingPeriodClosureReadinessDto>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreateAccountingPeriodCommandValidator
    : AbstractValidator<CreateAccountingPeriodCommand>
{
    public CreateAccountingPeriodCommandValidator()
    {
        RuleFor(x => x.FiscalYear).GreaterThan(0);
        RuleFor(x => x.PeriodNumber)
            .InclusiveBetween(1, AccountingPeriod.MaxPeriodsPerFiscalYear);
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("La fecha de fin no puede ser anterior a la fecha de inicio.");
    }
}

public sealed class GenerateAccountingFiscalYearCommandValidator
    : AbstractValidator<GenerateAccountingFiscalYearCommand>
{
    public GenerateAccountingFiscalYearCommandValidator() =>
        RuleFor(x => x.FiscalYear).InclusiveBetween(2000, 2999);
}

public sealed class CloseAccountingPeriodCommandValidator
    : AbstractValidator<CloseAccountingPeriodCommand>
{
    public CloseAccountingPeriodCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class LockAccountingPeriodCommandValidator
    : AbstractValidator<LockAccountingPeriodCommand>
{
    public LockAccountingPeriodCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

// ── Command Handlers ────────────────────────────────────────────────────

public sealed class CreateAccountingPeriodHandler
    : IRequestHandler<CreateAccountingPeriodCommand, Result<AccountingPeriodDto>>
{
    private readonly IAccountingPeriodRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public CreateAccountingPeriodHandler(
        IAccountingPeriodRepository repo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u,
        IDatabaseExceptionTranslator dbEx
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
        _u = u;
        _dbEx = dbEx;
    }

    public async Task<Result<AccountingPeriodDto>> Handle(
        CreateAccountingPeriodCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        // ACCOUNTING-07: estructura (solape, contigüidad, orden frente a Closed/Locked, número) validada
        // aquí para la UX; la exclusión PostgreSQL ex_accounting_periods_company_no_overlap cierra la
        // carrera de dos creaciones concurrentes.
        var existing = await _repo.GetByCompanyAsync(tenantId, companyId, ct);
        var violation = AccountingPeriodStructure.Validate(
            existing,
            new(cmd.FiscalYear, cmd.PeriodNumber, cmd.StartDate, cmd.EndDate)
        );
        if (violation is not null)
            return violation.IsOverlap
                ? Result<AccountingPeriodDto>.Conflict(violation.Message)
                : Result<AccountingPeriodDto>.ValidationFailure(violation.Message);

        try
        {
            var period = AccountingPeriod.Create(
                tenantId,
                companyId,
                cmd.FiscalYear,
                cmd.PeriodNumber,
                cmd.StartDate,
                cmd.EndDate,
                _u.UserId
            );

            await _repo.AddAsync(period, ct);
            await _repo.SaveChangesAsync(ct);
            return Result<AccountingPeriodDto>.Success(AccountingPeriodMap.ToDto(period));
        }
        catch (ArgumentException ex)
        {
            return Result<AccountingPeriodDto>.ValidationFailure(ex.Message);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<AccountingPeriodDto>.Conflict(
                $"Ya existe el período {cmd.FiscalYear}-{cmd.PeriodNumber} en esta empresa."
            );
        }
        catch (Exception ex) when (_dbEx.ClassifyFailureCode(ex) == ApiResponseCodes.Common.Conflict)
        {
            return Result<AccountingPeriodDto>.Conflict(AccountingPeriodMap.ConcurrentOverlapMessage);
        }
    }
}

public sealed class GenerateAccountingFiscalYearHandler
    : IRequestHandler<GenerateAccountingFiscalYearCommand, Result<AccountingFiscalYearDto>>
{
    private readonly IAccountingPeriodRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public GenerateAccountingFiscalYearHandler(
        IAccountingPeriodRepository repo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u,
        IDatabaseExceptionTranslator dbEx
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
        _u = u;
        _dbEx = dbEx;
    }

    public async Task<Result<AccountingFiscalYearDto>> Handle(
        GenerateAccountingFiscalYearCommand cmd,
        CancellationToken ct
    )
    {
        var existing = (await _repo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct)).ToList();
        var layout = AccountingPeriodStructure.MonthlyLayout(cmd.FiscalYear);
        var yearPeriods = existing
            .Where(p => p.FiscalYear == cmd.FiscalYear)
            .OrderBy(p => p.StartDate)
            .ToList();

        // Compatible = prefijo exacto de los meses naturales (mismo número y rango); cualquier otra
        // forma (período anual, rangos no mensuales) no se divide ni se completa.
        var compatible =
            yearPeriods.Count <= layout.Count
            && yearPeriods
                .Select((p, i) => (Period: p, Slot: layout[i]))
                .All(x =>
                    x.Period.PeriodNumber == x.Slot.PeriodNumber
                    && x.Period.StartDate == x.Slot.StartDate
                    && x.Period.EndDate == x.Slot.EndDate
                );
        if (!compatible)
            return Result<AccountingFiscalYearDto>.Conflict(
                $"El ejercicio {cmd.FiscalYear} ya tiene una estructura de períodos incompatible con la generación mensual."
            );

        var created = new List<AccountingPeriod>();
        foreach (var slot in layout.Skip(yearPeriods.Count))
        {
            var violation = AccountingPeriodStructure.Validate(existing, slot);
            if (violation is not null)
                return violation.IsOverlap
                    ? Result<AccountingFiscalYearDto>.Conflict(violation.Message)
                    : Result<AccountingFiscalYearDto>.ValidationFailure(violation.Message);

            var period = AccountingPeriod.Create(
                _t.TenantId,
                _c.CompanyId,
                slot.FiscalYear,
                slot.PeriodNumber,
                slot.StartDate,
                slot.EndDate,
                _u.UserId
            );
            existing.Add(period);
            created.Add(period);
        }

        if (created.Count > 0)
        {
            try
            {
                foreach (var period in created)
                    await _repo.AddAsync(period, ct);
                await _repo.SaveChangesAsync(ct);
            }
            catch (Exception ex)
                when (_dbEx.TryGetUniqueViolation(ex, out _)
                    || _dbEx.ClassifyFailureCode(ex) == ApiResponseCodes.Common.Conflict)
            {
                return Result<AccountingFiscalYearDto>.Conflict(AccountingPeriodMap.ConcurrentOverlapMessage);
            }
        }

        return Result<AccountingFiscalYearDto>.Success(
            new AccountingFiscalYearDto(
                cmd.FiscalYear,
                created.Count,
                yearPeriods.Concat(created).Select(p => AccountingPeriodMap.ToDto(p)).ToList()
            )
        );
    }
}

public sealed class CloseAccountingPeriodHandler
    : IRequestHandler<CloseAccountingPeriodCommand, Result<AccountingPeriodDto>>
{
    private readonly IAccountingPeriodRepository _repo;
    private readonly IAccountingCompletenessEvaluator _completeness;
    private readonly IAccountingOperationalPolicyRepository _accountingLock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyClock _clock;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CloseAccountingPeriodHandler(
        IAccountingPeriodRepository repo,
        IAccountingCompletenessEvaluator completeness,
        IAccountingOperationalPolicyRepository accountingLock,
        IUnitOfWork unitOfWork,
        ICompanyClock clock,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _completeness = completeness;
        _accountingLock = accountingLock;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<AccountingPeriodDto>> Handle(
        CloseAccountingPeriodCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // ACCOUNTING-CONTINUITY-01: lock exclusivo por Company — ninguna obligación contable
            // nueva puede comitearse entre la evaluación del gate y el cierre (accounting.md §8).
            await _accountingLock.AcquireExclusiveLockAsync(companyId, ct);

            var period = await _repo.GetByIdAsync(tenantId, companyId, cmd.Id, ct);
            if (period is null)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<AccountingPeriodDto>.NotFound("Período contable no encontrado.");
            }

            // ACCOUNTING-07: solo se cierra un período ya terminado según la fecha de la empresa
            // (ICompanyClock, nunca UTC) — cerrar el período en curso rechazaría toda operación con
            // fecha en el resto del rango y no existe reapertura.
            if (period.Status == PeriodStatus.Open)
            {
                var today = await _clock.TodayAsync(companyId, tenantId, ct);
                if (period.EndDate >= today)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<AccountingPeriodDto>.ValidationFailure(
                        AccountingPeriodMap.NotEndedMessage(period, today)
                    );
                }
            }

            // Fase 5.5 (ADR-026 §6.1/§9) / ACCOUNTING-CONTINUITY-06: precondición cross-aggregate
            // resuelta aquí (Application) con el readiness único, nunca dentro de AccountingPeriod —
            // el aggregate solo recibe el resumen ya calculado.
            var completeness = await _completeness.EvaluateClosureAsync(tenantId, companyId, period, ct);

            period.Close(_u.UserId, AccountingCompletenessService.ToClosureReadiness(completeness));

            await _repo.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<AccountingPeriodDto>.Success(AccountingPeriodMap.ToDto(period));
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}

public sealed class LockAccountingPeriodHandler
    : IRequestHandler<LockAccountingPeriodCommand, Result<AccountingPeriodDto>>
{
    private readonly IAccountingPeriodRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public LockAccountingPeriodHandler(
        IAccountingPeriodRepository repo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<AccountingPeriodDto>> Handle(
        LockAccountingPeriodCommand cmd,
        CancellationToken ct
    )
    {
        var period = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, cmd.Id, ct);
        if (period is null)
            return Result<AccountingPeriodDto>.NotFound("Período contable no encontrado.");

        period.Lock(_u.UserId);

        await _repo.SaveChangesAsync(ct);
        return Result<AccountingPeriodDto>.Success(AccountingPeriodMap.ToDto(period));
    }
}

// ── Query Handlers ──────────────────────────────────────────────────────

public sealed class GetAccountingPeriodsHandler
    : IRequestHandler<GetAccountingPeriodsQuery, Result<IReadOnlyList<AccountingPeriodDto>>>
{
    private readonly IAccountingPeriodRepository _repo;
    private readonly IAccessRepository _access;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetAccountingPeriodsHandler(
        IAccountingPeriodRepository repo,
        IAccessRepository access,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _access = access;
        _t = t;
        _c = c;
    }

    public async Task<Result<IReadOnlyList<AccountingPeriodDto>>> Handle(
        GetAccountingPeriodsQuery q,
        CancellationToken ct
    )
    {
        var items = await _repo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct);
        var names = await AccountingPeriodMap.UserNamesAsync(_access, items, ct);
        return Result<IReadOnlyList<AccountingPeriodDto>>.Success(
            items.Select(p => AccountingPeriodMap.ToDto(p, names)).ToList()
        );
    }
}

public sealed class GetAccountingPeriodByIdHandler
    : IRequestHandler<GetAccountingPeriodByIdQuery, Result<AccountingPeriodDto>>
{
    private readonly IAccountingPeriodRepository _repo;
    private readonly IAccessRepository _access;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetAccountingPeriodByIdHandler(
        IAccountingPeriodRepository repo,
        IAccessRepository access,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _access = access;
        _t = t;
        _c = c;
    }

    public async Task<Result<AccountingPeriodDto>> Handle(
        GetAccountingPeriodByIdQuery q,
        CancellationToken ct
    )
    {
        var period = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, q.Id, ct);
        if (period is null)
            return Result<AccountingPeriodDto>.NotFound("Período contable no encontrado.");
        var names = await AccountingPeriodMap.UserNamesAsync(_access, [period], ct);
        return Result<AccountingPeriodDto>.Success(AccountingPeriodMap.ToDto(period, names));
    }
}

public sealed class GetAccountingPeriodClosureReadinessHandler
    : IRequestHandler<GetAccountingPeriodClosureReadinessQuery, Result<AccountingPeriodClosureReadinessDto>>
{
    private readonly IAccountingPeriodRepository _repo;
    private readonly IAccountingCompletenessEvaluator _completeness;
    private readonly ICompanyClock _clock;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetAccountingPeriodClosureReadinessHandler(
        IAccountingPeriodRepository repo,
        IAccountingCompletenessEvaluator completeness,
        ICompanyClock clock,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _completeness = completeness;
        _clock = clock;
        _t = t;
        _c = c;
    }

    public async Task<Result<AccountingPeriodClosureReadinessDto>> Handle(
        GetAccountingPeriodClosureReadinessQuery q,
        CancellationToken ct
    )
    {
        var period = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, q.Id, ct);
        if (period is null)
            return Result<AccountingPeriodClosureReadinessDto>.NotFound("Período contable no encontrado.");

        var today = await _clock.TodayAsync(_c.CompanyId, _t.TenantId, ct);
        var hasEnded = period.EndDate < today;

        // ACCOUNTING-07: Closed/Locked no son elegibles — se informa explícitamente, sin evaluar.
        if (period.Status != PeriodStatus.Open)
            return Result<AccountingPeriodClosureReadinessDto>.Success(
                new(period.Id, period.Status.ToString(), false, hasEnded, today, false, null)
            );

        var completeness = await _completeness.EvaluateClosureAsync(_t.TenantId, _c.CompanyId, period, ct);
        return Result<AccountingPeriodClosureReadinessDto>.Success(
            new(
                period.Id,
                period.Status.ToString(),
                true,
                hasEnded,
                today,
                hasEnded && completeness.IsComplete,
                completeness
            )
        );
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

internal static class AccountingPeriodMap
{
    public const string ConcurrentOverlapMessage =
        "Otro usuario registró un período que se superpone con este rango. Actualice la lista e intente nuevamente.";

    public static string NotEndedMessage(AccountingPeriod p, DateOnly today) =>
        $"El período {p.FiscalYear}-{p.PeriodNumber} termina el {p.EndDate:yyyy-MM-dd}: solo puede cerrarse "
        + $"a partir del día siguiente (hoy es {today:yyyy-MM-dd} según la fecha de la empresa).";

    public static async Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(
        IAccessRepository access,
        IEnumerable<AccountingPeriod> periods,
        CancellationToken ct
    )
    {
        var ids = periods
            .SelectMany(p => new[] { p.ClosedBy, p.LockedBy })
            .OfType<Guid>()
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();
        return (await access.GetUsersByIdsAsync(ids, ct)).ToDictionary(u => u.Id, u => u.FullName);
    }

    public static AccountingPeriodDto ToDto(
        AccountingPeriod p,
        IReadOnlyDictionary<Guid, string>? names = null
    )
    {
        string? NameOf(Guid? id) =>
            id is { } value && names is not null && names.TryGetValue(value, out var name) ? name : null;

        return new(
            p.Id,
            p.FiscalYear,
            p.PeriodNumber,
            p.StartDate,
            p.EndDate,
            p.Status.ToString(),
            p.ClosedAtUtc,
            p.ClosedBy,
            NameOf(p.ClosedBy),
            p.LockedAtUtc,
            p.LockedBy,
            NameOf(p.LockedBy),
            p.CreatedAt,
            p.UpdatedAt
        );
    }
}
