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
    string Code, string Name, Guid? ParentAccountId,
    AccountType AccountType, AccountNature Nature, bool AllowsPosting)
    : IRequest<Result<AccountDto>>, ICompanyScopedRequest;

public sealed record RenameAccountCommand(Guid Id, string Name) : IRequest<Result<AccountDto>>, ICompanyScopedRequest;

public sealed record EnableAccountCommand(Guid Id) : IRequest<Result<AccountDto>>, ICompanyScopedRequest;

public sealed record DisableAccountCommand(Guid Id) : IRequest<Result<AccountDto>>, ICompanyScopedRequest;

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetAccountsQuery() : IRequest<Result<IReadOnlyList<AccountDto>>>, ICompanyScopedRequest;

public sealed record GetAccountByIdQuery(Guid Id) : IRequest<Result<AccountDto>>, ICompanyScopedRequest;

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

public sealed class RenameAccountCommandValidator : AbstractValidator<RenameAccountCommand>
{
    public RenameAccountCommandValidator()
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

// ── Command Handlers ────────────────────────────────────────────────────

public sealed class CreateAccountHandler : IRequestHandler<CreateAccountCommand, Result<AccountDto>>
{
    private readonly IAccountRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public CreateAccountHandler(
        IAccountRepository repo, ICurrentTenant t, ICurrentCompany c, ICurrentUser u,
        IDatabaseExceptionTranslator dbEx)
    {
        _repo = repo; _t = t; _c = c; _u = u; _dbEx = dbEx;
    }

    public async Task<Result<AccountDto>> Handle(CreateAccountCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        // Pre-check: primera línea de defensa (no la única — ver uq_accounts_company_code +
        // IDatabaseExceptionTranslator más abajo).
        var existing = await _repo.FindByCodeAsync(tenantId, companyId, cmd.Code, ct);
        if (existing is not null)
            return Result<AccountDto>.Conflict($"Ya existe una cuenta con el código '{cmd.Code}' en esta empresa.");

        try
        {
            var code = AccountCode.Create(cmd.Code);
            var account = Account.Create(
                tenantId, companyId, code, cmd.Name, cmd.ParentAccountId,
                cmd.AccountType, cmd.Nature, cmd.AllowsPosting, _u.UserId);

            await _repo.AddAsync(account, ct);
            await _repo.SaveChangesAsync(ct);
            return Result<AccountDto>.Success(Map.ToDto(account));
        }
        catch (ArgumentException ex)
        {
            return Result<AccountDto>.ValidationFailure(ex.Message);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<AccountDto>.Conflict($"Ya existe una cuenta con el código '{cmd.Code}' en esta empresa.");
        }
    }
}

public sealed class RenameAccountHandler : IRequestHandler<RenameAccountCommand, Result<AccountDto>>
{
    private readonly IAccountRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public RenameAccountHandler(IAccountRepository repo, ICurrentTenant t, ICurrentCompany c, ICurrentUser u)
    {
        _repo = repo; _t = t; _c = c; _u = u;
    }

    public async Task<Result<AccountDto>> Handle(RenameAccountCommand cmd, CancellationToken ct)
    {
        var account = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, cmd.Id, ct);
        if (account is null) return Result<AccountDto>.NotFound("Cuenta no encontrada.");

        try { account.Rename(cmd.Name, _u.UserId); }
        catch (ArgumentException ex) { return Result<AccountDto>.ValidationFailure(ex.Message); }

        await _repo.SaveChangesAsync(ct);
        return Result<AccountDto>.Success(Map.ToDto(account));
    }
}

public sealed class EnableAccountHandler : IRequestHandler<EnableAccountCommand, Result<AccountDto>>
{
    private readonly IAccountRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public EnableAccountHandler(IAccountRepository repo, ICurrentTenant t, ICurrentCompany c, ICurrentUser u)
    {
        _repo = repo; _t = t; _c = c; _u = u;
    }

    public async Task<Result<AccountDto>> Handle(EnableAccountCommand cmd, CancellationToken ct)
    {
        var account = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, cmd.Id, ct);
        if (account is null) return Result<AccountDto>.NotFound("Cuenta no encontrada.");

        // Account.Activate() — el Command se llama "Enable" por consistencia de vocabulario
        // REST con PostingRule.Enable(); el método de dominio conserva su nombre original de
        // Fase 1 (Account.Activate()), con su evento AccountActivatedEvent ya existente.
        try { account.Activate(_u.UserId); }
        catch (InvalidOperationException ex) { return Result<AccountDto>.ValidationFailure(ex.Message); }

        await _repo.SaveChangesAsync(ct);
        return Result<AccountDto>.Success(Map.ToDto(account));
    }
}

public sealed class DisableAccountHandler : IRequestHandler<DisableAccountCommand, Result<AccountDto>>
{
    private readonly IAccountRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public DisableAccountHandler(IAccountRepository repo, ICurrentTenant t, ICurrentCompany c, ICurrentUser u)
    {
        _repo = repo; _t = t; _c = c; _u = u;
    }

    public async Task<Result<AccountDto>> Handle(DisableAccountCommand cmd, CancellationToken ct)
    {
        var account = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, cmd.Id, ct);
        if (account is null) return Result<AccountDto>.NotFound("Cuenta no encontrada.");

        try { account.Disable(_u.UserId); }
        catch (InvalidOperationException ex) { return Result<AccountDto>.ValidationFailure(ex.Message); }

        await _repo.SaveChangesAsync(ct);
        return Result<AccountDto>.Success(Map.ToDto(account));
    }
}

// ── Query Handlers ──────────────────────────────────────────────────────

public sealed class GetAccountsHandler : IRequestHandler<GetAccountsQuery, Result<IReadOnlyList<AccountDto>>>
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

    public async Task<Result<IReadOnlyList<AccountDto>>> Handle(GetAccountsQuery q, CancellationToken ct)
    {
        var items = await _repo.GetByCompanyAsync(_t.TenantId, _c.CompanyId, ct);
        return Result<IReadOnlyList<AccountDto>>.Success(items.Select(Map.ToDto).ToList());
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
        return account is null
            ? Result<AccountDto>.NotFound("Cuenta no encontrada.")
            : Result<AccountDto>.Success(Map.ToDto(account));
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static AccountDto ToDto(Account a) => new(
        a.Id, a.Code.Value, a.Name, a.ParentAccountId,
        a.AccountType.ToString(), a.Nature.ToString(),
        a.AllowsPosting, a.IsActive, a.CreatedAt, a.UpdatedAt);
}
