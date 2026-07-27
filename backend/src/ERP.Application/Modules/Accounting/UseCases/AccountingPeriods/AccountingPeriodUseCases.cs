using FluentValidation;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;

// ── Commands ────────────────────────────────────────────────────────────

public sealed record CreateAccountingPeriodCommand(
    int FiscalYear, int PeriodNumber, DateOnly StartDate, DateOnly EndDate)
    : IRequest<Result<AccountingPeriodDto>>, ICompanyScopedRequest;

public sealed record CloseAccountingPeriodCommand(Guid Id) : IRequest<Result<AccountingPeriodDto>>, ICompanyScopedRequest;

public sealed record LockAccountingPeriodCommand(Guid Id) : IRequest<Result<AccountingPeriodDto>>, ICompanyScopedRequest;

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetAccountingPeriodsQuery()
    : IRequest<Result<IReadOnlyList<AccountingPeriodDto>>>, ICompanyScopedRequest;

public sealed record GetAccountingPeriodByIdQuery(Guid Id)
    : IRequest<Result<AccountingPeriodDto>>, ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreateAccountingPeriodCommandValidator : AbstractValidator<CreateAccountingPeriodCommand>
{
    public CreateAccountingPeriodCommandValidator()
    {
        RuleFor(x => x.FiscalYear).GreaterThan(0);
        RuleFor(x => x.PeriodNumber).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("La fecha de fin debe ser posterior a la fecha de inicio.");
    }
}

public sealed class CloseAccountingPeriodCommandValidator : AbstractValidator<CloseAccountingPeriodCommand>
{
    public CloseAccountingPeriodCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class LockAccountingPeriodCommandValidator : AbstractValidator<LockAccountingPeriodCommand>
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
        IAccountingPeriodRepository repo, ICurrentTenant t, ICurrentCompany c, ICurrentUser u,
        IDatabaseExceptionTranslator dbEx)
    {
        _repo = repo; _t = t; _c = c; _u = u; _dbEx = dbEx;
    }

    public async Task<Result<AccountingPeriodDto>> Handle(CreateAccountingPeriodCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        // Pre-check: invariante "sin períodos solapados" (ADR-026 §6.1) — cross-aggregate, sin
        // respaldo de UNIQUE en BD para el caso de solapamiento (solo para FiscalYear+PeriodNumber
        // exacto). Riesgo de carrera aceptado — operación administrativa de baja concurrencia.
        var overlapping = await _repo.GetOverlappingAsync(tenantId, companyId, cmd.StartDate, cmd.EndDate, ct);
        if (overlapping.Count > 0)
            return Result<AccountingPeriodDto>.Conflict(
                "El rango de fechas se superpone con un período contable existente de esta empresa.");

        try
        {
            var period = AccountingPeriod.Create(
                tenantId, companyId, cmd.FiscalYear, cmd.PeriodNumber, cmd.StartDate, cmd.EndDate, _u.UserId);

            await _repo.AddAsync(period, ct);
            await _repo.SaveChangesAsync(ct);
            return Result<AccountingPeriodDto>.Success(Map.ToDto(period));
        }
        catch (ArgumentException ex)
        {
            return Result<AccountingPeriodDto>.ValidationFailure(ex.Message);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<AccountingPeriodDto>.Conflict(
                $"Ya existe el período {cmd.FiscalYear}-{cmd.PeriodNumber} en esta empresa.");
        }
    }
}

public sealed class CloseAccountingPeriodHandler
    : IRequestHandler<CloseAccountingPeriodCommand, Result<AccountingPeriodDto>>
{
    private readonly IAccountingPeriodRepository _repo;
    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CloseAccountingPeriodHandler(
        IAccountingPeriodRepository repo, IJournalEntryRepository journalEntryRepository,
        ICurrentTenant t, ICurrentCompany c, ICurrentUser u)
    {
        _repo = repo; _journalEntryRepository = journalEntryRepository; _t = t; _c = c; _u = u;
    }

    public async Task<Result<AccountingPeriodDto>> Handle(CloseAccountingPeriodCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var period = await _repo.GetByIdAsync(tenantId, companyId, cmd.Id, ct);
        if (period is null) return Result<AccountingPeriodDto>.NotFound("Período contable no encontrado.");

        // Fase 5.5 (ADR-026 §6.1/§9): precondición cross-aggregate resuelta aquí (Application),
        // nunca dentro de AccountingPeriod — el aggregate solo recibe el resumen ya calculado.
        var readiness = await _journalEntryRepository.GetClosureReadinessAsync(tenantId, companyId, period.Id, ct);

        try { period.Close(_u.UserId, readiness); }
        catch (InvalidOperationException ex) { return Result<AccountingPeriodDto>.ValidationFailure(ex.Message); }

        await _repo.SaveChangesAsync(ct);
        return Result<AccountingPeriodDto>.Success(Map.ToDto(period));
    }
}

public sealed class LockAccountingPeriodHandler
    : IRequestHandler<LockAccountingPeriodCommand, Result<AccountingPeriodDto>>
{
    private readonly IAccountingPeriodRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public LockAccountingPeriodHandler(IAccountingPeriodRepository repo, ICurrentTenant t, ICurrentCompany c, ICurrentUser u)
    {
        _repo = repo; _t = t; _c = c; _u = u;
    }

    public async Task<Result<AccountingPeriodDto>> Handle(LockAccountingPeriodCommand cmd, CancellationToken ct)
    {
        var period = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, cmd.Id, ct);
        if (period is null) return Result<AccountingPeriodDto>.NotFound("Período contable no encontrado.");

        try { period.Lock(_u.UserId); }
        catch (InvalidOperationException ex) { return Result<AccountingPeriodDto>.ValidationFailure(ex.Message); }

        await _repo.SaveChangesAsync(ct);
        return Result<AccountingPeriodDto>.Success(Map.ToDto(period));
    }
}

// ── Query Handlers ──────────────────────────────────────────────────────

public sealed class GetAccountingPeriodsHandler
    : IRequestHandler<GetAccountingPeriodsQuery, Result<IReadOnlyList<AccountingPeriodDto>>>
{
    private readonly IAccountingPeriodRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetAccountingPeriodsHandler(IAccountingPeriodRepository repo, ICurrentTenant t, ICurrentCompany c)
    {
        _repo = repo;
        _t = t;
        _c = c;
    }

    public async Task<Result<IReadOnlyList<AccountingPeriodDto>>> Handle(GetAccountingPeriodsQuery q, CancellationToken ct)
    {
        var items = await _repo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct);
        return Result<IReadOnlyList<AccountingPeriodDto>>.Success(items.Select(Map.ToDto).ToList());
    }
}

public sealed class GetAccountingPeriodByIdHandler
    : IRequestHandler<GetAccountingPeriodByIdQuery, Result<AccountingPeriodDto>>
{
    private readonly IAccountingPeriodRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetAccountingPeriodByIdHandler(IAccountingPeriodRepository repo, ICurrentTenant t, ICurrentCompany c)
    {
        _repo = repo;
        _t = t;
        _c = c;
    }

    public async Task<Result<AccountingPeriodDto>> Handle(GetAccountingPeriodByIdQuery q, CancellationToken ct)
    {
        var period = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, q.Id, ct);
        return period is null
            ? Result<AccountingPeriodDto>.NotFound("Período contable no encontrado.")
            : Result<AccountingPeriodDto>.Success(Map.ToDto(period));
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static AccountingPeriodDto ToDto(AccountingPeriod p) => new(
        p.Id, p.FiscalYear, p.PeriodNumber, p.StartDate, p.EndDate,
        p.Status.ToString(), p.ClosedAtUtc, p.ClosedBy, p.CreatedAt, p.UpdatedAt);
}
