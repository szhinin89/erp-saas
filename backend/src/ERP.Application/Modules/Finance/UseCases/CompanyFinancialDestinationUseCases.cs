using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases;

// ── DTO ─────────────────────────────────────────────────────────────────

/// <summary>P0-02 Fase 4 — proyección de lectura de <see cref="CompanyFinancialDestination"/> (diseño §6.4).</summary>
public sealed record CompanyFinancialDestinationDto(
    Guid Id,
    string Code,
    string Name,
    string DestinationTypeCode,
    Guid AccountingAccountId,
    string CurrencyCode,
    Guid? CashRegisterId,
    string? BankInstitutionCode,
    string? BankAccountIdentifierNormalized,
    bool IsActive
);

// ── Commands ────────────────────────────────────────────────────────────

/// <summary>
/// P0-02 Fase 4 — único caso de uso de alta. Recibe los 8 campos estructurales (§6.4) — todos
/// inmutables una vez creado el destino (§6.4ter); ningún otro comando de esta fase puede volver
/// a enviarlos.
/// </summary>
public sealed record CreateCompanyFinancialDestinationCommand(
    string Code,
    string Name,
    FinancialDestinationTypeCode DestinationTypeCode,
    Guid AccountingAccountId,
    string CurrencyCode,
    Guid? CashRegisterId,
    string? BankInstitutionCode,
    string? BankAccountIdentifierNormalized
) : IRequest<Result<CompanyFinancialDestinationDto>>, ICompanyScopedRequest;

/// <summary>P0-02 Fase 4 — modifica exclusivamente <c>Name</c> (§6.4ter). Sin cuenta, sin estado, sin campos estructurales.</summary>
public sealed record UpdateCompanyFinancialDestinationNameCommand(Guid Id, string Name)
    : IRequest<Result<CompanyFinancialDestinationDto>>,
        ICompanyScopedRequest;

/// <summary>P0-02 Fase 4 — modifica exclusivamente <c>AccountingAccountId</c> (§6.4bis/§6.4ter). Sin nombre, sin estado, sin campos estructurales.</summary>
public sealed record ChangeCompanyFinancialDestinationAccountingAccountCommand(
    Guid Id,
    Guid AccountingAccountId
) : IRequest<Result<CompanyFinancialDestinationDto>>, ICompanyScopedRequest;

/// <summary>P0-02 Fase 4 — modifica exclusivamente <c>IsActive</c> (§6.4ter). Sin nombre, sin cuenta, sin campos estructurales.</summary>
public sealed record SetCompanyFinancialDestinationActiveCommand(Guid Id, bool IsActive)
    : IRequest<Result<CompanyFinancialDestinationDto>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreateCompanyFinancialDestinationValidator
    : AbstractValidator<CreateCompanyFinancialDestinationCommand>
{
    public CreateCompanyFinancialDestinationValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(CompanyFinancialDestination.CodeMaxLen)
            .WithMessage("El código es obligatorio (máximo 30 caracteres).");
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(CompanyFinancialDestination.NameMaxLen)
            .WithMessage("El nombre es obligatorio (máximo 200 caracteres).");
        RuleFor(x => x.DestinationTypeCode).IsInEnum();
        RuleFor(x => x.AccountingAccountId)
            .NotEmpty()
            .WithMessage("La cuenta contable es obligatoria.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty()
            .MaximumLength(CompanyFinancialDestination.CurrencyCodeMaxLen)
            .WithMessage("La moneda es obligatoria.");
    }
}

public sealed class UpdateCompanyFinancialDestinationNameValidator
    : AbstractValidator<UpdateCompanyFinancialDestinationNameCommand>
{
    public UpdateCompanyFinancialDestinationNameValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(CompanyFinancialDestination.NameMaxLen)
            .WithMessage("El nombre es obligatorio (máximo 200 caracteres).");
    }
}

public sealed class ChangeCompanyFinancialDestinationAccountingAccountValidator
    : AbstractValidator<ChangeCompanyFinancialDestinationAccountingAccountCommand>
{
    public ChangeCompanyFinancialDestinationAccountingAccountValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AccountingAccountId)
            .NotEmpty()
            .WithMessage("La cuenta contable es obligatoria.");
    }
}

public sealed class SetCompanyFinancialDestinationActiveValidator
    : AbstractValidator<SetCompanyFinancialDestinationActiveCommand>
{
    public SetCompanyFinancialDestinationActiveValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class CreateCompanyFinancialDestinationHandler
    : IRequestHandler<CreateCompanyFinancialDestinationCommand, Result<CompanyFinancialDestinationDto>>
{
    private readonly ICompanyFinancialDestinationRepository _repo;
    private readonly IAccountRepository _accounts;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CreateCompanyFinancialDestinationHandler(
        ICompanyFinancialDestinationRepository repo,
        IAccountRepository accounts,
        ICashRegisterRepository cashRegisters,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _accounts = accounts;
        _cashRegisters = cashRegisters;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<CompanyFinancialDestinationDto>> Handle(
        CreateCompanyFinancialDestinationCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var cid = _c.CompanyId;

        // SC-023: la cuenta contable no existe o no pertenece al tenant/company del destino.
        var account = await _accounts.GetByIdAsync(tid, cid, cmd.AccountingAccountId, ct);
        if (account is null)
            return Result<CompanyFinancialDestinationDto>.NotFound(
                "La cuenta contable indicada no existe o no pertenece a esta empresa."
            );

        // SC-024: la cuenta no es postable o está inactiva.
        if (!account.IsActive || !account.AllowsPosting)
            return Result<CompanyFinancialDestinationDto>.ValidationFailure(
                "La cuenta contable indicada no es postable o está inactiva."
            );

        // SC-026: la caja no existe o no pertenece al tenant/company (solo tipo caja).
        if (cmd.DestinationTypeCode == FinancialDestinationTypeCode.CashRegister)
        {
            if (cmd.CashRegisterId is null)
                return Result<CompanyFinancialDestinationDto>.ValidationFailure(
                    "La caja es obligatoria para un destino tipo caja."
                );

            var cashRegister = await _cashRegisters.GetByIdAsync(tid, cmd.CashRegisterId.Value, ct);
            if (cashRegister is null || cashRegister.CompanyId != cid)
                return Result<CompanyFinancialDestinationDto>.NotFound(
                    "La caja indicada no existe o no pertenece a esta empresa."
                );
        }

        CompanyFinancialDestination destination;
        try
        {
            // SC-022: configuración incompleta para el DestinationTypeCode (validada por el propio
            // dominio, §6.4 — CHECK combinado banco vs. caja, única fuente de verdad de la regla).
            destination = CompanyFinancialDestination.Create(
                tid,
                cid,
                cmd.Code,
                cmd.Name,
                cmd.DestinationTypeCode,
                cmd.AccountingAccountId,
                cmd.CurrencyCode,
                _u.UserId,
                cmd.CashRegisterId,
                cmd.BankInstitutionCode,
                cmd.BankAccountIdentifierNormalized
            );
        }
        catch (ArgumentException ex)
        {
            return Result<CompanyFinancialDestinationDto>.ValidationFailure(ex.Message);
        }

        await _repo.AddAsync(destination, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<CompanyFinancialDestinationDto>.Success(Map.ToDto(destination));
    }
}

public sealed class UpdateCompanyFinancialDestinationNameHandler
    : IRequestHandler<UpdateCompanyFinancialDestinationNameCommand, Result<CompanyFinancialDestinationDto>>
{
    private readonly ICompanyFinancialDestinationRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public UpdateCompanyFinancialDestinationNameHandler(
        ICompanyFinancialDestinationRepository repo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<CompanyFinancialDestinationDto>> Handle(
        UpdateCompanyFinancialDestinationNameCommand cmd,
        CancellationToken ct
    )
    {
        var destination = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (destination is null)
            return Result<CompanyFinancialDestinationDto>.NotFound(
                "Destino financiero no encontrado."
            );

        try
        {
            destination.UpdateName(cmd.Name, _u.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<CompanyFinancialDestinationDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<CompanyFinancialDestinationDto>.Success(Map.ToDto(destination));
    }
}

public sealed class ChangeCompanyFinancialDestinationAccountingAccountHandler
    : IRequestHandler<
        ChangeCompanyFinancialDestinationAccountingAccountCommand,
        Result<CompanyFinancialDestinationDto>
    >
{
    private readonly ICompanyFinancialDestinationRepository _repo;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public ChangeCompanyFinancialDestinationAccountingAccountHandler(
        ICompanyFinancialDestinationRepository repo,
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

    public async Task<Result<CompanyFinancialDestinationDto>> Handle(
        ChangeCompanyFinancialDestinationAccountingAccountCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var cid = _c.CompanyId;

        var destination = await _repo.GetByIdAsync(tid, cmd.Id, ct);
        if (destination is null)
            return Result<CompanyFinancialDestinationDto>.NotFound(
                "Destino financiero no encontrado."
            );

        // SC-023: la cuenta contable no existe o no pertenece al tenant/company del destino.
        var account = await _accounts.GetByIdAsync(tid, cid, cmd.AccountingAccountId, ct);
        if (account is null)
            return Result<CompanyFinancialDestinationDto>.NotFound(
                "La cuenta contable indicada no existe o no pertenece a esta empresa."
            );

        // SC-024: la cuenta no es postable o está inactiva.
        if (!account.IsActive || !account.AllowsPosting)
            return Result<CompanyFinancialDestinationDto>.ValidationFailure(
                "La cuenta contable indicada no es postable o está inactiva."
            );

        try
        {
            destination.ChangeAccountingAccount(cmd.AccountingAccountId, _u.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<CompanyFinancialDestinationDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<CompanyFinancialDestinationDto>.Success(Map.ToDto(destination));
    }
}

public sealed class SetCompanyFinancialDestinationActiveHandler
    : IRequestHandler<SetCompanyFinancialDestinationActiveCommand, Result<CompanyFinancialDestinationDto>>
{
    private readonly ICompanyFinancialDestinationRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public SetCompanyFinancialDestinationActiveHandler(
        ICompanyFinancialDestinationRepository repo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<CompanyFinancialDestinationDto>> Handle(
        SetCompanyFinancialDestinationActiveCommand cmd,
        CancellationToken ct
    )
    {
        var destination = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (destination is null)
            return Result<CompanyFinancialDestinationDto>.NotFound(
                "Destino financiero no encontrado."
            );

        destination.SetActive(cmd.IsActive, _u.UserId);

        await _repo.SaveChangesAsync(ct);
        return Result<CompanyFinancialDestinationDto>.Success(Map.ToDto(destination));
    }
}

// ── Query (Fase 13 Remediación 01) ─────────────────────────────────────

/// <summary>
/// P0-02 Fase 13 Remediación 01 — listado de <c>CompanyFinancialDestination</c> para el selector
/// de destino financiero (reembolso de crédito de proveedor) y para la administración limitada de
/// la fase (patrón Lista→Editor, `FinancialDestinationsPage.tsx`). Extensión puramente aditiva —
/// no reemplaza ni modifica los 4 casos de uso ya congelados en Fase 4/11.
/// </summary>
public sealed record GetCompanyFinancialDestinationListQuery(bool? IsActive)
    : IRequest<Result<IReadOnlyList<CompanyFinancialDestinationDto>>>,
        ICompanyScopedRequest;

public sealed class GetCompanyFinancialDestinationListHandler
    : IRequestHandler<
        GetCompanyFinancialDestinationListQuery,
        Result<IReadOnlyList<CompanyFinancialDestinationDto>>
    >
{
    private readonly ICompanyFinancialDestinationRepository _repo;
    private readonly ICurrentTenant _t;

    public GetCompanyFinancialDestinationListHandler(
        ICompanyFinancialDestinationRepository repo,
        ICurrentTenant t
    )
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<CompanyFinancialDestinationDto>>> Handle(
        GetCompanyFinancialDestinationListQuery q,
        CancellationToken ct
    )
    {
        var items = await _repo.GetListAsync(_t.TenantId, q.IsActive, ct);
        return Result<IReadOnlyList<CompanyFinancialDestinationDto>>.Success(
            items.Select(Map.ToDto).ToList()
        );
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static CompanyFinancialDestinationDto ToDto(CompanyFinancialDestination d) =>
        new(
            d.Id,
            d.Code,
            d.Name,
            d.DestinationTypeCode.ToString(),
            d.AccountingAccountId,
            d.CurrencyCode,
            d.CashRegisterId,
            d.BankInstitutionCode,
            d.BankAccountIdentifierNormalized,
            d.IsActive
        );
}
