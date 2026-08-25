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

/// <summary>
/// ACCOUNTING-POSTING-RULES-AUDIT-03 — decisión explícita sobre la convivencia de
/// <c>DebitAccountId</c>/<c>CreditAccountId</c> (legacy) con <c>Lines</c>: <b>Lines es la única
/// fuente que <see cref="Posting.JournalFactory"/> lee para construir un asiento</b> —
/// DebitAccountId/CreditAccountId nunca producen una <c>JournalEntryLine</c> (ver remarks de
/// <see cref="Posting.JournalFactory"/>). Por eso <see cref="CreatePostingRuleCommand"/> exige
/// <c>Lines.Count &gt;= 2</c> ("líneas efectivas") — una regla con solo los campos legacy
/// poblados y sin <c>Lines</c> se guardaría pero jamás produciría un asiento (fallaría en
/// <c>JournalValidator</c> con "menos de 2 líneas" recién cuando llegue el primer hecho real,
/// un error tardío y evitable). Los campos legacy siguen aceptándose y validándose (existencia/
/// misma Company/activa/AllowsPosting) por compatibilidad de contrato — nunca se eliminan en este
/// ticket sin auditoría (ver entregable) — pero son puramente informativos en tiempo de ejecución.
/// </summary>
public sealed record CreatePostingRuleCommand(
    string SourceModule,
    string FactType,
    Guid? DebitAccountId,
    Guid? CreditAccountId,
    string? TaxCode,
    IReadOnlyList<PostingRuleLineInput>? Lines = null
) : IRequest<Result<PostingRuleDto>>, ICompanyScopedRequest;

/// <summary>
/// No modifica <c>Lines</c> (sin consumidor todavía que edite líneas post-creación — fuera de
/// alcance de ACCOUNTING-POSTING-RULES-AUDIT-03, que es auditoría/endurecimiento, no rediseño).
/// Solo los campos legacy DebitAccountId/CreditAccountId se validan aquí (existencia/misma
/// Company/activa/AllowsPosting) cuando se proveen — mismo criterio que Create.
/// </summary>
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
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public CreatePostingRuleHandler(
        IPostingRuleRepository repo,
        IAccountRepository accountRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u,
        IDatabaseExceptionTranslator dbEx
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
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

        // ACCOUNTING-POSTING-RULES-AUDIT-03: "líneas efectivas" — ver remarks de
        // CreatePostingRuleCommand. Sin esto, una regla con solo DebitAccountId/CreditAccountId
        // (legacy, nunca leídos por JournalFactory) se guardaría inservible.
        if (cmd.Lines is null || cmd.Lines.Count < 2)
            return Result<PostingRuleDto>.ValidationFailure(
                "La regla necesita al menos 2 líneas (Lines) — es lo único que el Posting Engine "
                    + "lee para construir un asiento; DebitAccountId/CreditAccountId son heredados "
                    + "y no producen líneas de asiento."
            );

        var accountIds = cmd
            .Lines.Select(l => l.AccountId)
            .Concat(cmd.DebitAccountId is { } d ? new[] { d } : Array.Empty<Guid>())
            .Concat(cmd.CreditAccountId is { } cr ? new[] { cr } : Array.Empty<Guid>())
            .Distinct();

        var accountError = await PostingRuleAccountValidation.ValidateAsync(
            _accountRepo,
            tenantId,
            companyId,
            accountIds,
            ct
        );
        if (accountError is not null)
            return Result<PostingRuleDto>.ValidationFailure(accountError);

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

            foreach (var line in cmd.Lines)
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
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public UpdatePostingRuleHandler(
        IPostingRuleRepository repo,
        IAccountRepository accountRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PostingRuleDto>> Handle(
        UpdatePostingRuleCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var rule = await _repo.GetByIdAsync(tenantId, companyId, cmd.Id, ct);
        if (rule is null)
            return Result<PostingRuleDto>.NotFound("Regla de contabilización no encontrada.");

        var accountIds = (cmd.DebitAccountId is { } d ? new[] { d } : Array.Empty<Guid>())
            .Concat(cmd.CreditAccountId is { } cr ? new[] { cr } : Array.Empty<Guid>())
            .Distinct();

        var accountError = await PostingRuleAccountValidation.ValidateAsync(
            _accountRepo,
            tenantId,
            companyId,
            accountIds,
            ct
        );
        if (accountError is not null)
            return Result<PostingRuleDto>.ValidationFailure(accountError);

        rule.UpdateMapping(cmd.DebitAccountId, cmd.CreditAccountId, cmd.TaxCode, _u.UserId);

        await _repo.SaveChangesAsync(ct);
        return Result<PostingRuleDto>.Success(Map.ToDto(rule));
    }
}

/// <summary>
/// ACCOUNTING-POSTING-RULES-AUDIT-03: validación de cuentas compartida por Create/Update — toda
/// cuenta referenciada por una PostingRule (Lines o legacy DebitAccountId/CreditAccountId) debe
/// existir, pertenecer a la misma Company/Tenant, estar activa y admitir movimiento
/// (AllowsPosting). Misma regla que <see cref="Posting.PostingAccountGuard"/> aplica en tiempo de
/// ejecución — esta es la validación de configuración (falla rápido al guardar en vez de recién
/// cuando llegue el primer hecho contable real).
/// </summary>
file static class PostingRuleAccountValidation
{
    public static async Task<string?> ValidateAsync(
        IAccountRepository accountRepo,
        Guid tenantId,
        Guid companyId,
        IEnumerable<Guid> accountIds,
        CancellationToken ct
    )
    {
        foreach (var accountId in accountIds)
        {
            var account = await accountRepo.GetByIdAsync(tenantId, companyId, accountId, ct);
            if (account is null)
                return $"La cuenta '{accountId}' no existe en esta empresa.";
            if (!account.IsActive)
                return $"La cuenta '{account.Code.Value}' ({account.Name}) está inactiva y no puede usarse en una regla de contabilización.";
            if (!account.AllowsPosting)
                return $"La cuenta '{account.Code.Value}' ({account.Name}) no admite movimientos (AllowsPosting = false).";
        }
        return null;
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
