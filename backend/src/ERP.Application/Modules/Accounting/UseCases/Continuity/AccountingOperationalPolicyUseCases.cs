using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Accounting.UseCases.Continuity;

// ACCOUNTING-CONTINUITY-01 — modo operativo de Accounting por Company, versionado con vigencia
// (docs/architecture/accounting.md §3). La obligación fiscal sigue siendo Company.IsAccountingReq.

public sealed record GetAccountingOperationalPolicyQuery()
    : IRequest<Result<AccountingOperationalPolicyDto>>,
        ICompanyScopedRequest;

public sealed record SetAccountingOperationalModeCommand(
    AccountingOperationalMode Mode,
    DateOnly EffectiveFrom,
    string? Reason
) : IRequest<Result<AccountingOperationalPolicyDto>>, ICompanyScopedRequest;

public sealed class SetAccountingOperationalModeCommandValidator
    : AbstractValidator<SetAccountingOperationalModeCommand>
{
    public SetAccountingOperationalModeCommandValidator()
    {
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.EffectiveFrom)
            .NotEqual(default(DateOnly))
            .WithMessage("La fecha de vigencia es obligatoria.");
        RuleFor(x => x.Reason).MaximumLength(AccountingOperationalPolicy.ReasonMaxLength);
    }
}

/// <summary>Lectura compartida de la política vigente (query y respuesta del comando).</summary>
public sealed class AccountingOperationalPolicyReader
{
    private readonly IAccountingOperationalPolicyRepository _policies;
    private readonly ICompanyRepository _companies;
    private readonly ICompanyClock _clock;

    public AccountingOperationalPolicyReader(
        IAccountingOperationalPolicyRepository policies,
        ICompanyRepository companies,
        ICompanyClock clock
    )
    {
        _policies = policies;
        _companies = companies;
        _clock = clock;
    }

    public async Task<AccountingOperationalPolicyDto?> ReadAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct
    )
    {
        var company = await _companies.GetByIdForTenantAsync(companyId, tenantId, ct);
        if (company is null)
            return null;

        var today = await _clock.TodayAsync(companyId, tenantId, ct);
        var versions = await _policies.ListAsync(tenantId, companyId, ct);
        return new AccountingOperationalPolicyDto(
            company.IsAccountingReq,
            versions.Count > 0,
            versions.FirstOrDefault(v => v.Covers(today))?.Mode,
            today,
            // ACCOUNTING-CONTINUITY-05: Disabled se ofrece siempre salvo empresa obligada; sus
            // precondiciones (pendientes, vigencia) se validan al registrarlo.
            !company.IsAccountingReq,
            versions.Select(AccountingOperationalPolicyVersionDto.From).ToList()
        );
    }
}

public sealed class GetAccountingOperationalPolicyHandler
    : IRequestHandler<GetAccountingOperationalPolicyQuery, Result<AccountingOperationalPolicyDto>>
{
    private readonly AccountingOperationalPolicyReader _reader;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;

    public GetAccountingOperationalPolicyHandler(
        AccountingOperationalPolicyReader reader,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
    {
        _reader = reader;
        _tenant = tenant;
        _company = company;
    }

    public async Task<Result<AccountingOperationalPolicyDto>> Handle(
        GetAccountingOperationalPolicyQuery query,
        CancellationToken ct
    )
    {
        var dto = await _reader.ReadAsync(_tenant.TenantId, _company.CompanyId, ct);
        return dto is null
            ? Result<AccountingOperationalPolicyDto>.NotFound("Empresa no encontrada.")
            : Result<AccountingOperationalPolicyDto>.Success(dto);
    }
}

/// <summary>
/// Crea una versión nueva de la política (append-only; la versión abierta se cierra el día previo).
/// Activar no genera contabilización histórica. Bajo el lock exclusivo de política (compartido por el
/// registro de obligaciones, el cierre y el cambio fiscal) se validan Required ⇒ Enabled, la vigencia
/// no retroactiva y la ausencia de obligaciones/hechos sujetos sin resolver para Disabled.
/// </summary>
public sealed class SetAccountingOperationalModeHandler
    : IRequestHandler<SetAccountingOperationalModeCommand, Result<AccountingOperationalPolicyDto>>
{
    private readonly IAccountingOperationalPolicyRepository _policies;
    private readonly ICompanyRepository _companies;
    private readonly AccountingOperationalPolicyReader _reader;
    private readonly AccountingModeChangePreconditions _preconditions;
    private readonly ICompanyClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public SetAccountingOperationalModeHandler(
        IAccountingOperationalPolicyRepository policies,
        ICompanyRepository companies,
        AccountingOperationalPolicyReader reader,
        AccountingModeChangePreconditions preconditions,
        ICompanyClock clock,
        IUnitOfWork unitOfWork,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser user
    )
    {
        _policies = policies;
        _companies = companies;
        _reader = reader;
        _preconditions = preconditions;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _tenant = tenant;
        _company = company;
        _user = user;
    }

    public async Task<Result<AccountingOperationalPolicyDto>> Handle(
        SetAccountingOperationalModeCommand command,
        CancellationToken ct
    )
    {
        var tenantId = _tenant.TenantId;
        var companyId = _company.CompanyId;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _policies.AcquireExclusiveLockAsync(companyId, ct);

            // IsAccountingReq se lee dentro del lock: UpdateCompanyFiscal toma el mismo lock exclusivo.
            var company = await _companies.GetByIdForTenantAsync(companyId, tenantId, ct);
            if (company is null)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<AccountingOperationalPolicyDto>.NotFound("Empresa no encontrada.");
            }

            var versions = await _policies.ListAsync(tenantId, companyId, ct);
            var open = versions.LastOrDefault(v => v.EffectiveTo is null);
            if (open is not null && open.Mode == command.Mode)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<AccountingOperationalPolicyDto>.ValidationFailure(
                    $"Contabilidad ya está en modo {command.Mode} desde el {open.EffectiveFrom:yyyy-MM-dd}."
                );
            }

            var today = await _clock.TodayAsync(companyId, tenantId, ct);
            var rejection = await _preconditions.CheckAsync(
                tenantId,
                companyId,
                company.IsAccountingReq,
                versions,
                command.Mode,
                command.EffectiveFrom,
                today,
                ct
            );
            if (rejection is not null)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<AccountingOperationalPolicyDto>.ValidationFailure(rejection);
            }

            open?.EndBefore(command.EffectiveFrom, _user.UserId);
            await _policies.AddAsync(
                AccountingOperationalPolicy.Create(
                    tenantId,
                    companyId,
                    command.Mode,
                    command.EffectiveFrom,
                    command.Reason,
                    _user.UserId
                ),
                ct
            );
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            _unitOfWork.ClearChangeTracker();
            throw;
        }

        var dto = await _reader.ReadAsync(tenantId, companyId, ct);
        return Result<AccountingOperationalPolicyDto>.Success(dto!);
    }
}
