using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Accounting.UseCases.PostingRules;

// ── Commands ────────────────────────────────────────────────────────────

/// <summary>Línea efectiva de una regla: lo único que <c>JournalFactory</c> lee para construir un asiento.</summary>
public sealed record PostingRuleLineInput(
    Guid AccountId,
    AccountNature Nature,
    PostingAmountKind AmountKind
);

/// <summary>
/// ACCOUNTING-11 — crea la regla de un hecho de <see cref="PostingFactCatalog"/> (nunca una clave
/// libre). Las líneas se validan contra la forma del hecho y las cuentas de la Company.
/// </summary>
public sealed record CreatePostingRuleCommand(
    string SourceModule,
    string FactType,
    IReadOnlyList<PostingRuleLineInput> Lines
) : IRequest<Result<PostingRuleDto>>, ICompanyScopedRequest;

/// <summary>
/// ACCOUNTING-11 — reemplaza por completo las líneas de la regla (agregar/quitar líneas, cuenta,
/// dirección y tipo de monto). Solo afecta contabilizaciones futuras; los asientos ya publicados
/// no se tocan.
/// </summary>
public sealed record UpdatePostingRuleCommand(Guid Id, IReadOnlyList<PostingRuleLineInput> Lines)
    : IRequest<Result<PostingRuleDto>>,
        ICompanyScopedRequest;

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

/// <summary>ACCOUNTING-11 — hechos soportados y la regla de la Company para cada uno (faltantes con RuleId null).</summary>
public sealed record GetPostingFactCatalogQuery()
    : IRequest<Result<IReadOnlyList<PostingFactCatalogItemDto>>>,
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
        RuleFor(x => x.Lines).NotNull();
        RuleForEach(x => x.Lines).SetValidator(new PostingRuleLineInputValidator());
    }
}

public sealed class UpdatePostingRuleCommandValidator : AbstractValidator<UpdatePostingRuleCommand>
{
    public UpdatePostingRuleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Lines).NotNull();
        RuleForEach(x => x.Lines).SetValidator(new PostingRuleLineInputValidator());
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

        var fact = PostingFactCatalog.Find(cmd.SourceModule, cmd.FactType);
        if (fact is null)
            return Result<PostingRuleDto>.ValidationFailure(
                $"'{cmd.SourceModule}'/'{cmd.FactType}' no es un hecho contable soportado."
            );

        // Pre-check: primera línea de defensa (no la única — ver uq_posting_rules_company_source_fact
        // + IDatabaseExceptionTranslator más abajo).
        var existing = await _repo.FindByKeyAsync(
            tenantId,
            companyId,
            fact.SourceModule,
            fact.FactType,
            ct
        );
        if (existing is not null)
            return Result<PostingRuleDto>.Conflict(
                $"Ya existe una regla de contabilización para '{fact.SourceModule}'/'{fact.FactType}' en esta empresa."
            );

        var error = await PostingRuleLinesValidation.ValidateAsync(
            _accountRepo,
            tenantId,
            companyId,
            fact,
            cmd.Lines.Select(l => (l.AccountId, l.Nature, l.AmountKind)).ToList(),
            ct
        );
        if (error is not null)
            return Result<PostingRuleDto>.ValidationFailure(error);

        try
        {
            var rule = PostingRule.Create(
                tenantId,
                companyId,
                fact.SourceModule,
                fact.FactType,
                _u.UserId
            );

            foreach (var line in cmd.Lines)
                rule.AddLine(line.AccountId, line.Nature, line.AmountKind);

            await _repo.AddAsync(rule, ct);
            await _repo.SaveChangesAsync(ct);
            var accounts = await _accountRepo.GetByCompanyAsync(tenantId, companyId, ct);
            return Result<PostingRuleDto>.Success(
                Map.ToDto(rule, accounts.ToDictionary(a => a.Id))
            );
        }
        catch (ArgumentException ex)
        {
            return Result<PostingRuleDto>.ValidationFailure(ex.Message);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<PostingRuleDto>.Conflict(
                $"Ya existe una regla de contabilización para '{fact.SourceModule}'/'{fact.FactType}' en esta empresa."
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

        var fact = PostingFactCatalog.Find(rule.SourceModule, rule.FactType);
        if (fact is null)
            return Result<PostingRuleDto>.ValidationFailure(
                $"'{rule.SourceModule}'/'{rule.FactType}' ya no es un hecho contable soportado; la regla no se edita."
            );

        var lines = cmd.Lines.Select(l => (l.AccountId, l.Nature, l.AmountKind)).ToList();
        var error = await PostingRuleLinesValidation.ValidateAsync(
            _accountRepo,
            tenantId,
            companyId,
            fact,
            lines,
            ct
        );
        if (error is not null)
            return Result<PostingRuleDto>.ValidationFailure(error);

        rule.ReplaceLines(lines, _u.UserId);

        await _repo.SaveChangesAsync(ct);
        var accounts = await _accountRepo.GetByCompanyAsync(tenantId, companyId, ct);
        return Result<PostingRuleDto>.Success(Map.ToDto(rule, accounts.ToDictionary(a => a.Id)));
    }
}

/// <summary>
/// ACCOUNTING-11 — validación única de Create/Update/Enable: forma de líneas según el hecho del
/// catálogo y cuentas existentes en la misma Company/Tenant, activas y con AllowsPosting (misma
/// regla que <see cref="Posting.PostingAccountGuard"/> aplica en tiempo de ejecución; aquí falla
/// rápido al guardar).
/// </summary>
file static class PostingRuleLinesValidation
{
    public static async Task<string?> ValidateAsync(
        IAccountRepository accountRepo,
        Guid tenantId,
        Guid companyId,
        PostingFactDefinition fact,
        IReadOnlyCollection<(Guid AccountId, AccountNature Nature, PostingAmountKind AmountKind)> lines,
        CancellationToken ct
    )
    {
        var shapeError = PostingFactCatalog.ValidateLineShape(
            fact,
            lines.Select(l => (l.Nature, l.AmountKind)).ToList()
        );
        if (shapeError is not null)
            return shapeError;

        foreach (var accountId in lines.Select(l => l.AccountId).Distinct())
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
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public EnablePostingRuleHandler(
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
        EnablePostingRuleCommand cmd,
        CancellationToken ct
    )
    {
        var rule = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, cmd.Id, ct);
        if (rule is null)
            return Result<PostingRuleDto>.NotFound("Regla de contabilización no encontrada.");

        // ACCOUNTING-11 — solo se reactiva una regla válida: hecho soportado y líneas vigentes.
        var fact = PostingFactCatalog.Find(rule.SourceModule, rule.FactType);
        if (fact is null)
            return Result<PostingRuleDto>.ValidationFailure(
                $"'{rule.SourceModule}'/'{rule.FactType}' ya no es un hecho contable soportado; la regla no se reactiva."
            );
        var error = await PostingRuleLinesValidation.ValidateAsync(
            _accountRepo,
            _t.TenantId,
            _c.CompanyId,
            fact,
            rule.Lines.Select(l => (l.AccountId, l.Nature, l.AmountKind)).ToList(),
            ct
        );
        if (error is not null)
            return Result<PostingRuleDto>.ValidationFailure(error);

        try
        {
            rule.Enable(_u.UserId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Result<PostingRuleDto>.Conflict(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        var accounts = await _accountRepo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct);
        return Result<PostingRuleDto>.Success(Map.ToDto(rule, accounts.ToDictionary(a => a.Id)));
    }
}

public sealed class DisablePostingRuleHandler
    : IRequestHandler<DisablePostingRuleCommand, Result<PostingRuleDto>>
{
    private readonly IPostingRuleRepository _repo;
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public DisablePostingRuleHandler(
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
        catch (DomainRuleViolationException ex)
        {
            return Result<PostingRuleDto>.Conflict(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        var accounts = await _accountRepo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct);
        return Result<PostingRuleDto>.Success(Map.ToDto(rule, accounts.ToDictionary(a => a.Id)));
    }
}

// ── Query Handlers ──────────────────────────────────────────────────────

public sealed class GetPostingRulesHandler
    : IRequestHandler<GetPostingRulesQuery, Result<IReadOnlyList<PostingRuleDto>>>
{
    private readonly IPostingRuleRepository _repo;
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetPostingRulesHandler(
        IPostingRuleRepository repo,
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

    public async Task<Result<IReadOnlyList<PostingRuleDto>>> Handle(
        GetPostingRulesQuery q,
        CancellationToken ct
    )
    {
        var items = await _repo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct);
        var accounts = await _accountRepo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct);
        var accountsById = accounts.ToDictionary(a => a.Id);
        return Result<IReadOnlyList<PostingRuleDto>>.Success(
            items.Select(r => Map.ToDto(r, accountsById)).ToList()
        );
    }
}

public sealed class GetPostingRuleByIdHandler
    : IRequestHandler<GetPostingRuleByIdQuery, Result<PostingRuleDto>>
{
    private readonly IPostingRuleRepository _repo;
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetPostingRuleByIdHandler(
        IPostingRuleRepository repo,
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

    public async Task<Result<PostingRuleDto>> Handle(
        GetPostingRuleByIdQuery q,
        CancellationToken ct
    )
    {
        var rule = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, q.Id, ct);
        if (rule is null)
            return Result<PostingRuleDto>.NotFound("Regla de contabilización no encontrada.");

        var accounts = await _accountRepo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct);
        return Result<PostingRuleDto>.Success(Map.ToDto(rule, accounts.ToDictionary(a => a.Id)));
    }
}

public sealed class GetPostingFactCatalogHandler
    : IRequestHandler<GetPostingFactCatalogQuery, Result<IReadOnlyList<PostingFactCatalogItemDto>>>
{
    private readonly IPostingRuleRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetPostingFactCatalogHandler(
        IPostingRuleRepository repo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
    }

    public async Task<Result<IReadOnlyList<PostingFactCatalogItemDto>>> Handle(
        GetPostingFactCatalogQuery q,
        CancellationToken ct
    )
    {
        var rules = await _repo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct);
        var byKey = rules.ToDictionary(r => (r.SourceModule, r.FactType));
        return Result<IReadOnlyList<PostingFactCatalogItemDto>>.Success(
            PostingFactCatalog
                .All.Select(f =>
                {
                    byKey.TryGetValue((f.SourceModule, f.FactType), out var rule);
                    return new PostingFactCatalogItemDto(
                        f.SourceModule,
                        f.FactType,
                        f.AllowedAmountKinds.Select(k => k.ToString()).ToList(),
                        f.DynamicSides.HasFlag(PostingDynamicSides.Debit),
                        f.DynamicSides.HasFlag(PostingDynamicSides.Credit),
                        rule?.Id,
                        rule?.IsActive
                    );
                })
                .ToList()
        );
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

/// <summary>
/// ACCOUNTING-POSTING-RULES-UI-12: <paramref name="accountsById"/> se resuelve una sola vez por
/// request (todas las cuentas de la Company, ya paginado por naturaleza — el Plan de Cuentas es
/// siempre pequeño, mismo criterio que <c>JournalEntryLineDto</c>) — nunca N+1 por línea. Una
/// línea cuya cuenta no aparece en el diccionario (borrado físico imposible por regla del
/// proyecto, pero defensivo ante datos inconsistentes) cae a placeholders explícitos en vez de
/// lanzar, para que la pantalla de auditoría pueda seguir mostrando el resto de la regla.
/// </summary>
file static class Map
{
    public static PostingRuleDto ToDto(
        PostingRule r,
        IReadOnlyDictionary<Guid, Account> accountsById
    ) =>
        new(
            r.Id,
            r.SourceModule,
            r.FactType,
            r.IsActive,
            PostingFactCatalog.Find(r.SourceModule, r.FactType) is not null,
            r.Lines.OrderBy(l => l.SortOrder).Select(l => ToLineDto(l, accountsById)).ToList(),
            r.CreatedAt,
            r.UpdatedAt
        );

    private static PostingRuleLineDto ToLineDto(
        PostingRuleLine l,
        IReadOnlyDictionary<Guid, Account> accountsById
    )
    {
        accountsById.TryGetValue(l.AccountId, out var account);
        return new PostingRuleLineDto(
            l.Id,
            l.AccountId,
            account?.Code.Value ?? "—",
            account?.Name ?? "Cuenta no encontrada",
            account?.AccountType.ToString() ?? "—",
            account?.Nature.ToString() ?? "—",
            account?.IsActive ?? false,
            account?.AllowsPosting ?? false,
            l.Nature.ToString(),
            l.AmountKind.ToString(),
            l.SortOrder
        );
    }
}
