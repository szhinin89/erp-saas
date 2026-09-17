using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases;

// ── DTO ─────────────────────────────────────────────────────────────────

/// <summary>TREASURY-BANK-ACCOUNTS-01: proyección de lectura de <see cref="CompanyBankAccount"/>.</summary>
public sealed record CompanyBankAccountDto(
    Guid Id,
    Guid BankId,
    string AccountType,
    string AccountNumber,
    string DisplayName,
    Guid AccountingAccountId,
    bool IsActive
);

// ── Commands / Queries ──────────────────────────────────────────────────

public sealed record CreateCompanyBankAccountCommand(
    Guid BankId,
    BankAccountType AccountType,
    string AccountNumber,
    string DisplayName,
    Guid AccountingAccountId
) : IRequest<Result<CompanyBankAccountDto>>, ICompanyScopedRequest;

public sealed record UpdateCompanyBankAccountCommand(
    Guid Id,
    string DisplayName,
    Guid AccountingAccountId
) : IRequest<Result<CompanyBankAccountDto>>, ICompanyScopedRequest;

public sealed record SetCompanyBankAccountActiveCommand(Guid Id, bool IsActive)
    : IRequest<Result<CompanyBankAccountDto>>,
        ICompanyScopedRequest;

public sealed record GetCompanyBankAccountByIdQuery(Guid Id)
    : IRequest<Result<CompanyBankAccountDto>>,
        ICompanyScopedRequest;

public sealed record GetCompanyBankAccountListQuery(bool? IsActive)
    : IRequest<Result<IReadOnlyList<CompanyBankAccountDto>>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreateCompanyBankAccountValidator : AbstractValidator<CreateCompanyBankAccountCommand>
{
    public CreateCompanyBankAccountValidator()
    {
        RuleFor(x => x.BankId).NotEmpty().WithMessage("El banco es obligatorio.");
        RuleFor(x => x.AccountType).IsInEnum();
        RuleFor(x => x.AccountNumber)
            .NotEmpty()
            .MaximumLength(CompanyBankAccount.AccountNumberMaxLen)
            .WithMessage("El número de cuenta es obligatorio (máximo 50 caracteres).");
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(CompanyBankAccount.DisplayNameMaxLen)
            .WithMessage("El alias/nombre visible es obligatorio (máximo 200 caracteres).");
        RuleFor(x => x.AccountingAccountId)
            .NotEmpty()
            .WithMessage("La cuenta contable es obligatoria.");
    }
}

public sealed class UpdateCompanyBankAccountValidator : AbstractValidator<UpdateCompanyBankAccountCommand>
{
    public UpdateCompanyBankAccountValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(CompanyBankAccount.DisplayNameMaxLen)
            .WithMessage("El alias/nombre visible es obligatorio (máximo 200 caracteres).");
        RuleFor(x => x.AccountingAccountId)
            .NotEmpty()
            .WithMessage("La cuenta contable es obligatoria.");
    }
}

public sealed class SetCompanyBankAccountActiveValidator
    : AbstractValidator<SetCompanyBankAccountActiveCommand>
{
    public SetCompanyBankAccountActiveValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class CreateCompanyBankAccountHandler
    : IRequestHandler<CreateCompanyBankAccountCommand, Result<CompanyBankAccountDto>>
{
    private readonly ICompanyBankAccountRepository _repo;
    private readonly IBankRepository _banks;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CreateCompanyBankAccountHandler(
        ICompanyBankAccountRepository repo,
        IBankRepository banks,
        IAccountRepository accounts,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _banks = banks;
        _accounts = accounts;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<CompanyBankAccountDto>> Handle(
        CreateCompanyBankAccountCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var cid = _c.CompanyId;

        var bank = await _banks.GetByIdAsync(tid, cmd.BankId, ct);
        if (bank is null)
            return Result<CompanyBankAccountDto>.NotFound("El banco indicado no existe.");
        if (!bank.IsActive)
            return Result<CompanyBankAccountDto>.ValidationFailure("El banco indicado está inactivo.");

        var account = await _accounts.GetByIdAsync(tid, cid, cmd.AccountingAccountId, ct);
        if (account is null)
            return Result<CompanyBankAccountDto>.NotFound(
                "La cuenta contable indicada no existe o no pertenece a esta empresa."
            );
        if (!account.IsActive || !account.AllowsPosting)
            return Result<CompanyBankAccountDto>.ValidationFailure(
                "La cuenta contable indicada no es postable o está inactiva."
            );

        var duplicate = await _repo.ExistsAsync(
            tid,
            cid,
            cmd.BankId,
            (int)cmd.AccountType,
            cmd.AccountNumber,
            ct: ct
        );
        if (duplicate)
            return Result<CompanyBankAccountDto>.UniqueViolation(
                "Ya existe una cuenta bancaria con el mismo banco, tipo y número para esta empresa."
            );

        CompanyBankAccount entity;
        try
        {
            entity = CompanyBankAccount.Create(
                tid,
                cid,
                cmd.BankId,
                cmd.AccountType,
                cmd.AccountNumber,
                cmd.DisplayName,
                cmd.AccountingAccountId,
                _u.UserId
            );
        }
        catch (ArgumentException ex)
        {
            return Result<CompanyBankAccountDto>.ValidationFailure(ex.Message);
        }

        await _repo.AddAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<CompanyBankAccountDto>.Success(Map.ToDto(entity));
    }
}

public sealed class UpdateCompanyBankAccountHandler
    : IRequestHandler<UpdateCompanyBankAccountCommand, Result<CompanyBankAccountDto>>
{
    private readonly ICompanyBankAccountRepository _repo;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public UpdateCompanyBankAccountHandler(
        ICompanyBankAccountRepository repo,
        IAccountRepository accounts,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _accounts = accounts;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<CompanyBankAccountDto>> Handle(
        UpdateCompanyBankAccountCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var cid = _c.CompanyId;

        var entity = await _repo.GetByIdAsync(tid, cmd.Id, ct);
        if (entity is null)
            return Result<CompanyBankAccountDto>.NotFound("Cuenta bancaria no encontrada.");

        var account = await _accounts.GetByIdAsync(tid, cid, cmd.AccountingAccountId, ct);
        if (account is null)
            return Result<CompanyBankAccountDto>.NotFound(
                "La cuenta contable indicada no existe o no pertenece a esta empresa."
            );
        if (!account.IsActive || !account.AllowsPosting)
            return Result<CompanyBankAccountDto>.ValidationFailure(
                "La cuenta contable indicada no es postable o está inactiva."
            );

        try
        {
            entity.Update(cmd.DisplayName, cmd.AccountingAccountId, _u.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<CompanyBankAccountDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<CompanyBankAccountDto>.Success(Map.ToDto(entity));
    }
}

public sealed class SetCompanyBankAccountActiveHandler
    : IRequestHandler<SetCompanyBankAccountActiveCommand, Result<CompanyBankAccountDto>>
{
    private readonly ICompanyBankAccountRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public SetCompanyBankAccountActiveHandler(
        ICompanyBankAccountRepository repo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<CompanyBankAccountDto>> Handle(
        SetCompanyBankAccountActiveCommand cmd,
        CancellationToken ct
    )
    {
        var entity = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (entity is null)
            return Result<CompanyBankAccountDto>.NotFound("Cuenta bancaria no encontrada.");

        if (cmd.IsActive)
            entity.Enable(_u.UserId);
        else
            entity.Disable(_u.UserId);

        await _repo.SaveChangesAsync(ct);
        return Result<CompanyBankAccountDto>.Success(Map.ToDto(entity));
    }
}

public sealed class GetCompanyBankAccountByIdHandler
    : IRequestHandler<GetCompanyBankAccountByIdQuery, Result<CompanyBankAccountDto>>
{
    private readonly ICompanyBankAccountRepository _repo;
    private readonly ICurrentTenant _t;

    public GetCompanyBankAccountByIdHandler(ICompanyBankAccountRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<CompanyBankAccountDto>> Handle(
        GetCompanyBankAccountByIdQuery q,
        CancellationToken ct
    )
    {
        var entity = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        return entity is null
            ? Result<CompanyBankAccountDto>.NotFound("Cuenta bancaria no encontrada.")
            : Result<CompanyBankAccountDto>.Success(Map.ToDto(entity));
    }
}

public sealed class GetCompanyBankAccountListHandler
    : IRequestHandler<GetCompanyBankAccountListQuery, Result<IReadOnlyList<CompanyBankAccountDto>>>
{
    private readonly ICompanyBankAccountRepository _repo;
    private readonly ICurrentTenant _t;

    public GetCompanyBankAccountListHandler(ICompanyBankAccountRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<CompanyBankAccountDto>>> Handle(
        GetCompanyBankAccountListQuery q,
        CancellationToken ct
    )
    {
        var items = await _repo.GetListAsync(_t.TenantId, q.IsActive, ct);
        return Result<IReadOnlyList<CompanyBankAccountDto>>.Success(items.Select(Map.ToDto).ToList());
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static CompanyBankAccountDto ToDto(CompanyBankAccount x) =>
        new(
            x.Id,
            x.BankId,
            x.AccountType.ToString(),
            x.AccountNumber,
            x.DisplayName,
            x.AccountingAccountId,
            x.IsActive
        );
}
