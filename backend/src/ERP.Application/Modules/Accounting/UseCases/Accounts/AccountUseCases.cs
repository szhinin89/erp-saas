using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Accounting.UseCases.Accounts;

// ── Commands ────────────────────────────────────────────────────────────

public sealed record CreateAccountCommand(
    string Code,
    string Name,
    Guid? ParentAccountId,
    AccountType AccountType,
    AccountNature Nature,
    bool AllowsPosting
) : IRequest<Result<AccountDto>>, ICompanyScopedRequest;

/// <summary>
/// ACCOUNTING-CHART-OF-ACCOUNTS-02: reemplaza el antiguo RenameAccountCommand (Name únicamente,
/// sin consumidores todavía) por un único comando de edición — Code/AccountType/Nature quedan
/// deliberadamente fuera: cambiarlos post-facto corrompería la clasificación de asientos ya
/// contabilizados contra esta cuenta (mismo criterio de "clasificación universal, no editable"
/// ya documentado en AccountType/AccountNature).
/// </summary>
public sealed record UpdateAccountCommand(
    Guid Id,
    string Name,
    Guid? ParentAccountId,
    bool AllowsPosting
) : IRequest<Result<AccountDto>>, ICompanyScopedRequest;

public sealed record EnableAccountCommand(Guid Id)
    : IRequest<Result<AccountDto>>,
        ICompanyScopedRequest;

public sealed record DisableAccountCommand(Guid Id)
    : IRequest<Result<AccountDto>>,
        ICompanyScopedRequest;

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetAccountsQuery()
    : IRequest<Result<IReadOnlyList<AccountDto>>>,
        ICompanyScopedRequest;

public sealed record GetAccountByIdQuery(Guid Id)
    : IRequest<Result<AccountDto>>,
        ICompanyScopedRequest;

/// <summary>ACCOUNTING-CHART-OF-ACCOUNTS-02 — el repositorio ya soportaba esta búsqueda (FindByCodeAsync), sin query expuesta.</summary>
public sealed record GetAccountByCodeQuery(string Code)
    : IRequest<Result<AccountDto>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AccountType).IsInEnum();
        RuleFor(x => x.Nature).IsInEnum();
    }
}

public sealed class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public sealed class EnableAccountCommandValidator : AbstractValidator<EnableAccountCommand>
{
    public EnableAccountCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class DisableAccountCommandValidator : AbstractValidator<DisableAccountCommand>
{
    public DisableAccountCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class GetAccountByCodeQueryValidator : AbstractValidator<GetAccountByCodeQuery>
{
    public GetAccountByCodeQueryValidator() => RuleFor(x => x.Code).NotEmpty();
}

// ── Command Handlers ────────────────────────────────────────────────────

public sealed class CreateAccountHandler : IRequestHandler<CreateAccountCommand, Result<AccountDto>>
{
    private readonly IAccountRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public CreateAccountHandler(
        IAccountRepository repo,
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

    public async Task<Result<AccountDto>> Handle(CreateAccountCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        // Pre-check: primera línea de defensa (no la única — ver uq_accounts_company_code +
        // IDatabaseExceptionTranslator más abajo).
        var existing = await _repo.FindByCodeAsync(tenantId, companyId, cmd.Code, ct);
        if (existing is not null)
            return Result<AccountDto>.Conflict(
                $"Ya existe una cuenta con el código '{cmd.Code}' en esta empresa."
            );

        var byId = (await _repo.GetByCompanyAsync(tenantId, companyId, ct)).ToDictionary(a => a.Id);

        // ACCOUNTING-CHART-OF-ACCOUNTS-02: existencia/pertenencia del padre — invariante
        // cross-aggregate deliberadamente diferida por Account.cs a esta capa (ver <remarks>).
        // Sin ciclo posible en creación: la cuenta nueva todavía no tiene Id ni hijos.
        if (cmd.ParentAccountId is { } parentId && !byId.ContainsKey(parentId))
            return Result<AccountDto>.ValidationFailure(
                "La cuenta padre indicada no existe en esta empresa."
            );

        try
        {
            var code = AccountCode.Create(cmd.Code);
            var account = Account.Create(
                tenantId,
                companyId,
                code,
                cmd.Name,
                cmd.ParentAccountId,
                cmd.AccountType,
                cmd.Nature,
                cmd.AllowsPosting,
                _u.UserId
            );

            await _repo.AddAsync(account, ct);
            await _repo.SaveChangesAsync(ct);
            byId[account.Id] = account;
            return Result<AccountDto>.Success(Map.ToDto(account, byId));
        }
        catch (ArgumentException ex)
        {
            return Result<AccountDto>.ValidationFailure(ex.Message);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<AccountDto>.Conflict(
                $"Ya existe una cuenta con el código '{cmd.Code}' en esta empresa."
            );
        }
    }
}

public sealed class UpdateAccountHandler : IRequestHandler<UpdateAccountCommand, Result<AccountDto>>
{
    private readonly IAccountRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public UpdateAccountHandler(
        IAccountRepository repo,
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

    public async Task<Result<AccountDto>> Handle(UpdateAccountCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var account = await _repo.GetByIdAsync(tenantId, companyId, cmd.Id, ct);
        if (account is null)
            return Result<AccountDto>.NotFound("Cuenta no encontrada.");

        var byId = (await _repo.GetByCompanyAsync(tenantId, companyId, ct)).ToDictionary(a => a.Id);

        if (cmd.ParentAccountId is { } parentId)
        {
            if (parentId == cmd.Id)
                return Result<AccountDto>.ValidationFailure(
                    "Una cuenta no puede ser su propio padre."
                );
            if (!byId.ContainsKey(parentId))
                return Result<AccountDto>.ValidationFailure(
                    "La cuenta padre indicada no existe en esta empresa."
                );
            if (CreatesCycle(cmd.Id, parentId, byId))
                return Result<AccountDto>.ValidationFailure(
                    "No se puede asignar ese padre: generaría un ciclo en el Plan de Cuentas."
                );
        }

        try
        {
            account.Rename(cmd.Name, _u.UserId);
            account.UpdateParent(cmd.ParentAccountId, _u.UserId);
            account.SetAllowsPosting(cmd.AllowsPosting, _u.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<AccountDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        byId[account.Id] = account;
        return Result<AccountDto>.Success(Map.ToDto(account, byId));
    }

    /// <summary>
    /// True si, al reparentar <paramref name="accountId"/> bajo <paramref name="proposedParentId"/>,
    /// la cadena de ancestros de este último termina llegando de vuelta a <paramref name="accountId"/>
    /// (es decir, <paramref name="accountId"/> es hoy ancestro de <paramref name="proposedParentId"/>).
    /// Guarda de profundidad (tamaño del Plan de Cuentas) por si ya existiera un ciclo previo —
    /// nunca debería ocurrir dado que esta misma validación lo previene en origen.
    /// </summary>
    private static bool CreatesCycle(
        Guid accountId,
        Guid proposedParentId,
        IReadOnlyDictionary<Guid, Account> byId
    )
    {
        var current = proposedParentId;
        var guard = byId.Count + 1;
        while (guard-- > 0)
        {
            if (current == accountId)
                return true;
            if (!byId.TryGetValue(current, out var node) || node.ParentAccountId is not { } next)
                return false;
            current = next;
        }
        return true;
    }
}

public sealed class EnableAccountHandler : IRequestHandler<EnableAccountCommand, Result<AccountDto>>
{
    private readonly IAccountRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public EnableAccountHandler(
        IAccountRepository repo,
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

    public async Task<Result<AccountDto>> Handle(EnableAccountCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var account = await _repo.GetByIdAsync(tenantId, companyId, cmd.Id, ct);
        if (account is null)
            return Result<AccountDto>.NotFound("Cuenta no encontrada.");

        // Account.Activate() — el Command se llama "Enable" por consistencia de vocabulario
        // REST con PostingRule.Enable(); el método de dominio conserva su nombre original de
        // Fase 1 (Account.Activate()), con su evento AccountActivatedEvent ya existente.
        try
        {
            account.Activate(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<AccountDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        var byId = (await _repo.GetByCompanyAsync(tenantId, companyId, ct)).ToDictionary(a => a.Id);
        return Result<AccountDto>.Success(Map.ToDto(account, byId));
    }
}

public sealed class DisableAccountHandler
    : IRequestHandler<DisableAccountCommand, Result<AccountDto>>
{
    private readonly IAccountRepository _repo;
    private readonly IPostingRuleRepository _postingRuleRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public DisableAccountHandler(
        IAccountRepository repo,
        IPostingRuleRepository postingRuleRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _postingRuleRepo = postingRuleRepo;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<AccountDto>> Handle(DisableAccountCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var account = await _repo.GetByIdAsync(tenantId, companyId, cmd.Id, ct);
        if (account is null)
            return Result<AccountDto>.NotFound("Cuenta no encontrada.");

        // ACCOUNTING-CHART-OF-ACCOUNTS-02: el Posting Engine (JournalFactory/PostingRuleResolver)
        // no valida hoy que las cuentas resueltas desde PostingRule sigan activas — deshabilitar
        // una cuenta todavía referenciada por una regla activa produciría asientos futuros contra
        // una cuenta inactiva sin ningún error. Bloquear aquí evita ese estado inconsistente sin
        // tocar el Posting Engine (fuera de alcance de este ticket). No bloquea por asientos ya
        // contabilizados (JournalEntryLine histórico) — eso es historial, no un riesgo futuro.
        var rules = await _postingRuleRepo.GetByCompanyAsync(tenantId, companyId, ct);
        var usedByActiveRule = rules.Any(r =>
            r.IsActive
            && (
                r.DebitAccountId == cmd.Id
                || r.CreditAccountId == cmd.Id
                || r.Lines.Any(l => l.AccountId == cmd.Id)
            )
        );
        if (usedByActiveRule)
            return Result<AccountDto>.Conflict(
                "No se puede desactivar: la cuenta está referenciada por una Regla de Contabilización activa."
            );

        try
        {
            account.Disable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<AccountDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        var byId = (await _repo.GetByCompanyAsync(tenantId, companyId, ct)).ToDictionary(a => a.Id);
        return Result<AccountDto>.Success(Map.ToDto(account, byId));
    }
}

// ── Query Handlers ──────────────────────────────────────────────────────

public sealed class GetAccountsHandler
    : IRequestHandler<GetAccountsQuery, Result<IReadOnlyList<AccountDto>>>
{
    private readonly IAccountRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetAccountsHandler(IAccountRepository repo, ICurrentTenant t, ICurrentCompany c)
    {
        _repo = repo;
        _t = t;
        _c = c;
    }

    public async Task<Result<IReadOnlyList<AccountDto>>> Handle(
        GetAccountsQuery q,
        CancellationToken ct
    )
    {
        var items = await _repo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct);
        var byId = items.ToDictionary(a => a.Id);
        return Result<IReadOnlyList<AccountDto>>.Success(
            items.Select(a => Map.ToDto(a, byId)).ToList()
        );
    }
}

public sealed class GetAccountByIdHandler : IRequestHandler<GetAccountByIdQuery, Result<AccountDto>>
{
    private readonly IAccountRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetAccountByIdHandler(IAccountRepository repo, ICurrentTenant t, ICurrentCompany c)
    {
        _repo = repo;
        _t = t;
        _c = c;
    }

    public async Task<Result<AccountDto>> Handle(GetAccountByIdQuery q, CancellationToken ct)
    {
        var account = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, q.Id, ct);
        if (account is null)
            return Result<AccountDto>.NotFound("Cuenta no encontrada.");

        var byId = (
            await _repo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct)
        ).ToDictionary(a => a.Id);
        return Result<AccountDto>.Success(Map.ToDto(account, byId));
    }
}

public sealed class GetAccountByCodeHandler
    : IRequestHandler<GetAccountByCodeQuery, Result<AccountDto>>
{
    private readonly IAccountRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetAccountByCodeHandler(IAccountRepository repo, ICurrentTenant t, ICurrentCompany c)
    {
        _repo = repo;
        _t = t;
        _c = c;
    }

    public async Task<Result<AccountDto>> Handle(GetAccountByCodeQuery q, CancellationToken ct)
    {
        var account = await _repo.FindByCodeAsync(_t.TenantId, _c.CompanyId, q.Code, ct);
        if (account is null)
            return Result<AccountDto>.NotFound("Cuenta no encontrada.");

        var byId = (
            await _repo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct)
        ).ToDictionary(a => a.Id);
        return Result<AccountDto>.Success(Map.ToDto(account, byId));
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static AccountDto ToDto(Account a, IReadOnlyDictionary<Guid, Account> byId)
    {
        Account? parent = a.ParentAccountId is { } pid && byId.TryGetValue(pid, out var p) ? p : null;

        return new(
            a.Id,
            a.Code.Value,
            a.Name,
            a.ParentAccountId,
            parent?.Code.Value,
            parent?.Name,
            ComputeLevel(a, byId),
            a.AccountType.ToString(),
            a.Nature.ToString(),
            a.AllowsPosting,
            a.IsActive,
            a.CreatedAt,
            a.UpdatedAt
        );
    }

    /// <summary>0 para una cuenta raíz; +1 por cada ancestro. Guarda de profundidad defensiva
    /// (tamaño del Plan de Cuentas) — nunca debería activarse dado que Application impide crear
    /// ciclos al escribir (ver UpdateAccountHandler.CreatesCycle).</summary>
    private static int ComputeLevel(Account a, IReadOnlyDictionary<Guid, Account> byId)
    {
        var level = 0;
        var current = a;
        var guard = byId.Count + 1;
        while (current.ParentAccountId is { } parentId && guard-- > 0)
        {
            if (!byId.TryGetValue(parentId, out var parent))
                break;
            level++;
            current = parent;
        }
        return level;
    }
}
