using ERP.Application.Common;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases.CreditTerms;

// ── Shared input ────────────────────────────────────────────────────────

public sealed record InstallmentInput(int Number, int DaysOffset, decimal Percentage);

// ── Commands & Queries ──────────────────────────────────────────────────

public sealed record CreateCreditTermCommand(
    string Code,
    string Name,
    CreditTermMode Mode,
    int TotalDays,
    List<InstallmentInput>? Installments = null
) : IRequest<Result<CreditTermDto>>, ICompanyScopedRequest;

public sealed record UpdateCreditTermCommand(
    Guid Id,
    string Name,
    CreditTermMode Mode,
    int TotalDays,
    List<InstallmentInput>? Installments = null
) : IRequest<Result<CreditTermDto>>, ICompanyScopedRequest;

public sealed record EnableCreditTermCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

public sealed record DisableCreditTermCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

public sealed record GetCreditTermByIdQuery(Guid Id)
    : IRequest<Result<CreditTermDto>>,
        ICompanyScopedRequest;

public sealed record GetCreditTermsQuery(bool? IsActive = null, string? Search = null)
    : IRequest<Result<IReadOnlyList<CreditTermDto>>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreateCreditTermCommandValidator : AbstractValidator<CreateCreditTermCommand>
{
    public CreateCreditTermCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CreditTerm.MaxCodeLength);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CreditTerm.MaxNameLength);
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.TotalDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Installments)
            .NotEmpty()
            .When(x => x.Mode == CreditTermMode.FinancialStrict)
            .WithMessage("El modo FinancialStrict requiere al menos una cuota.");
        RuleForEach(x => x.Installments)
            .ChildRules(i =>
            {
                i.RuleFor(x => x.Percentage).GreaterThan(0).LessThanOrEqualTo(100);
                i.RuleFor(x => x.DaysOffset).GreaterThanOrEqualTo(0);
                i.RuleFor(x => x.Number).GreaterThan(0);
            })
            .When(x => x.Installments is { Count: > 0 });
    }
}

public sealed class UpdateCreditTermCommandValidator : AbstractValidator<UpdateCreditTermCommand>
{
    public UpdateCreditTermCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CreditTerm.MaxNameLength);
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.TotalDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Installments)
            .NotEmpty()
            .When(x => x.Mode == CreditTermMode.FinancialStrict)
            .WithMessage("El modo FinancialStrict requiere al menos una cuota.");
        RuleForEach(x => x.Installments)
            .ChildRules(i =>
            {
                i.RuleFor(x => x.Percentage).GreaterThan(0).LessThanOrEqualTo(100);
                i.RuleFor(x => x.DaysOffset).GreaterThanOrEqualTo(0);
                i.RuleFor(x => x.Number).GreaterThan(0);
            })
            .When(x => x.Installments is { Count: > 0 });
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class CreateCreditTermHandler
    : IRequestHandler<CreateCreditTermCommand, Result<CreditTermDto>>
{
    private readonly ICreditTermRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CreateCreditTermHandler(
        ICreditTermRepository repo,
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

    public async Task<Result<CreditTermDto>> Handle(
        CreateCreditTermCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        if (await _repo.CodeExistsAsync(tid, cmd.Code, null, ct))
            return Result<CreditTermDto>.Conflict($"Código '{cmd.Code}' duplicado.");

        var installments = cmd.Installments?.Select(i => (i.Number, i.DaysOffset, i.Percentage));

        try
        {
            var term = CreditTerm.Create(
                tid,
                _c.CompanyId,
                cmd.Code,
                cmd.Name,
                cmd.Mode,
                cmd.TotalDays,
                _u.UserId,
                installments
            );
            await _repo.AddAsync(term, ct);
            await _repo.SaveChangesAsync(ct);
            return Result<CreditTermDto>.Success(Map.ToDto(term));
        }
        catch (InvalidOperationException ex)
        {
            return Result<CreditTermDto>.ValidationFailure(ex.Message);
        }
    }
}

public sealed class UpdateCreditTermHandler
    : IRequestHandler<UpdateCreditTermCommand, Result<CreditTermDto>>
{
    private readonly ICreditTermRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public UpdateCreditTermHandler(ICreditTermRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<CreditTermDto>> Handle(
        UpdateCreditTermCommand cmd,
        CancellationToken ct
    )
    {
        var term = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (term is null)
            return Result<CreditTermDto>.NotFound("Condición de crédito no encontrada.");

        var installments = cmd.Installments?.Select(i => (i.Number, i.DaysOffset, i.Percentage));

        try
        {
            term.Update(cmd.Name, cmd.Mode, cmd.TotalDays, _u.UserId, installments);
            await _repo.SaveChangesAsync(ct);
            return Result<CreditTermDto>.Success(Map.ToDto(term));
        }
        catch (InvalidOperationException ex)
        {
            return Result<CreditTermDto>.ValidationFailure(ex.Message);
        }
    }
}

public sealed class EnableCreditTermHandler : IRequestHandler<EnableCreditTermCommand, Result<bool>>
{
    private readonly ICreditTermRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public EnableCreditTermHandler(ICreditTermRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<bool>> Handle(EnableCreditTermCommand cmd, CancellationToken ct)
    {
        var term = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (term is null)
            return Result<bool>.NotFound("No encontrada.");
        try
        {
            term.Enable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class DisableCreditTermHandler
    : IRequestHandler<DisableCreditTermCommand, Result<bool>>
{
    private readonly ICreditTermRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public DisableCreditTermHandler(ICreditTermRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<bool>> Handle(DisableCreditTermCommand cmd, CancellationToken ct)
    {
        var term = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (term is null)
            return Result<bool>.NotFound("No encontrada.");
        try
        {
            term.Disable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class GetCreditTermByIdHandler
    : IRequestHandler<GetCreditTermByIdQuery, Result<CreditTermDto>>
{
    private readonly ICreditTermRepository _repo;
    private readonly ICurrentTenant _t;

    public GetCreditTermByIdHandler(ICreditTermRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<CreditTermDto>> Handle(GetCreditTermByIdQuery q, CancellationToken ct)
    {
        var term = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        return term is null
            ? Result<CreditTermDto>.NotFound("No encontrada.")
            : Result<CreditTermDto>.Success(Map.ToDto(term));
    }
}

public sealed class GetCreditTermsHandler
    : IRequestHandler<GetCreditTermsQuery, Result<IReadOnlyList<CreditTermDto>>>
{
    private readonly ICreditTermRepository _repo;
    private readonly ICurrentTenant _t;

    public GetCreditTermsHandler(ICreditTermRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<CreditTermDto>>> Handle(
        GetCreditTermsQuery q,
        CancellationToken ct
    )
    {
        var all = await _repo.GetAllAsync(_t.TenantId, q.IsActive, q.Search, ct);
        return Result<IReadOnlyList<CreditTermDto>>.Success(all.Select(Map.ToDto).ToList());
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static CreditTermDto ToDto(CreditTerm t) =>
        new(
            t.Id,
            t.Code,
            t.Name,
            t.Mode.ToString(),
            t.TotalDays,
            t.IsActive,
            t.Installments.OrderBy(i => i.InstallmentNumber)
                .Select(i => new CreditInstallmentDto(
                    i.Id,
                    i.InstallmentNumber,
                    i.DaysOffset,
                    i.Percentage
                ))
                .ToList(),
            t.CreatedAt,
            t.UpdatedAt
        );
}
