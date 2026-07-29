using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Accounting.UseCases.PostingRules;

// ── Commands ────────────────────────────────────────────────────────────

/// <summary>
/// Fase 5.6.2 — línea real que consumirá JournalFactory (<c>PostingRule.Lines</c>). Antes de esta
/// fase, <c>CreatePostingRuleCommand</c> solo poblaba los campos planos legacy
/// (DebitAccountId/CreditAccountId), que JournalFactory ya no lee — cualquier regla creada sin
/// líneas producía asientos con cero líneas en producción. <c>Lines</c> es opcional para no
/// romper la forma anterior del comando, pero es la única vía real de dejar una regla funcional.
/// </summary>
public sealed record PostingRuleLineInput(
    Guid AccountId,
    AccountNature Nature,
    PostingAmountKind AmountKind
);

public sealed record CreatePostingRuleCommand(
    string SourceModule,
    string FactType,
    Guid? DebitAccountId,
    Guid? CreditAccountId,
    string? TaxCode,
    IReadOnlyList<PostingRuleLineInput>? Lines = null
) : IRequest<Result<PostingRuleDto>>, ICompanyScopedRequest;

public sealed record UpdatePostingRuleCommand(
    Guid Id,
    Guid? DebitAccountId,
    Guid? CreditAccountId,
    string? TaxCode
) : IRequest<Result<PostingRuleDto>>, ICompanyScopedRequest;

public sealed record EnablePostingRuleCommand(Guid Id)
    : IRequest<Result<PostingRuleDto>>,
        ICompanyScopedRequest;

public sealed record DisablePostingRuleCommand(Guid Id)
    : IRequest<Result<PostingRuleDto>>,
        ICompanyScopedRequest;

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetPostingRulesQuery()
    : IRequest<Result<IReadOnlyList<PostingRuleDto>>>,
        ICompanyScopedRequest;

public sealed record GetPostingRuleByIdQuery(Guid Id)
    : IRequest<Result<PostingRuleDto>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class PostingRuleLineInputValidator : AbstractValidator<PostingRuleLineInput>
{
    public PostingRuleLineInputValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Nature).IsInEnum();
        RuleFor(x => x.AmountKind).IsInEnum();
    }
}

public sealed class CreatePostingRuleCommandValidator : AbstractValidator<CreatePostingRuleCommand>
{
    public CreatePostingRuleCommandValidator()
    {
        RuleFor(x => x.SourceModule).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FactType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TaxCode).MaximumLength(10).When(x => x.TaxCode is not null);
        RuleForEach(x => x.Lines)
            .SetValidator(new PostingRuleLineInputValidator())
            .When(x => x.Lines is not null);
    }
}

public sealed class UpdatePostingRuleCommandValidator : AbstractValidator<UpdatePostingRuleCommand>
{
    public UpdatePostingRuleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TaxCode).MaximumLength(10).When(x => x.TaxCode is not null);
    }
}

public sealed class EnablePostingRuleCommandValidator : AbstractValidator<EnablePostingRuleCommand>
{
    public EnablePostingRuleCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class DisablePostingRuleCommandValidator
    : AbstractValidator<DisablePostingRuleCommand>
{
    public DisablePostingRuleCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

// ── Command Handlers ────────────────────────────────────────────────────

public sealed class CreatePostingRuleHandler
    : IRequestHandler<CreatePostingRuleCommand, Result<PostingRuleDto>>
{
    private readonly IPostingRuleRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public CreatePostingRuleHandler(
        IPostingRuleRepository repo,
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

    public async Task<Result<PostingRuleDto>> Handle(
        CreatePostingRuleCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        // Pre-check: primera línea de defensa (no la única — ver uq_posting_rules_company_source_fact
        // + IDatabaseExceptionTranslator más abajo).
        var existing = await _repo.FindByKeyAsync(
            tenantId,
            companyId,
            cmd.SourceModule,
            cmd.FactType,
            ct
        );
        if (existing is not null)
            return Result<PostingRuleDto>.Conflict(
                $"Ya existe una regla de contabilización para '{cmd.SourceModule}'/'{cmd.FactType}' en esta empresa."
            );

        try
        {
            var rule = PostingRule.Create(
                tenantId,
                companyId,
                cmd.SourceModule,
                cmd.FactType,
                cmd.DebitAccountId,
                cmd.CreditAccountId,
                cmd.TaxCode,
                _u.UserId
            );

            foreach (var line in cmd.Lines ?? Array.Empty<PostingRuleLineInput>())
                rule.AddLine(line.AccountId, line.Nature, line.AmountKind);

            await _repo.AddAsync(rule, ct);
            await _repo.SaveChangesAsync(ct);
            return Result<PostingRuleDto>.Success(Map.ToDto(rule));
        }
        catch (ArgumentException ex)
        {
            return Result<PostingRuleDto>.ValidationFailure(ex.Message);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<PostingRuleDto>.Conflict(
                $"Ya existe una regla de contabilización para '{cmd.SourceModule}'/'{cmd.FactType}' en esta empresa."
            );
        }
    }
}

public sealed class UpdatePostingRuleHandler
    : IRequestHandler<UpdatePostingRuleCommand, Result<PostingRuleDto>>
{
    private readonly IPostingRuleRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public UpdatePostingRuleHandler(
        IPostingRuleRepository repo,
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

    public async Task<Result<PostingRuleDto>> Handle(
        UpdatePostingRuleCommand cmd,
        CancellationToken ct
    )
    {
        var rule = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, cmd.Id, ct);
        if (rule is null)
            return Result<PostingRuleDto>.NotFound("Regla de contabilización no encontrada.");

        rule.UpdateMapping(cmd.DebitAccountId, cmd.CreditAccountId, cmd.TaxCode, _u.UserId);

        await _repo.SaveChangesAsync(ct);
        return Result<PostingRuleDto>.Success(Map.ToDto(rule));
    }
}

public sealed class EnablePostingRuleHandler
    : IRequestHandler<EnablePostingRuleCommand, Result<PostingRuleDto>>
{
    private readonly IPostingRuleRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public EnablePostingRuleHandler(
        IPostingRuleRepository repo,
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

    public async Task<Result<PostingRuleDto>> Handle(
        EnablePostingRuleCommand cmd,
        CancellationToken ct
    )
    {
        var rule = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, cmd.Id, ct);
        if (rule is null)
            return Result<PostingRuleDto>.NotFound("Regla de contabilización no encontrada.");

        try
        {
            rule.Enable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PostingRuleDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<PostingRuleDto>.Success(Map.ToDto(rule));
    }
}

public sealed class DisablePostingRuleHandler
    : IRequestHandler<DisablePostingRuleCommand, Result<PostingRuleDto>>
{
    private readonly IPostingRuleRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public DisablePostingRuleHandler(
        IPostingRuleRepository repo,
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

    public async Task<Result<PostingRuleDto>> Handle(
        DisablePostingRuleCommand cmd,
        CancellationToken ct
    )
    {
        var rule = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, cmd.Id, ct);
        if (rule is null)
            return Result<PostingRuleDto>.NotFound("Regla de contabilización no encontrada.");

        try
        {
            rule.Disable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PostingRuleDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<PostingRuleDto>.Success(Map.ToDto(rule));
    }
}

// ── Query Handlers ──────────────────────────────────────────────────────

public sealed class GetPostingRulesHandler
    : IRequestHandler<GetPostingRulesQuery, Result<IReadOnlyList<PostingRuleDto>>>
{
    private readonly IPostingRuleRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetPostingRulesHandler(IPostingRuleRepository repo, ICurrentTenant t, ICurrentCompany c)
    {
        _repo = repo;
        _t = t;
        _c = c;
    }

    public async Task<Result<IReadOnlyList<PostingRuleDto>>> Handle(
        GetPostingRulesQuery q,
        CancellationToken ct
    )
    {
        var items = await _repo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct);
        return Result<IReadOnlyList<PostingRuleDto>>.Success(items.Select(Map.ToDto).ToList());
    }
}

public sealed class GetPostingRuleByIdHandler
    : IRequestHandler<GetPostingRuleByIdQuery, Result<PostingRuleDto>>
{
    private readonly IPostingRuleRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetPostingRuleByIdHandler(
        IPostingRuleRepository repo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
    }

    public async Task<Result<PostingRuleDto>> Handle(
        GetPostingRuleByIdQuery q,
        CancellationToken ct
    )
    {
        var rule = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, q.Id, ct);
        return rule is null
            ? Result<PostingRuleDto>.NotFound("Regla de contabilización no encontrada.")
            : Result<PostingRuleDto>.Success(Map.ToDto(rule));
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static PostingRuleDto ToDto(PostingRule r) =>
        new(
            r.Id,
            r.SourceModule,
            r.FactType,
            r.DebitAccountId,
            r.CreditAccountId,
            r.TaxCode,
            r.IsActive,
            r.Lines.Select(l => new PostingRuleLineDto(
                    l.Id,
                    l.AccountId,
                    l.Nature.ToString(),
                    l.AmountKind.ToString(),
                    l.SortOrder
                ))
                .ToList(),
            r.CreatedAt,
            r.UpdatedAt
        );
}
