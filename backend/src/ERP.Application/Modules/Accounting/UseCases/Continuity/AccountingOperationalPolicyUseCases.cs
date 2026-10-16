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
    /// <summary>
    /// Disabled no se habilita hasta que todas las integraciones automáticas (Ventas, Pagos,
    /// Cobros, Costos, …) converjan a la obligación durable: hoy algunas siguen contabilizando en
    /// línea y un Disabled parcial dejaría un libro inconsistente.
    /// </summary>
    public const bool DisabledAvailable = false;

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
            DisabledAvailable,
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
/// Activar no genera contabilización histórica. Disabled se rechaza siempre en esta fase y, por
/// regla permanente, cuando la Company está obligada a llevar contabilidad.
/// </summary>
public sealed class SetAccountingOperationalModeHandler
    : IRequestHandler<SetAccountingOperationalModeCommand, Result<AccountingOperationalPolicyDto>>
{
    private readonly IAccountingOperationalPolicyRepository _policies;
    private readonly ICompanyRepository _companies;
    private readonly AccountingOperationalPolicyReader _reader;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public SetAccountingOperationalModeHandler(
        IAccountingOperationalPolicyRepository policies,
        ICompanyRepository companies,
        AccountingOperationalPolicyReader reader,
        IUnitOfWork unitOfWork,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser user
    )
    {
        _policies = policies;
        _companies = companies;
        _reader = reader;
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

        var company = await _companies.GetByIdForTenantAsync(companyId, tenantId, ct);
        if (company is null)
            return Result<AccountingOperationalPolicyDto>.NotFound("Empresa no encontrada.");

        if (command.Mode == AccountingOperationalMode.Disabled)
        {
            if (company.IsAccountingReq)
                return Result<AccountingOperationalPolicyDto>.ValidationFailure(
                    "La empresa está obligada a llevar contabilidad: Contabilidad no puede deshabilitarse."
                );
            if (!AccountingOperationalPolicyReader.DisabledAvailable)
                return Result<AccountingOperationalPolicyDto>.ValidationFailure(
                    "Deshabilitar Contabilidad aún no está disponible: existen integraciones automáticas "
                        + "que todavía no usan la contabilización durable."
                );
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _policies.AcquireExclusiveLockAsync(companyId, ct);
            var versions = await _policies.ListAsync(tenantId, companyId, ct);
            var open = versions.LastOrDefault(v => v.EffectiveTo is null);
            if (open is not null && open.Mode == command.Mode)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<AccountingOperationalPolicyDto>.ValidationFailure(
                    $"Contabilidad ya está en modo {command.Mode} desde el {open.EffectiveFrom:yyyy-MM-dd}."
                );
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
