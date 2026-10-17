using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyFiscal;

public sealed class UpdateCompanyFiscalHandler
    : IRequestHandler<UpdateCompanyFiscalCommand, Result<CompanyProfileDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;
    private readonly ICurrentUser _currentUser;
    private readonly IAccountingOperationalPolicyRepository _accountingPolicies;
    private readonly ICompanyClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCompanyFiscalHandler(
        ICompanyAccessGuard accessGuard,
        ICompanyRepository companies,
        ICurrentUser currentUser,
        IAccountingOperationalPolicyRepository accountingPolicies,
        ICompanyClock clock,
        IUnitOfWork unitOfWork
    )
    {
        _unitOfWork = unitOfWork;
        _accessGuard = accessGuard;
        _companies = companies;
        _currentUser = currentUser;
        _accountingPolicies = accountingPolicies;
        _clock = clock;
    }

    public async Task<Result<CompanyProfileDto>> Handle(
        UpdateCompanyFiscalCommand command,
        CancellationToken cancellationToken
    )
    {
        var access = await _accessGuard.RequireCurrentCompanyAsync(cancellationToken);
        if (!access.IsSuccess)
            return Result<CompanyProfileDto>.Failure(access.Error!, access.Code);

        // ACCOUNTING-CONTINUITY-05: mismo lock exclusivo de política que SetAccountingOperationalMode;
        // la invariante Required ⇒ Enabled no puede romperse por dos cambios concurrentes.
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _accountingPolicies.AcquireExclusiveLockAsync(access.Value!.CompanyId, cancellationToken);
            var result = await UpdateAsync(command, access.Value!.CompanyId, access.Value!.TenantId, cancellationToken);
            if (result.IsSuccess)
                await _unitOfWork.CommitAsync(cancellationToken);
            else
                await _unitOfWork.RollbackAsync(cancellationToken);
            return result;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Result<CompanyProfileDto>> UpdateAsync(
        UpdateCompanyFiscalCommand command,
        Guid companyId,
        Guid tenantId,
        CancellationToken cancellationToken
    )
    {
        var entity = await _companies.GetTrackedByIdForTenantAsync(companyId, tenantId, cancellationToken);
        if (entity is null)
            return Result<CompanyProfileDto>.Failure("Empresa no encontrada.");

        // ACCOUNTING-CONTINUITY-01 (accounting.md §3): una empresa obligada a llevar contabilidad
        // debe tener Accounting Enabled; no se activa automáticamente desde el dato fiscal.
        if (
            command.IsAccountingReq
            && !entity.IsAccountingReq
            && !await IsAccountingEnabledFromTodayAsync(entity.Id, entity.TenantId, cancellationToken)
        )
            return Result<CompanyProfileDto>.ValidationFailure(
                "Para marcar la empresa como obligada a llevar contabilidad, primero habilite "
                    + "Contabilidad en Contabilidad > Configuración > Modo operativo."
            );

        entity.UpdateFiscalSettings(
            command.TaxRegimeCode,
            command.IsAccountingReq,
            command.SpecialTaxpayerNo,
            command.IsForeignTrade,
            command.WithholdsRenta,
            command.WithholdsVat,
            _currentUser.UserId
        );

        await _companies.SaveChangesAsync(cancellationToken);

        return Result<CompanyProfileDto>.Success(CompanyProfileDto.FromEntity(entity));
    }

    /// <summary>Enabled vigente hoy y sin ninguna versión posterior que lo deshabilite.</summary>
    private async Task<bool> IsAccountingEnabledFromTodayAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken ct
    )
    {
        var today = await _clock.TodayAsync(companyId, tenantId, ct);
        var versions = await _accountingPolicies.ListAsync(tenantId, companyId, ct);
        var current = versions.FirstOrDefault(v => v.Covers(today));
        return current?.Mode == AccountingOperationalMode.Enabled
            && versions
                .Where(v => v.EffectiveFrom > today)
                .All(v => v.Mode == AccountingOperationalMode.Enabled);
    }
}
